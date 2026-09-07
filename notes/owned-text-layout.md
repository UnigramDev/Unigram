# A text control that owns its layout

The case for replacing the `RichTextBlock` inside `FormattedTextBlock` with a layout we own,
and the order to do it in. Nothing here is built yet.

## Why

Not primarily speed. The reasons are structural, and every one of them is specific to this
one control:

- **A DependencyObject per run, span and paragraph, per message.** `XamlDirect` exists in this
  codebase for this control alone (`FormattedTextBlock.cs`, the `GetOrCreateRun` /
  `ApplyRunProperties` family) — that is what the overhead of `Inline` objects forced.
- **The pool exists for this control alone** (`FormattedTextBlockRecyclePool`), and so do the
  teardown guards: the `_released` check in `SetText`, and the `RelativeDateService`
  registration that pins a block, its paragraph and its runs for the session if it is dropped
  before `Unloaded` arrives.
- **Accessibility is already broken, not merely absent.** Caret browsing (F7) crashes the app
  at the first `RichTextBlock` containing a `Hyperlink`. Whatever an owned control has to build
  for automation, it is building on top of a crash, not replacing something that works.
- **Everything measured is a reconstruction.** The block is laid out by `RichTextBlock` and
  then laid out *again* by DirectWrite to answer questions about itself: where the last line
  ends (the footer), which rectangles a spoiler covers, the line boxes for the skeleton, the
  column widths a table needs. The two engines agree to about a pixel, and every place they
  disagree has cost a fix:
  - a 1 DIP `Slop` per table column (`PageBlockRenderer.cs`, `TableRoot`);
  - the footer overlapping text when a custom font is set (still open — see
    [[dwrite-vs-xaml-measurement]]);
  - spoiler and highlight shapes landing right of RTL paragraphs, fixed by laying out in the
    arranged width rather than the offered one (`FormattedTextBlock.ArrangedWidth`);
  - bidi runs having to be merged per line before a shape could be built from them.
  - inline buttons, which cannot be measured at all: DirectWrite cannot see them.

Owning the layout deletes the category rather than the individual bugs, and deletes the second
layout pass with it.

## What already exists

More than half of the measurement side is in the tree:

- `Telegram.Native/TextFormat.{idl,h,cpp}` — a retained `IDWriteTextLayout` with `Configure`
  (size, width, reading direction, wrapping, applied only where they differ), `ContentEnd`,
  `ContentWidths` (min/max content), `MaxLines`, `RangeMetrics`, `LineMetrics` (runs merged per
  line) and a private `HitTestRange`.
- `Direct2DDevice` — the DWrite factory, the packaged font collection, and thin wrappers over
  the above for callers that have text rather than a retained layout.
- `graphic_dwrite.cpp` — MicroTeX draws text through D2D already, so the drawing backend is not
  new ground.
- `CompositionDevice` / `CreateDrawingSurface`, and the rounded-polygon path builder used for
  highlights and skeletons.

## Shape

- **Layout object.** `TextFormat` grows from "a layout that answers questions" into the layout
  the control renders from. One per block, rebuilt on text change, reconfigured on width.
- **Drawing.** `ID2D1DeviceContext::DrawTextLayout` with colour fonts enabled, into a
  composition surface **the layout owns**: one per layout, kept and resized rather than
  recreated per redraw, and redrawn by the layout itself on `RenderingDeviceReplaced` - the
  same pattern `FreeformGradientSurface` and `MessageBubbleNineGrid` use. The surface object
  never changes identity, so the control sets it on its brush once and device-lost recovery
  needs no registry of live blocks. `DirectTextLayout` is `IClosable` for it: the surface is a
  region of the device's atlas, and waiting for the wrapper to be collected holds it.
  If realization churn shows up on the
  scroll path, move to a shared virtual surface used as an **atlas** — a rect per block, so the
  visual stays inside the item and XAML keeps doing the positioning (see the note on why a
  viewport-space canvas cannot work: virtualization has no stable content space).
- **Non-text children.** Custom emoji, inline buttons and animated emoji become inline objects
  of a known width in the layout, with their XAML/composition children positioned from the
  layout metrics. This is the part that is *better* owned: today they are invisible to
  measurement.
