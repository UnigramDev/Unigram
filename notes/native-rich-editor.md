# Native rich message editor

Replaces the WebView2/ProseMirror editor (`Libraries/unigram-iv-editor`, hosted by
`Views/Host/RichTextWindow`) with a native one, and adds what the web editor never had:
media composed from scratch (local files, uploaded on send), button rows and inline buttons,
maps, and editing of every block a sent message can carry. Mouse, keyboard, touch and
screen readers are first-class.

The 2026-08 decision to stay on WebView rested on "Windows has no block-capable native rich
text primitive". That is still true; the answer is not RichEditBox but the pieces the app has
grown since: `DirectTextLayout` (owned DirectWrite layout with hit testing, ranges, inline
objects, math) and `DirectTextBlock` (renders it, with custom emoji, inline buttons,
spoilers, code highlighting and a UIA text pattern).

## Prior art

- **tdesktop** (`iv/editor/`, ~42k lines, Qt): typed block tree (`RichPage`), a flat list of
  editable text leaves in document order, the read-view renderer in an edit mode, one live
  `InputField` moved over the active leaf, whole-document snapshot undo, structural
  selections (blocks / list items / table rows / cells) beside text selections, markdown
  input rules on Space, media uploaded up front and patched in on completion, `EditRichButtonBox`
  (label, Url / CopyText / UserProfile, colour, inline-or-row). Limits default to 32768 chars,
  500 blocks, depth 16, 50 media, 20 table columns, 8 buttons per row, 10 items per collage.
  No touch caret, no accessibility beyond button names.
- **Android** (`ui/iv/`, ~27k lines): a flat `BlockRow` list in a RecyclerView with one
  `EditText` per text block; nesting is encoded as row attributes (list `level`/`num`, a quote
  id stack, details header + end sentinel) and folded back into a tree on send. Cross-block
  selection reuses the IV viewer's selection helper and locks the EditTexts while it is active.
  Undo stores row snapshots: an 800ms debounce, an immediate flush past 16 changed chars, and a
  depth of 150. Media is uploaded on insert, and send is blocked while uploads are pending.
  Slash commands (`RichCommand`) and markdown triggers. Inline-button editor offers Link /
  Copy / User Profile. Limits are rechecked 1s after a change; over the limit, the send button
  is disabled.
  - String keys to reuse: `Article*` (headings, hints, commands, table ops, conversion,
    drafts) mostly exist in `Resources.xml` already. Missing, to add from the Android export
    when needed: `ArticleTableBordered`, `ArticleTableCompact`, `ArticleTableTitleHint`, all
    `RichEditor*` (button editor), `AccDescrIV*`.

We take the tree + leaf index + snapshot undo + input rules from tdesktop. We do **not** take
the floating input field: on Windows it would be a RichEditBox, whose rendering does not match
`DirectTextLayout`, so the active leaf would visibly change when focused. We own the caret
instead, through `CoreTextEditContext`, which gives IME, the touch keyboard, handwriting and
dictation without a text control.

## Layout of the code

`Telegram/Controls/RichEditor/` — namespace `Telegram.Controls.RichEditor`.

| File | Role |
|---|---|
| `RichDocument.cs` | Block tree: `RichBlock` subclasses, `RichTextLeaf`, `RichDocument` (leaf index, clone) |
| `RichTextLeaf.cs` | Editable text: a string plus `RichTextRun`s (marks, link payload, atomic objects) |
| `RichDocumentConverter.cs` | `PageBlock` (sent message) / `FormattedText` (composer) → document; document → `InputPageBlock` |
| `RichEditorEngine.cs` | Selection and every mutation, undo/redo; no XAML, testable on its own |
| `RichEditorView.cs` | The control: block presenters, caret, selection, pointer/touch, `CoreTextEditContext` |
| `RichEditorAutomationPeer.cs` | `ITextProvider` over the whole document |

## Model

- A **leaf** (`RichTextLeaf`) is every place a caret can be: paragraph/heading/code/footer
  text, quote credits, pullquote text, details header, list item paragraphs, table cells,
  captions and credits of media. A leaf is a `string` and a run list covering it exactly.
- **Atomic inline objects** - custom emoji, inline formula, inline button, icon, anchor - are
  one `U+FFFC` each in the leaf string, with the object on a run of length 1. One caret stop,
  one IME character, one UIA character. They expand to their RichText only at the edges.
- **Atomic blocks** - divider, anchor, block formula, media, collage/slideshow, map, button
  row - have no leaf of their own (their captions do). They are selected as a node.
- Positions are `(leaf index, offset)`; the document keeps `Leaves` in document order, rebuilt
  on structural change only.
- Marks: bold, italic, underline, strikethrough, spoiler, monospace, marked, subscript,
  superscript, plus payload marks (url, mention name, date). Typing inherits the marks of the
  character before the caret except payload marks at their end, plus "stored" marks toggled
  with an empty selection.

## Undo

Snapshot per transaction (tdesktop does the same, uncapped; we cap at 200). Typing coalesces
into one transaction until the caret jumps, a non-typing command runs, or a word boundary
follows a 1s pause. Cloning shares immutable payloads (media sources, `InlineButton`s).

## Media

From scratch: picker / paste / drop → `StorageMedia` → the existing `MessageFactory` builds
`InputMessagePhoto/Video/...`, and its `InputPhoto/InputVideo/...` goes straight into
`inputPageBlockPhoto/Video/...` - TDLib uploads on send. Editing a sent message reuses
`InputFileId` (as `PageBlockHelper.ToInputBlocks` does). Display is a static thumbnail plus
native overlays; nothing decodes inside the editor.

## Buttons

`inputPageBlockButtonRow` and `richTextButton`. Regular users may only use
`inlineKeyboardButtonTypeUrl`, `...User` and `...CopyText` (TDLib docs on `inlineButton`).
Styles: `buttonStyleDefault/Primary/Danger/Success/Link`.

## Order of work

1. Model, converters, engine (text ops, structure ops, undo). **in progress**
2. View: presenters per block over `DirectTextBlock`, caret, mouse selection, keyboard,
   `CoreTextEditContext`.
3. Swap `RichTextWindow` onto it; toolbar and context menu to the engine.
4. Media, collage/slideshow, maps, button rows, inline buttons, formulas, tables, details.
5. Clipboard: internal format, `FormattedText` + HTML out, text/HTML/image in.
6. Touch: tap/long-press, selection grippers, touch context menu.
7. Accessibility: document text pattern, block peers, announcements.
8. Limits, drafts, markdown input rules, polish; delete the WebView editor.

## State

Each step updates this section. What follows is a log, oldest first: a later entry settles what
an earlier one left open. The list just below is the current state.

**Open (checked against the code, 2026-10-14):**
- Theming: the selection is a stand-in brush (`RichEditorSelectionBrush`, TODO in RichEditor.xaml).
- Markdown on paste, which Android offers after a paste.
- Dragging a selection out of the editor.
- Audio and file blocks look like a badge.
- Slideshow design is wrong: the frame takes the size of the slide being shown
  (`RichMediaRenderer`, `SetSize` from `group.Items[page]`), so turning a page resizes the block
  and moves everything under it. Its size must not depend on the visible slide: one size for
  the slideshow, every slide fitted inside it.
- No test project: the engine's tests live in a scratch harness only.

**Step 1 done (2026-10-12).** `RichTextLeaf`, `RichDocument`, `RichDocumentConverter`,
`RichEditorEngine` (+ `.Editing`, `.Structure`). Not yet in either csproj. Compiled standalone
against the real generated TDLib types, and exercised by 87 engine tests, all passing: typing, marks and
stored marks, link growth, Enter rules (headings, code, lists, quotes, details), Backspace
rules (merge, lift, select-then-delete blocks), cross-block delete, lists (wrap, style, indent,
outdent order), quotes, atomic-block navigation, undo/redo coalescing, the RichText round trip,
word and grapheme boundaries.

The harness is in the session scratchpad (`engine/`): an SDK console project that compiles
`obj/modern/x64/Debug/.../TdSchema/TdDotNetApi.g.cs`, `Td/Vector.cs`, `Td/ClientJson.cs`,
`Td/TdJsonReader.cs`, `Td/PtrClientJson.cs` and `Common/ArrayPoolBufferWriter.cs` with
`TD_POINTER_PARSER`, plus stubs for `ClientResultHandler`, `PageBlockHelper` and
`RichTextTonAddress` (the generated file in `obj` predates it). It should move into the repo
as a test project once there is a home for one.

Known gaps in step 1, to close later: limits validation, markdown input rules, and clipboard.

**Tables done (2026-10-12).** `RichEditorEngine.Table.cs`: `RichTableGrid` holds a slot per
grid position pointing at its covering cell, and every op edits slots and rebuilds TDLib's
rows (each cell once, in its start row, spans measured). Insert/delete row and column,
merge, split, header toggle, horizontal and vertical alignment, and the bordered/compact
style. Tests cover spans across inserts. That caught a real bug: `Normalize` removed rows
with no cells of their own, which a cell spanning down from above legitimately leaves.