- **Declared paragraphs.** The existing feature — a caller declares `Paragraph`s in the
  control's own XAML and the text is appended into a `Span` on the last one (`ChatCell` puts
  the preview after the sender name) — becomes leading content in our layout. The XAML shape
  can stay; only the plumbing behind it changes.
- **Everything derived comes off the layout**: selection, spoiler geometry, the skeleton, the
  footer position, table columns. No second engine to reconcile with.
- **Automation.** A peer over our own layout. `Name` only for cells; `ITextProvider` /
  `ITextRangeProvider` for message text, where the range data is exactly what the layout has.

## Order

1. **Chat list cells first.** Highest instantiation rate, short strings, no selection, no
   spoilers, no inline buttons, automation is a name. A regression is immediately visible and
   cheap to revert, and it proves the engine.
2. **Message text**, behind a `Diagnostics` kill-switch, with the automation peer.
3. Instant view and the rest follow the message path.

Both controls coexist through the migration; `FormattedTextBlock` is not edited in place.

## Where it stands

Behind `Diagnostics.DirectTextDebug`, read once per process so a list never mixes engines:

- `Telegram.Native/DirectTextLayout` — the layout the control owns, and one
  `IDWriteTextLayout` per paragraph inside it. Text, entities, font (family, size, weight,
  style), width, per-paragraph direction and alignment, max lines, colours and inline objects
  are its state; it measures, draws into a surface it owns, hit tests, and answers with ranges,
  lines, line lengths, line count and the content end. `IClosable`, because the surface it
  holds is a region of the device's atlas.
- `Telegram.Native/Controls/DirectTextBlockBase` — a `Panel` in C++/WinRT that owns the
  viewport subscription, so the framework's event args never cross into managed code.
- `Telegram/Controls/DirectTextBlock` — measures and draws from one layout, hosts the emoji
  players, inline buttons and the spoiler particles as children, and implements
  `ISelectableControl`, `IRelativeDateHost`, `ITrimmableText` and `ITextPresenter`.
- A formula is the one box the layout both reserves and draws. `SetMathObject` parses the
  expression into a `RichMathSurface` and keeps it on the `InlineObject`, which draws it in
  `IDWriteInlineObject::Draw` - inside `DrawTextLayout`, into the context the text is being
  drawn into, at the origin DirectWrite works out, in the colour the text is drawn in. So
  there is no element to measure, arrange, colour or recycle, and no bitmap: it costs a
  `Graphics2D_dwrite` over the shared context, which composes onto the transform it finds and
  puts back the transform and antialiasing it took. `RichMathImage` remains for the two
  callers that host a formula as an image - the inline path in `FormattedTextBlock` and the
  instant view page block, which is a formula alone with no text around it - and those go
  through `Direct2DDevice.RenderMath`, which is where the WIC and D2D factories are.
- `MessageTextBlock` renders every message through it, splitting only for a quote or a code
  block; `PageBlockRenderer` builds every text in an instant view page through it, styled by
  the two keyed styles in `App.xaml`.
- **The chat list is not on it.** `ChatCell` was the first thing ported and was reverted on
  2026-09-07: a second template keyed `DirectChatCellStyle` in `ChatCell.Direct.xaml`, and a
  `SetBriefText` that composed the sender or draft prefix into one string and coloured it as a
  range, because a `DirectTextBlock` has no `Run` to give it its own colour. It is a preview
  line - one line, trimmed, no links, no selection - so it exercises almost none of the engine
  while touching every path in the busiest cell in the app. Worth doing after the message
  path is proven, not before; `git log` has it if it is wanted back.

**Unbuilt as of 2026-09-07.** `Telegram.Native` must be rebuilt: `TextParagraph`,
`SetParagraphs`, `LineCount`, `IsTrimmed`, `Alignment`, `FontFamily`/`FontWeight`/`Italic`,
`Render`/`RenderedOrigin`→`Extent` and `DirectTextBlockBase` are all new or changed metadata.
Nothing in the last stretch has been run.

**What to exercise first**, in the order the risk sits: a message whose paragraphs read
different ways (the paragraph rewrite is the newest core change), quotes and code blocks, an
instant view page (every text there moved to `ITextPresenter` and the new styles), then the
search highlight, the translation skeleton, expandable quotes and a formula. The chat list
shows the old engine whatever the flag says.

Beside the engine, and not behind the flag: `WindowContext`'s rasterization-scale registry was
removed - a scale change already forces a recursive measure invalidation on the whole tree, so
`AnimatedImage` checks the scale in its arrange pass and posts the reload, and `RichMathImage`
rasterizes in arrange against what it last drew.

## Parity with FormattedTextBlock

Done: styled runs, coloured ranges, custom emoji as inline objects with real players,
spoilers (hidden text plus particles, revealed on click), links (hit test, cursor, click,
entity round trip), manager-driven selection with copy as text and entities, single line
trimming with an ellipsis, the content end the footer is placed from, RTL alignment, bidi line
merging, DPI, inline buttons measured in the measure pass, a UIA text pattern with a peer per
link, the quote highlight geometry (`GetHighlightRectangles`, which `FormattedTextBlock` now
answers too, so `MessageBubble` no longer lays the text out a second time), viewport awareness
driven from the native base so the emoji players stop off screen, link tooltips on the system's
own timings and placed against the line of the link, quotes and code blocks - so every message
shape renders - relative dates that tick, dates and inline code as links, the entity context
menu, which `MessageHelper` now builds from the entity rather than from the `Hyperlink` that
drew it, the search query highlight and the translation skeleton, expandable quotes, alignment
(left, centre, right, and the direction deciding when nothing is asked), the font family,
weight and style - so `PageBlockRenderer` builds every text in a page on this engine - a
paragraph each, laid out in its own direction and stacked, and math formulas drawn into the
surface by the layout itself.

Small - a port of logic that exists, onto range data the layout already gives:

- hyperlink underline style, `AutoFontSize` resolution;
- theme changes: `Foreground`, `HyperlinkForeground` and `SelectionHighlightColor` are theme
  resources set by the implicit style in `CommonStyles.xaml` (implicit because a `Panel` has no
  `DefaultStyleKey`), and the last two are also watched for their `Color` changing in place,
  which is what a chat theme does. Still baked at `SetText`: the spoiler particles take the
  foreground colour when the presenter is built, and a styled run that carries a colour of its
  own keeps the one it was given;
- **the selection has no brush of its own.** The style stands `MessageHeaderBorderBrush` in for
  it - in the three `DirectTextBlock` styles and now in `RichMathImage`'s as well; a real
  `TextSelectionBackgroundBrush` belongs in `Theme.cs` beside the other message brushes, in all
  four palettes;
- the selection hit token is always `SelectionHit.None`, so a word or paragraph expansion
  cannot tell the end of a line from the start of the next one.

Real work:

- **tab navigation** - nothing takes focus, so a link cannot be reached from the keyboard;
- **icons** - `TextEntityTypeIcon`, which is a TODO on the inline path too, so neither engine
  renders one today;
- **custom emoji sized from the font** - `EmojiSize` is a fixed 20 DIPs, where the inline path
  scales the player and its frame with the paragraph's font size. A message at a larger text
  size gets emoji that no longer match the glyphs around them.

Worth doing, not done: **lay the paragraphs out in the width the block was arranged at**, when
they do not all read the same way. The box they share is as wide as the widest of them, which
is what puts a right-to-left paragraph against the right edge of the TEXT; where the paragraphs
disagree, each should instead sit against its own edge of the BLOCK, which is what the inline
path does with one `RichTextBlock` spanning the bubble. `Render` is already given the arranged
size - it is called from `ArrangeOverride` with `finalSize` - so it could hand that width to the
layout and let each paragraph align in it, in place of the single `_contentLeft` that moves the
whole surface to one side. Only worth the arrange-time reflow for the mixed case: a block whose
paragraphs agree is laid out once at measure and moved as a whole, which costs nothing.

What the design rests on, and what running it settled:

- **trailing whitespace and RTL.** `metrics.width` leaves out the whitespace a line ends with,
  `metrics.left` is the left-most point of the text INCLUDING it, and right to left that
  whitespace is at the left end - so a box no wider than the text makes `left` negative by the
  width of a space, and anything built from `left + width` comes out a few pixels short. Both
  offsets are clamped at zero where the surface is sized;