**Step 2 done, unbuilt (2026-10-12).** The view compiles against the Modern
`bin/.../Telegram.dll` (scratchpad `view/` harness: SDK project with `UseUwp`, CsWinRT
2.3.1, references to that dll plus Win2D interop and MUXC). The only errors left are
expected: strings added in this change, and `RichTextTonAddress`, which is newer than that
dll. Files:
- `RichLeafView` (templated; every text's look is a visual state in `RichEditor.xaml`):
  feeds `DirectTextBlock` the leaf's own string, so editor and layout offsets are identical.
  An empty leaf, or one ending in `\n`, is drawn with a trailing U+200B so it has a line.
  Spoilers stay visible while editing; link colours are read back from `HyperlinkForeground`
  rather than looked up.
- `RichBlockPresenters`: one templated control per block family, reused by object identity
  across structural changes (`_presenters` / `_itemPresenters` / `_cellPresenters`, pruned
  after each realize). `IsRealizing` stops a template applied mid-pass from updating twice.
- `RichEditorView` (+`.Input`, `.TextInput`): caret = a `Rectangle` placed from the new
  native `DirectTextLayout.Caret(offset)` (DirectWrite `HitTestTextPosition`; exposed as
  `DirectTextBlock.GetCaretRectangle`), with a composition opacity blink at the system rate.
  Mouse: click counting at the system double-click time, word and line drag, auto-scroll,
  I-beam cursor via `CoreWindow.PointerCursor`. Touch: tap places the caret (no capture, so
  panning scrolls), long press selects a word and raises the menu, grippers drag the ends.
  Keyboard: everything in `HandleKey`, including Up/Down by hit-testing at a remembered
  goal x, Home/End by line, Tab for list indent and table cells (otherwise focus moves on),
  Alt+Up/Down to move a block, Ctrl+Alt+0–6 for text kinds. IME: `CoreTextEditContext`
  over the focused leaf only. A change made by keys or the mouse is reported as the whole
  leaf replaced, and moving to another leaf is a focus leave plus enter. The composition
  underline is drawn in the decorations canvas.
- `RichEditorAutomationPeer`: Edit control type with `ITextProvider2` over the flat
  document (leaves joined by `\n`, objects one position each that read as their text).
  Units are character, word, line (from the layout's line lengths), paragraph (one per
  leaf), and document. It raises the text-changed and selection-changed events.
- `RichMediaRenderer`: stills only (`ImageView.SetSource` for remote media,
  `GetMapThumbnailFile` for maps, `UriEx.ToBitmap` for local files), plus a kind/duration
  badge. A collage shows its first item and a count; the mosaic layout is still to do
  (done since: collages are tiled with the album's mosaic layout, see below).
- Native: `DirectTextLayout.Caret` (idl/h/cpp). Checked with `/Zs` against the real headers,
  and the check was proven live with a planted error.

**Step 3 done, unbuilt (2026-10-12).** `RichTextWindow` hosts `RichEditorView` in place of
the WebView2 and its `CustomEmojiCanvas`. The toolbars are unchanged and read
`RichEditorStatus`. Every flyout drives the engine directly: paragraph kinds, lists with
indent/outdent, the full table menu, formulas (inline in cells and captions, a block
elsewhere), links through `CreateLinkPopup`, dates through `ChooseDateTimeToast`, and emoji.
The Premium lock is recomputed 500ms after typing stops, not on every key. Send trims the
empty paragraphs at either end. Both csproj files are covered (`Telegram.csproj` has
explicit entries, Modern globs), and `Generic.xaml` merges `RichEditor.xaml`.
`RichEditorState.cs` / `RichEditorCommands.cs` and the JS library are now unused, and are
kept until Fela decides (step 8). The architecture map has no editor section yet; add one
with the commit.

Strings added (`Resources.xml`): `ArticleTableTitleHint`, `ArticleTableBordered`,
`ArticleTableCompact`, `AccDescrCollage`, the `AccDescrIV*` set (all with Android's wording),
and `ArticleAlign{Left,Center,Right,Top,Middle,Bottom}` (new; Android has none). Move
up/down reuses Android's `LinkAbove`/`LinkBelow`.

Build feedback 2026-10-12: WMC0047 (a visual state and an element shared an `x:Name` in
`RichAtomPresenter`; the elements are now `DividerLine`/`AnchorChip`/`MathImage`). CS0104:
the model's `RichTextBlock` clashed with XAML's and is now `RichParagraphBlock`.

**Step 4 done, unbuilt (2026-10-12).**
- `RichMediaFactory`: `IStorageItem`s or a `StorageMedia` → `MessageFactory` (same
  compression and conversion as the composer) → `RichMediaSource.FromLocal`, which keeps
  the `StorageMedia` (`Local`, for `EditMediaPopup`) and a preview `ImageSource`.
- `RichEditorEngine.Media`: `SetSpoiler`, `SetSource` (replace/edit), `Group` with the
  neighbouring photo/video/collage (captions move under the group), `Ungroup`,
  `SetSlideshow`, `AddToGroup`, `RemoveFromGroup`, `SetButtons` (an emptied row is
  deleted), `CountMedia`.
- Collages are tiled with `MosaicAlbumLayout.chatMessageBubbleMosaicLayout`, the chat
  album's layout. Media under a spoiler is dimmed with a spoiler glyph, not hidden.
- `RichButtonPopup` (Views/Popups, modelled on `CreateLinkPopup`): Link / Copy / User
  profile (`ChooseChatsPopup.PickUserAsync`), label, and a colour that maps to the
  default/primary/danger/success styles. Delete appears only when editing. Used for row
  buttons and for inline buttons (formatting menu → "Button"). Clicking an inline button in
  the text opens it, found in its leaf by object identity.
- Window: an Attach menu (image/video, file, audio, map via `SendLocationPopup`, add button,
  divider, toggle block) and node menus. Media: edit (local only), replace, spoiler,
  combine into collage, author. Collage: collage/slideshow, add media, ungroup. Map: change
  location. Button row: edit each button, add, alignment. Formula: edit. Paste and drop of
  files and bitmaps go to media; with `plain`, paste takes the text. The
  media count is checked against `rich_message_media_count_max` before insertion.
- Strings: the `RichEditor*` button set, `ChatLink`, `ColorBlue`/`ColorRed`/`ColorGreen`,
  `StoriesCollage`, `CommunityUngroupChatsButton` (all Android), plus new
  `RichEditorButtonColor` and `ArticleLimitMedia`.
- Not yet (2026-10-13: reordering a collage's items is in its context menu, and slideshow paging
  is there): a
  better look for audio and file blocks than a badge, and media upload progress (TDLib
  uploads on send, so the chat shows progress, not the editor).

Fela's feedback after the first run (2026-10-12), all fixed:
- The caret and every overlay were shifted by the root's padding: the canvases were children
  of a padded `Grid` but positioned in its coordinates. All layers now sit in a `Surface` grid
  that owns the padding as a margin, and every coordinate is measured from it. Pointer input
  is still caught on the outer root. Scroll maths adds `Padding.Top` back.
- Duplicate `OnPasteRequested`: the step-3 placeholder was left beside the step-4 handler.
- The selection was the text colour. It is now `RichEditorSelectionBrush`: ThemeDictionaries
  in `RichEditor.xaml`, accent Light2 in light, Dark1 in dark, the highlight colour in high
  contrast. Proper theming is still TODO.
- Audio now uses the composer's `SendAudiosPopup`: picked tracks go in by file id, and
  browsing falls through to the file picker, as in `ComposeViewModel`.
- Clearing a leaf left its last character drawn. This was a `DirectTextBlock` bug: `Render`
  returned early for a size under 1 DIP, the zero-width line holder an empty leaf draws,
  and left the old surface on the brush. It now hides the visual in that case.

**Step 5 done, unbuilt (2026-10-12).**
- `RichEditorEngine.Clipboard`: `GetSelectionFragment` slices a clone, clearing leaves
  outside the range, trimming the two ends and pruning blocks wholly outside. A container
  only partly selected stays, so a quote stays a quote and a table keeps its shape.
  `InsertFragment`: one plain paragraph goes inline with marks and objects. Otherwise the
  line splits and the first and last pasted paragraphs join its halves. Into a cell, a
  caption or code, only text goes.
- `RichClipboard`: an in-process store (fragment plus a token in
  `application/x-unigram-rich-message`, as tdesktop and Android do; media cannot go
  through the clipboard); `ToFormattedText` (one line per leaf with message entities, pre
  for code blocks, merged blockquotes, custom emoji with their alt text); `ToHtml`; and a
  tolerant `FromHtml` (block tags, lists including checkboxes, tables with spans, inline
  styles, Word's span styles; comments, style and script skipped).
- `MessageHelper.SetTextAsync(DataPackage, FormattedText)` is split out of `CopyText`, whose
  behaviour is unchanged. Copy writes plain text, the chat's `x-tl-field-tags` (so a paste
  into the composer keeps its styles), HTML and the token.
- Paste priority: own document, then media (unless plain), then `x-tl-field-tags`, then
  HTML, then text.
- Tests: 138, all passing (fragment slicing, pasting with joins, cell paste, entity ranges,
  HTML out exactly, HTML in including nested quote-in-list and paragraph-in-cell).

Not done in step 5: markdown in (Android offers it as an option after paste), and dragging
a selection out of the editor.

Second round of feedback (2026-10-12), fixed:
- Clearing a leaf still left its last characters drawn. The first fix covered the wrong case:
  an empty leaf is laid out a full line tall, so it never hit the zero-size path. The native
  `Render` returns null when the extent has no width, which a zero-width line holder does,
  and `DirectTextBlock` kept the old surface. It now hides the visual on a null surface.
- Combined styles rendered plain. `DirectTextLayout` matches each part's `Type` against a
  single `TextStyle` (`==`, not a flag test), so a part with bold and italic OR-ed together
  matched nothing. `RichLeafView` now emits one part per style, merged across neighbouring
  runs. The layout itself only supports bold, italic, underline, strikethrough and monospace.
- Spoilers and highlights showed nothing while editing. The layout cannot draw either, so
  the view now paints them as rounded rectangles in the decorations layer under the text
  (`RichEditorSpoilerBrush`, `RichEditorMarkedBrush`), read from swatch elements in the
  template. The pass skips leaves without such runs before asking the layout anything, and
  reruns when the arranged width changes.
- Sub/superscript: drawn in the editor too (confirmed 2026-10-13).

**Step 6 done, unbuilt (2026-10-12).**
- Auto-scroll is driven by a timer while a mouse drag or a handle is held near an edge. It
  measures the point in the viewport, so a still finger keeps scrolling and extending.
- Handle positions add the scroll since the drag began. The handles are 40×40 targets with a
  16px dot hanging below the line, and `Raw` for automation.
- The touch menu opens above the selection (`ShowMenu(..., touch: true)`), clear of the finger.
- The touch keyboard: `InputPane.Showing` takes the covered part out of the scroll viewport
  (a bottom margin on the `ScrollViewer`) and brings the caret into view. The handlers are
  unsubscribed on unload, because the pane outlives the control.
- Every `+= OnX` in the editor and the window is paired with a `-=`, checked with a
  script; the only exceptions are the control's own Loaded/Unloaded.

**Step 7 done, unbuilt (2026-10-12).**
- The text pattern reports attributes per range (`GetAttribute`): `StyleId` (Heading1–6,
  Quote, BulletedList, NumberedList, Normal), font weight, italic, underline, strikethrough,
  sub/superscript, bullet style, centred alignment for pull quotes, and read-only false.
  `FindAttribute` walks format units, so readers can jump by heading. The XAML
  `AutomationStyleId` enum ends at `BulletedList`; `NumberedList` is UIA's raw 70016,
  checked by reflecting the projection rather than assumed.
- The Format unit is a run, not a word.
- Blocks selected whole are children of the text: `GetChildren` returns the peers of the
  blocks between a range's lines, and `RangeFromChild` maps one back to its newline.
  `RichBlockAutomationPeer` gives them control types (Image, Separator, Group) and names
  from `RichEditorView.Describe`: the kind, spoiler, caption, button labels and formula source.
- Text reaches automation once: `RichLeafView`, its `DirectTextBlock` and the placeholder
  are `Raw`, and so are the presenters that are only layout.
- Announcements (`RaiseNotificationEvent`): arriving on a selected block, and a command
  changing the kind of the current line without moving the caret (Ctrl+Alt+2 says
  "Heading 2").
- Ctrl+Shift+Enter toggles a checklist box or a collapsible block from the keyboard, and
  says the new state. Checkboxes are named after their item.
- Every window toolbar button has a name and a tooltip (19 buttons).
- Strings added: `AccDescrChecked`, `AccDescrNotChecked`, `Subscript`, `Superscript`, all
  from Android.

**Step 8 done, unbuilt (2026-10-12).**
- `RichDocumentLimits`: the document measured the way `inputRichMessage` documents its
  limits. Text includes custom emoji alt text, formula sources, anchor names and button
  labels. Blocks count `AllBlocks`, list items and table rows. Depth is block nesting (a
  list counts as two: the list and its item) plus the depth of the `RichText` each leaf
  serializes to. Media and table columns are counted too. The window refuses to send over a
  limit, with a toast naming it (`ArticleLimit*`, new strings, since Android has none), and
  dims the send button on the same 500ms recheck as the Premium lock. Adding a table column
  past the limit is refused up front. Saving a draft is never refused.
- `RichEditorEngine.Rules`: markdown at the start of a plain top-level paragraph, applied
  on the space after it. `#`–`######` make a heading; `-`/`*`/`+`/`•` a bullet list;
  `N.`/`N)` a list numbered from N; `[]`/`[ ]`/`[x]` a checklist; `>` a quote (not inside one);
  ` ```lang ` code. On Enter, `---`/`***`/`___` make a divider and ` ```lang ` a code block. A
  rule is one undo step: `_suppressUndo` stops the operations inside it from each taking a
  snapshot. Backspace straight after a rule is Undo. The IME path notices the structural
  change and re-syncs the input service instead of applying its stale selection.
- Drafts needed nothing: TDLib hands a rich draft back as `DraftMessageContentRichMessage`, a
  display `RichMessage`, which `FromRichMessage` already loads.
- Editing a long message: `EditRichMessage` now fetches the whole message with
  `GetFullRichMessage` when `IsFull` is false. Before, it opened only the shown part, and
  saving would have cut the message.
- Tests: 161, all passing.

**After step 8 (2026-10-12), Fela's picks:**
- **WebView editor deleted.** `Libraries/unigram-iv-editor` (19 tracked files plus
  node_modules), `RichEditorState.cs`, `RichEditorCommands.cs`, the `editor.html` Content items
  in all three csproj files, and its `.gitattributes` block. Docs updated: the
  Build-instructions section, the architecture map lines, package gate 6 (retired), the
  Benchmarks README, and `RichHtml`'s summary. Nothing is staged.
- **One HTML reader.** `Common/RichHtml.cs` already parsed Android's rich clipboard format
  and browser HTML for the composer. The editor's own reader is gone, and
  `RichClipboard.FromHtml` is `RichHtml.Parse` + `FromPageBlocks`. `ToHtml` now writes
  RichHtml's markup, stamped with `data-telegram-rich`, so the composer recognises a copy
  from the editor as rich. `RichHtml.InlineSpan` learned span styles (font-weight,
  font-style, text-decoration), which fixes bold pasted from Word and Docs for the composer
  too. Blocks inside an inline context - paragraphs in a table cell - are now lines of their
  own (`RichHtml.InlineChildren`), and the markup whitespace between them is dropped.
- **Sub/superscript drawn.** `DirectTextLayout` lays the range out at `ScriptScale` (0.7), so
  advances, hit testing and the caret are right, then draws it transparent. `DrawScript`
  redraws it per line from small layouts, offset from that line's baseline (up a third, down
  a sixth). Checked with `/Zs`, and the check proven live with a planted error. This also
  reaches the read view, whose `StyledText` already emitted these parts.
- **Collage items.** Tiles register in `_itemViews` and are hit-tested before their
  collage, so a click selects the photo with its own outline. Arrows walk between items and
  past the ends out of the collage. Alt+Left/Right reorders, Delete removes (the collage goes
  with its last item), and "Ungroup" on an item (`ExtractFromGroup`) moves it after the
  collage. A slideshow pages with previous/next arrows, and its badge shows "n/N".
  `StopOf` gives an item its collage's stop, fixing `PositionBefore`/`After` indexing past
  the stop list.
- **Audio and file blocks** are a row: an icon, a title (track title, file name), and a
  subtitle (performer, size, length). Remote, local `StorageMedia` and music-picker
  `InputAudio` sources are all handled. The renderer takes a `RichMediaParts` bag
  instead of a growing argument list.
- Tests: 174, all passing.

Table cells (Fela: "I can't select table cells"). A cell's leaf was hit-tested by its text's
own bounds, which are left-aligned, as wide as the text, and nearly zero wide when the cell is
empty. Clicks in a cell's empty part went to the nearest text, often the next cell's. Leaves now
have a `HitTarget` (the cell presenter) to measure against, and the position is still taken
inside the text. A selection from one cell into another selects cells
(`GetSelectedCells`: the rectangle, merged cells whole), drawn by a `SelectionFill` layer in
the cell template rather than as text highlight.

Up and Down (Fela: Down left a table instead of going to the cell below, and Up from under one
jumped above it). A probe a pixel past the line can land in the gap between blocks or in a
cell's padding, and then belongs to the line it left. `MoveLineOnce` now searches: within the
current text, the next line; past it, the nearest leaf or whole-selected block lying entirely
beyond the line, ties broken by distance from the remembered column.

**Polish and design pass (2026-10-12).**
- Blocks look as `PageBlockRenderer` draws them. Spacing is its `UpdateSpacing` rule, applied
  by `RichEditorView.Realize(blocks, index, root)`: 8 between blocks, none between paragraphs,
  12 above and 4 below a divider, 4 between button rows, 10 side padding at the root except
  for photos, videos, collages and maps. The editor's own padding is 10 less, so text stays
  where it was. Code and block quotes use `BlockQuote`'s chrome: header tint, 3px accent bar,
  corner glyph, padding 8,4,24,6. A pull quote is centred on a 10% tint between quote marks.
  An expandable quote is a block quote with `IsCollapsed` (see "Collapsible quotes" below).
  Details blocks look like the message's `DetailsBlock`: a turning 16px chevron, its
  `DetailsBlockHeader` with no content, beside the header text, which has to take the caret,
  and a `MessageHeaderBackgroundBrush` line at the end. Credits are SemiBold. Captions
  and cells are body text. Table cells draw their own collapsed borders and outer corners;
  bordered, striped and compact are now drawn. A table caption goes above the table, and is
  first in the stops so arrows and the text pattern read it where it is. Files are a
  `DocumentContent`-style row, maps fit 320×200, and buttons use the filled
  `ReplyMarkupInlineButton` styles in equal columns.
- Re-renders do less. A media block keeps what it last showed (source; a collage's items and
  page; a map's location and size) and skips `SetSource` and the map request when nothing
  changed. Its tiles are registered again without being rebuilt. Button rows rebuild only
  when their buttons or alignment change. Table layout reuses one occupancy array. `Bounds`
  caches element rectangles until the next `LayoutUpdated`, since a drag asks for every
  leaf's on every move.
- Kept as they are: presenters' `GoToState` calls. They pass literals, and moving to the
  state already current costs almost nothing.
- Tests: 178, all passing. The harness numbers leaves without table captions, as it always had.

Media controls (Fela: no way to find the collage/slideshow switch). Pictures carry TDesktop's
overlay: a 27px "…" 6px in from the top left, which selects the item and opens the same menu
as a right click. A collage has one per tile, placed by the renderer, and hidden on tiles under
64px; a slideshow has one for the current slide. Top right are "+" and, on a group, the layout
switch, which shows the layout it switches to. "+" on a lone photo makes a collage of it and
what is picked (`GroupWith`). The menus have one switch item, on the group and on its items.
The view ignores taps, double taps and holds that end on a button, which it used to act on a
second time. The glyphs are stand-ins (`Icons.Apps` for collage, `Icons.Image` for
slideshow): the font has neither of TDesktop's `to_collage`/`to_slideshow`. Tests: 183.

Media sizing. `RichMediaFrame` (the media box) shows at the size the renderer gives it, or
narrower at the same ratio when the column is; `RichCollagePanel` lays the tiles, and their "…",
out of the album mosaic scaled to that width. Fixed Width/Height and canvas offsets had clipped
the pictures while the selection and the controls went by the unclipped box. A collage or
slideshow left with one item becomes that item in `Normalize`, with the group's caption.

Popups, after TDesktop's `EditMathBox` and `EditRichButtonBox`: a formula has its LaTeX, a
"Separate Line" checkbox and a result card with a dimmed sample while empty; a button has its
text, the checkbox, its colour as four previews (radio buttons, so one choice to the keyboard
and a screen reader), the action and its value or user. The checkbox is offered on insert where
both can go; a selected block takes only a block, a cell or caption only text. Editing does not
offer it: turning a formula or button between inline and block is not in the engine. Menus
opened by pointer are transient so the caret stays; from the keyboard they take focus.

Toolbar, after TDesktop's UX in the app's own look (Fela: "the app has to keep its identity").
One top bar: history left; Block style, Text style, List, Table, Link, Attach, Formula in the
middle (scrolling when narrow); emoji right; Send alone at the bottom. No pill over the
selection: marks are in the Text style menu, and in the context menu's submenu built by the
same method. Divider went to Block style, Button and Date to Link, the toggle block to List.
Premium is marked, not enforced, as there: a gradient star (`E9B5`) on List, Table, Attach and
Formula, and `MenuFlyoutPremiumItem`/`ToggleMenuFlyoutPremiumItem` (copies of the default item
templates with a star column) for headings, pullquote, footer, divider, sub/superscript,
marked text, list inserts and every attach item. Sending is what still asks. TDesktop's "/" menu,
its Discard/Cancel pill and list numbering types are left out on purpose.

(Done in 8a646ca2e.) The `notes/architecture.md` section for the editor.

IME. A composition is formatted a range at a time, a clause each for the IMEs that convert
clause by clause, so `FormatUpdating` keeps a segment per range (underline style and colour,
background) and they are drawn together; it used to keep one range and redraw only that, which
left only the last clause marked. A format follows the update that typed the text, before
layout, so drawing waits for `LayoutUpdated`. The IME's colours are system element types, read
from `UISettings` into brushes made in code - the one place the editor does that, because they
are the IME's, not the theme's. The text colour it asks for is not applied. The input scope is
per leaf: `Default` for code and with the system spell checker turned off, `Text` otherwise,
set before every focus enter.

Spelling. `Telegram.Native.SpellChecker` wraps the Windows Spell Checking API, allowed in Store
apps (`spellcheck.h` puts it in the app partition). Its factory is registered
`ThreadingModel = Free`, so the checker is made and called on the thread pool only.
`RichEditorView.SpellCheck.cs` checks when a word ends, as the system text box does, not on a
timer: an edit that types or deletes a separator, the caret moving after an edit, or any
structural change. Each result keeps the text it is for; an edit is the diff against it, so
squiggles before the edit stay, those after shift, and only the word being edited loses its own.
A result that arrives after more typing is moved the same way rather than thrown away. It draws a
squiggle per word on the decorations layer (`SpellingSwatch`, critical fill).
The language is the keyboard's when Windows has a dictionary for it, else the first of the user's
languages that does. Links, mentions, inline objects and monospace are not checked; neither is
code; a word typed twice is not flagged (it has nothing to suggest). The right-click menu opens
with up to five suggestions, Add to Dictionary and Ignore word, awaited before the menu shows -
which is why a menu's keyboard origin now travels on `RichMenuRequestedEventArgs.IsKeyboard`
rather than in a field set around the event. All of it follows `AppSettings.UseSystemSpellChecker`.

Collapsible quotes follow TDesktop. A block quote of plain paragraphs only (`IsCollapsible`)
shows a chevron at its bottom right once its text runs past three lines, and always while it is
collapsed. Collapsing joins its paragraphs with line breaks, since `inputPageBlockExpandableBlockQuote`
holds one text. The collapsed quote is then atomic: one stop for the quote, then its credit,
which also keeps a document of only that quote with a leaf. Its text is clipped by
`RichLeafView.MaxLines` and not registered with the view, because `BindLeaf` registers only
leaves that are in the index. Collapsed, it is sent as the expandable quote and read back as
one; open, it is a plain block quote. The old expandable `RichTextQuote` is gone, and
`RichTextQuote` is the pull quote only. To open or close it: the chevron, double click, or
Ctrl+Shift+Enter (the toggle-block key).

A tap below the last block (`OnScrollViewerTapped`; the scroll viewer is transparent so the
space takes it) does what Down from the end does: `AppendLine` when the last block is not text,
otherwise Ctrl+End.

(Superseded.) **Left, waiting for Fela:** deleting the WebView editor (`Libraries/unigram-iv-editor`, the
`editor.html` Content link, `RichEditorState.cs`, `RichEditorCommands.cs`); and the
`notes/architecture.md` section, which goes in with the commit. WebView2 itself stays: web
apps use it.

**Known gaps:**
- Markdown paste is not supported, and a selection cannot be dragged out.
- Theming is placeholder: the selection, spoiler and marked brushes.
- RichHtml joins a `<p>` inside a table cell without a break.
- A caption's first character does not respace a last block; the next structural change does.

(Superseded plan line follows.) Next: step 7, accessibility (a peer per block, with names from `AccDescrIV*`, and
announcements on structural changes). Then step 8 (limits validation on send, markdown input
rules, loading drafts, deleting the WebView editor).

(Superseded plan line follows.) Next: step 6, touch (a `CommandBarFlyout` for the touch menu, gripper polish, scrolling while
dragging a gripper). Then step 7, accessibility (a peer per block, with names from
`AccDescrIV*`, and announcements on structural changes), and step 8 (limits validation on
send, markdown input rules, loading drafts, deleting the WebView editor).

(Superseded plan line follows.) Step 5 — clipboard. An internal format that keeps blocks and media; `FormattedText`
for the chat's own clipboard format; HTML out; HTML and markdown in. Then step 6 polish
(touch menu as a `CommandBarFlyout`), step 7 (peers per block, announcements), and step 8
(limits validation on send, markdown input rules, draft load, deleting the WebView editor).

(Superseded plan line follows.) Step 4 — the Attach menu (photo/video picker into `StorageMedia` → `MessageFactory`
→ `RichMediaSource.FromInput`), media actions (spoiler, replace, edit through
`EditMediaPopup`, group/ungroup, collage ↔ slideshow), the mosaic collage, the button editor
(Url/User/CopyText; label, style), inline buttons, maps (location picker), and paste/drop of
images.

Tables in a selection (2026-10-13). A selection with one end in a table and the other outside it
takes the table whole, as Word does: `CoverTables` in `Select` moves the end inside to the table's
far edge, so a drag, Shift+click or Ctrl+A covers it from caption to last cell, and delete and copy
take it whole (`Covers`, shared with `DeleteRange`). Extending back into it with the keyboard lets
it go instead (`ReleaseTable`), or Shift+Up would land on the far edge again every time. A covered
table is drawn as selected cells, not as text (`GetCoveredTables`). A selection inside one table is
unchanged.

Links were drawn in the text colour: the leaf view read the block's `HyperlinkForeground` once, at
render, and the block's implicit style can reach it later. `DirectTextBlock.HyperlinkColorChanged`
now tells the leaf view to colour them again, which a theme change needs as well.

"Highlight" is "Marked" (`RichEditorMarked`), with Fluent's highlight_20_regular at F79A.

TODO: markdown on paste, which Android offers as an option after a paste.

Grouped toolbar (2026-10-13). The toolbar's middle is a `RichToolbarHost` around a scroll viewer
of groups, as Word's ribbon has them: Formatting, Text style, List and Insert, with separators.
Only the host sees the column's width, since the scroll viewer measures the groups unbounded.
Each group is either expanded (two rows of small buttons over its label) or folded into one
button with its glyph and label, which opens the group's menu. Groups expand in
`RichToolbarGroup.Priority` order while they fit: Text style, then Formatting, List, Insert.
The order is strict: once one does not fit the rest fold, so widening the window never folds a
group back.
When even every group folded does not fit, the bar scrolls. A first version also had TDesktop's
one-row compact layout below that width; it was dropped. Every command of the menus has a
button, so no group needs a "more" button. What is not shown is collapsed, not clipped, which
keeps it out of the tab order and the automation tree. A group therefore only has a width in
the state it is in, and the host remembers the other from when it was last shown (unknown at
first, stale if the content changed meanwhile). It applies a choice and measures again inside the
same measure pass until the choice was made on real widths, so nothing wrong is ever drawn.
Widths are the whole group's, its template padding and margin included.
The bar is one tab stop with arrow keys inside it. The grouped buttons are named and run typed
handlers: the marks and the list styles through arrays pairing each button with its value. The
Premium stars bind to `PremiumMarksVisibility`. The small buttons' look is in the window's
resources (`GroupToggleButtonStyle`, `GroupGlyphButtonStyle`, `GroupFoldedButtonStyle`,
`GroupLabelStyle`, `GroupSeparatorStyle`), the place to restyle it.
New glyphs: indent, outdent and clear formatting at F79B-F79D, button at F79E.

Marked text in bubbles drawn by `DirectTextBlock` had no background: the layout has none to give
a range. It is now a shape under the glyphs, as the search match is, in `FormattedTextBlock`'s
PaleGoldenrod. The editor is unaffected: its leaves never emit `Marked` parts and draw their own.

Right to left (2026-10-14): each line runs the way its first strong character does
(`NativeUtils.GetDirectionality` in `RichLeafView.Render`, handed to `DirectTextBlock.SetText`); a
line with none follows the editor's `FlowDirection`. An article's direction is its first strong
line's (`RichEditorView.UpdateDocumentDirection`): it sets `RichDocument.IsRtl`, which is sent,
and mirrors the editor as an instant view mirrors. A message's text keeps the app's direction.
The leaves' text blocks, formulas and media frames are pinned left to right, since they draw into
surfaces that a mirrored parent would show mirrored; `RichLeafView` converts across that one flip,
so everything else, which shares the leaf view's direction, reads its coordinates straight. The
arrows ask the line (`DirectTextBlock.IsRightToLeft`), and Home and End go to the line's logical
start and end (`RichLeafView.GetLineEdge`), which they did not for an RTL line before.

Media uploads while the article is written (2026-10-14). `RichMediaUploads`, which
`RichTextWindow` gives the editor, starts TDLib's `preliminaryUploadFile` for a source the first
time the renderer shows it, which covers inserting, pasting, dropping and a draft loaded with local
files alike. Progress comes through `UpdateManager` with the editor as the subscriber, so it lands
on the UI thread; a picture or a collage shows one ring, its uploads' bytes added up, and a file
shows one on its icon. A structural change stops the uploads of sources no longer in the document
(`AllBlocks`). On send, `RichDocumentConverter.ToInputRichMessage` asks the uploads
(`IRichMediaInputs`) what each source goes as, and one uploading goes as a copy of its input
naming `InputFileId`: the source itself is untouched, because undo steps share it and a draft,
which is saved without the uploads, has to name the file on the device. The window detaches the
uploads when a rich message went out and cancels them otherwise, on close.