- **`IsTextTrimmable` is not "is trimmed".** It answers whether the text is longer than the
  lines a quote collapses to, whichever limit is in force at the moment - an expanded quote
  still says yes, or the chevron that expanded it disappears and it can never be collapsed.
  `FormattedTextBlock` measures it separately for the same reason;
- **a height cut is trimmed like a width cut.** `MaxLines` above one cuts the layout box to the
  height of that many lines (`SetMaxHeight`) with the trimming sign set. DirectWrite documents
  trimming for a line that overflows the box's WIDTH, and it was an open question whether the
  sign also lands on the last line a HEIGHT cut leaves - it does, verified in the app on
  2026-09-07. So there is no truncate-and-lay-out-again path to write, and no offsets to shift;
- **an inline object draws at the origin it is given, whichever way the line reads.**
  `IDWriteInlineObject::Draw` is handed an `isRightToLeft` flag as well, and the layout ignores
  it: a formula in a right-to-left line lands where it should regardless (verified 2026-09-07);
- **paragraphs.** `DirectTextLayout` holds one `IDWriteTextLayout` per paragraph and stacks
  them, because DirectWrite gives a layout a single reading direction and a single alignment.
  Offsets stay what the caller gives - into the whole text - and every answer maps them to the
  paragraph they fall in; the newline between two paragraphs is in the text and in no layout,
  so it belongs to the line the paragraph before it ends with. `MessageTextBlock` splits a
  message only for a quote or a code block now, which are a control each, not a layout each.

What disappears rather than reaching parity: the `XamlDirect` run building, the recycle pool,
the rendered-to-styled index maps, the zero-width marks around custom emoji, and the teardown
guards. That is what the list above buys.

## What it cost to learn

Every one of these was a crash or a visible bug, and none of them is about text:

- **A layout pass may not change layout.** Adding a child, setting `Width`, flipping
  `Visibility` in measure or arrange invalidates the pass running them: XAML gives up after
  its iteration limit and fails fast with `0x88000FA8` out of `CLayoutManager::UpdateLayout`.
  Children are built where the text is set; measure measures and arrange arranges.
- **`BeginDraw` hands back the composition graphics device's shared context.** `SetDpi` on it
  persisted into every other surface drawn from that device - gradients, bubble backgrounds,
  cached animation frames - and at 150% none of them rendered again. Scale through the
  transform instead and leave the context alone.
- **A visual sized to the arranged size resamples the surface.** The surface is a whole number
  of pixels and the arranged size is not; size the visual to the surface's own pixel size
  converted back to DIPs, or every glyph is blurred by the fraction between them.
- **An element that paints nothing is not hit tested**, whatever `IsHitTestVisible` says. The
  host needs a brush before any of links, a drag, or the selection manager can see a point.
- **`NO_WRAP` does not remove the breaks in the text.** One line means flattening them, one
  character for one character so every offset a caller gave still lands where it did.
- **Text that cannot overflow never trims.** A horizontal `StackPanel` measures with unbounded
  width, so the preview trimmed nothing until its parent became a `Grid`.
- **`CompositionPathGeometry.Path` cannot be set to null** - it takes the process with it.
  Hide the shape visual instead.
- **A hit test answers two questions.** DirectWrite reports the nearest position wherever the
  point is, and separately whether it was on the text: a selection wants the first, a link the
  second, and conflating them made dragging past the end of a line jump to the whole message.
- **The other template's children are not there.** `BriefLabel`, `DraftLabel`, `FromLabel` are
  Runs inside the inline paragraph; every use of one from shared code behind is a null
  reference the moment the direct style is picked.

## What the first slice has to prove

- realization cost and memory per cell against the current control, measured, not argued;
- surface churn while scrolling (allocation per realization is the thing that would kill the
  naive shape);
- colour emoji rendering, and animated custom emoji hosted as children at layout positions;
- spoilers: the overlay, the hit test that reveals them, and the hidden state;
- RTL and bidi parity;
- DPI and font-size changes.

## Open

- Where the surface lives, and whether an atlas is needed on day one.
- Whether the layout object stays `TextFormat` or splits into layout + renderer.
- Text selection across blocks, which today is coordinated by `_selectionManager` over
  `RichTextBlock` hit testing.
