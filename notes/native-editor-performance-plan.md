# Typing at any length in the native editor

A plan (2026-10-15); the meter is built and has measured, phase 2 is designed. The native editor (`Controls/RichEditor/`) is to become the
app's main text editor: the composer, every message box, and `RichTextWindow`, whose main use is
long text. Typing a long text already starts to lag. This is what it takes for a keystroke to cost
the same in a 100,000 character article as in a one-line message.

## The target

- A keystroke costs what the edit changed: the edit, and the lines it moves.
- A frame and a scroll cost what is on screen.
- Opening a document, and the memory it holds, grow with what is on screen and a compact model,
  not with every paragraph having its XAML, a layout and a surface.

## What a keystroke costs today

Traced through the code (2026-10-15); the meter's numbers are under State.

- **The engine.** `RichEditorEngine.ReplaceText` edits one leaf. `BeginEdit` snapshots the whole
  document (`Capture()`) for every new undo step; typing coalesces for a second
  (`TypingCoalesceMilliseconds`), so that is once per pause. Positions are found by walking
  every leaf: `RichFormattedTextConverter.GetOffset` and `GetLength`, and the automation peer's
  `Length` and `ToFlat`.
- **The paragraph.** `RichEditorView.Refresh` renders only the edited leaf, but whole:
  `RichLeafView.Render` builds its style parts again, `DirectTextBlock.SetText` has the native
  layout build a new `IDWriteTextLayout` for the whole paragraph, and `DirectTextLayout.Render`
  clears and redraws the whole surface. DirectWrite has no incremental layout. A message is a
  paragraph per line today, so the cost is a paragraph's length, and a long paragraph, typed
  without Enter, pays it in full on every key.
- **The view.** `Blocks` is a `StackPanel` (`RichEditor.xaml`): every block of the document has its
  presenter, its layout and its surface, in view or not. Vertical caret movement asks the views
  (`MoveLineOnce` walks `_leafViews`).
- **The input service.** After a change it did not make, `SyncEditContext` reports the whole leaf
  replaced (`NotifyTextChanged` over 0 to its old length), and the service reads it all again.
- **The pages.** `MessageTextBox` counts the whole document for `MaxLength` on every change. In
  ChatView, `CheckMessageBoxEmpty` reads `TextField.Text`, which on the native box is
  `RichFormattedTextConverter.ToFormattedText` of the whole message, scans it for a URL and, if
  it may hold one, asks TDLib for a link preview with the whole text, all on every change.
  `RichTextWindow` asks the engine for its status (`GetStatus`) on every change for the toolbar,
  and converts the whole document for the send lock, throttled by a timer, for free users.
- **Already fine.** Spell checking runs off the UI thread, one check at a time. Automation events
  are raised only while something listens. Suggestions look back from the caret inside its line.
  The surface of an edited line is no longer replaced as it grows (`IsReadOnly`, de0829c28).

## How editors keep typing cheap

- A key does the least it can synchronously: the model edit, a layout of what changed, a paint.
  Spell checking, link detection, suggestions, accessibility, counts and drafts wait for idle or
  a short debounce.
- Layout is cached by visual line. An edit in line k is laid out again from line k - 1, since a
  deletion can pull a word back up, and stops at the first line that breaks where it did before:
  every line after it is the same line, moved.
- Only dirty lines are painted, and only lines in view are drawn at all. A text out of view is
  an estimated height until it scrolls in.
- What is derived from the text - its length, whether it is empty, where an offset is - is kept up
  to date by each edit, not computed again from the text.

## Is DirectWrite the right engine

Yes, though not the layer of it in use today.

- **`IDWriteTextLayout`**, which `DirectTextLayout` builds a paragraph at a time, is what has no
  incremental layout. Win2D's `CanvasTextLayout` wraps the same object.
- **`IDWriteTextAnalyzer`** is the layer under it: script itemization, bidi levels, line break
  opportunities, shaping (`GetGlyphs`, `GetGlyphPlacements`), with glyph runs drawn through D2D.
  An editor built on it shapes a run once, keeps the advances, and breaks lines itself, so an
  edit reshapes a word and reflows from cached widths. RichEdit and Word do this through their
  own line layout library, which is not public.
- **HarfBuzz and ICU** (the Skia paragraph stack) would replace Windows' font fallback, its colour
  emoji and the font collections the app already configures (`FontChains`), add large
  dependencies, and still draw through DirectWrite in the end. Nothing in it is worth that on
  Windows.
- **RichEdit** is what this replaces.

So DirectWrite stays, and edited text is laid out on `IDWriteTextAnalyzer` (decided 2026-10-15).
A window of `IDWriteTextLayout` lines was the cheaper start, and it was passed over: it would still
shape every window from scratch, could not keep a shaped word across edits, and leaves the line
breaking and the drawing to an object that cannot be told what changed. The design is phase 2,
below.

## The design, by layer

### Model

- **Undo by operations.** An edit records itself and its inverse, in place of snapshotting the
  document per undo step. The snapshot stays possible for whole-document operations (load, paste
  of a document).
- **A position index.** Leaf lengths in a prefix-sum tree updated by each edit, for flat offsets
  in O(log n): the converter, the automation peer, the caret offset callers ask for. The length
  the box limits and the emptiness callers ask about become running values.
- **Text inside a leaf** stays a string until measurement says otherwise; a gap buffer or rope
  in `RichTextLeaf` is the change if it does.

### Layout and painting

Phase 2, designed in full below: edited text laid out on the analyzer, shaped in chunks that an
edit replaces, lines broken by us and broken again only until they agree with the old ones, and
drawn into a virtual surface a tile at a time, visible tiles only.

### The view (`RichEditorView`)

- Presenters realized only near the viewport, the rest standing as estimated heights in a height
  map that the real ones correct once measured: a virtualizing panel in place of the `StackPanel`,
  which keeps the scroll position still while an estimate is replaced.
- Caret movement, selection and hit testing answer from the model and the layout cache for
  blocks that are not realized, and realize the block they move into. Bringing the caret into
  view realizes ahead of it.
- Everything that walks `_leafViews` today (vertical movement, spelling draws, direction) is
  checked against that.

### Input and scheduling

- `SyncEditContext` reports the range that changed, not the whole leaf.
- A key edits, marks what is dirty, and leaves layout and paint to the frame. Debounced or left
  to idle: ChatView's link preview check, `RichTextWindow`'s toolbar status and send lock, the
  length limit, suggestions' detection where it is not needed at once, automation events,
  spell-check scheduling and draft saves.
- Callers that ask for the whole text per change (`Text`, `GetFormattedText`) are moved off it, or
  answered from a cache the next edit invalidates.

### Measuring, kept

- Benchmark documents: one paragraph of 100,000 characters, 2,000 short paragraphs, and an article
  with media, quotes, code and tables.
- A scripted typing replay that records the time of every keystroke and every frame, from
  `DirectTextLayout.Counters` and timers of our own around the engine, the view and the handlers.
- The engine's tests in a test project in the repo (they live in a scratch harness today); undo
  and positions are what this changes.

## Order

0. **The meter.** The benchmark documents, the replay, the timers. Each phase after it ships when
   the numbers move, and the numbers say how much of phases 2 and 3 messages need.
1. **Lines (phase 2).** The analyzer layout, the incremental update and the tiles. What a long
   paragraph needs, and the measured worst case; the largest native piece.
2. **Blocks (phase 3).** Virtualization and model-based navigation in `RichEditorView`. What many
   paragraphs need: their cost per key is the layout pass over every presenter, and opening them
   takes seconds.
3. **Cleanup (phase 1, measured to be no part of the lag).** Deferred page work, the running
   length and position index, the exact range to the input service, coalesced status and
   automation updates.
4. **The rest, if measured:** undo by operations, and a rope.

The phases keep their numbers; the order is the one the measurements gave.

Phases 2 and 3 are each larger than the whole message box migration was.

## State

**Phase 0, the meter (2026-10-15).** `RichEditorMeter` (`Controls/RichEditor/`), behind
Diagnostics, "Editor Meter": Ctrl+Alt+Shift+M in a native editor offers its documents (a message
box: one paragraph of 4,096 characters, 100 lines; the article editor: one paragraph of 100,000,
2,000 paragraphs, an article of 100,000 with headings, marks, quotes, code, lists and tables) and
"This Text". A run loads the document, times it opening, then types 100 keys in the middle of the
longest text and 100 at the end, one a frame, through `RichEditorView.TypeForMeasure`, the path
`OnTextUpdating` takes for the service. Per key: the change handlers, the view's share of them
(timed inside `OnEngineChanged`, before `StateChanged`), the rest (engine, box, page), the layout
and draw (`UpdateLayout`), and the frame. Then `DirectTextLayout.Counters`. The report is shown,
logged and appended to `LocalState\editor-meter.txt`; the editor's text is put back, its undo
history is not. Not measured: the input service itself, composition and the GPU, and media.

**Measured (2026-10-15, Fela's machine, 1.5x, a 912 DIP wide article editor and the composer).**
Medians per key; "view" includes layout and draw, because `UpdateSelection` forces a layout pass
(`Surface.UpdateLayout()`) inside the change handlers, which is why the meter's own layout column
reads zero.

| Run | Per key | Where it goes |
|---|---|---|
| One paragraph, 100,000 characters | 123 ms | `Build` 121 ms, once per key |
| One paragraph, 4,096 (composer) | 11 ms | `Build` 5.0 ms, `DrawTextLayout` 5.7 ms |
| 2,000 paragraphs | 10 ms, p95 78 ms, frames 31 ms | builds and draws 0.13 ms each: the layout pass over 2,000 realized presenters |
| Article, 100,000 | 7 ms, p95 frames 55 ms | as above, 424 blocks |
| 100 lines (composer) | 1.5 ms | fine |

- Opening: 2,000 paragraphs 1.9 s, the article 1.3 s, every block built, measured and drawn.
- The 100,000 character paragraph opens in 138 ms and is never drawn: no surface is created, as
  it would be about 40,000 pixels tall, past the largest a surface can be. The text is invisible.
- The engine, `MessageTextBox` and ChatView together cost about 0.1 ms in every run. The text had
  no URL, so the link preview path was not taken.
- `IsReadOnly` holds: 200 surfaces kept, none dropped, while typing.
- Rates: a build costs about 1.2 µs a character and a draw about 1.4, at 4,096 and at 100,000
  alike, and a draw draws every line, seen or not (the composer shows two of its seventy).
- Not explained: frames well past the handlers in the many-paragraph runs (p95 55 to 100 ms, and
  20 ms a frame outside them at 2,000). Spelling results, a collection, or XAML drawing 2,000
  elements; the meter does not split them yet.

**Phase 2a, written (2026-10-15), not yet built or run.** `EditableTextLayout`
(`Telegram.Native/EditableTextLayout.h/.cpp`) builds a paragraph cold as designed below: analysis,
items, `MapCharacters`, chunks of up to 256 characters ending where a word starts, shaping, greedy
lines, L1 and L2, and the queries. It draws its own glyph runs with colour layers, underline,
strikethrough and sub- and superscript, and formulas through their `InlineObject`.
`DirectTextLayout` delegates to it when `UseAnalyzer` is set and the text is edited, one implicit
paragraph without `MaxLines`; anything else, and a build that fails, is laid out as before.
`UseAnalyzer` comes from Diagnostics, "Analyzer Text Layout in Editors", read once a launch.
`DirectTextLayout.CompareWithAnalyzer` builds both layouts from the same state and reports the
build times, the box, line breaks, line metrics, every caret, 17 hit tests a line and the content
end. The meter offers it as "Compare Layouts in Every Script", a corpus of a paragraph per script
and direction, emoji, tabs, a word longer than the line, combining marks, the styles and a code
block with hard breaks, and "Compare Layouts in This Text". The comparison does not look at
pixels: what is drawn has to be checked by eye, the analyzer on and off.

**Phase 2b, written (2026-10-15), not yet built or run.** `EditableTextLayout::Update` works an
edit out from the layout it already holds: the common prefix and suffix of the old and new text,
styles and object boxes; the window from the run before the change to the end of the run after
it, analysed (script, breaks) and shaped again and spliced in, the runs and glyphs after it moved;
the lines broken again from the one before the window's until a line past the window ends where an
old one did, the old ones after it moved. `DirectTextLayout` keeps the layout `SetText` takes out
of use (`m_previous`) for the next build to update. Built from scratch instead: a right to left
paragraph, any level that is not the paragraph's, an edit that brings in a right to left character
or a directional control, another format (the locale included, so the first East Asian character),
an empty text, a width change (lines only), and any failure. `VerifyAnalyzer` checks every update
against a cold build (`EditableTextLayout::Matches`: break opportunities, clusters, fonts, glyphs,
lines and every caret; not the runs, which an update chunks differently, rightly), the count and
the last difference in `Counters`. The meter's "Type in This Text, Checking Every Update" types
with it on, every fifth key a deletion, and two more entries load the 4,096 and the 100,000
paragraph first.

**Proven (2026-10-15, Debug).** The comparison over the corpus: no line break, line metric, caret
(x, y, height), hit test or content end differs, in any script or direction. Two fixes got it
there: a caret is as tall as its run and starts at the run's ascent above the baseline, which is
what DirectWrite answers and which lowers it in a smaller font and in sub- and superscript; and an
update analyses a line of text on either side of its window, keeping only the window, because the
analyzer takes a range for the whole text and never breaks at its first position (184 of 200
updates differed before it, none after). Updates checked against cold builds: none differ, typing
from empty and in the 4,096 paragraph. That paragraph, against phase 0: an update 0.6 ms a key
against a build of 5.0, and a draw 1.2 ms against 5.7, both Debug, the cold build 13.6 ms against
DirectWrite's 5.0 - Debug weighs on our code, not on the system's, so the times to judge by are
Release ones. The 100,000 paragraph checks clean as well; its updates take 6.7 ms a key in Debug
against phase 0's 121 ms build, the cold build 357 ms.

**Measured in Release (2026-10-15, 240 Hz).** Per key, median, against phase 0:

| Run | Phase 0 | Now | Of which |
|---|---|---|---|
| One paragraph, 4,096 | 11 ms | 1.2 ms | update 0.12 ms, draw 0.72 ms: the 1 ms target met |
| One paragraph, 100,000 | 123 ms | 1.5-1.8 ms | update 0.75 ms; not drawn yet (2c) |
| 100 lines | 1.5 ms | 0.8 ms | |
| 2,000 paragraphs | 10 ms | 1.1-6.3 ms | frames p95 43 ms: the XAML pass, phase 3 |
| Article, 100,000 | 7 ms | 4.1 ms | as above |

- A cold build is a little slower than DirectWrite's: 139 ms for the 100,000 paragraph against
  121, and about the same for a short leaf. The read-only path does not move to the analyzer for
  speed.
- The 100,000 paragraph still spikes (p95 16 ms, max 47), mostly outside the layout. Suspected,
  not shown: the text copied whole per key as a large object, and the full collections that
  brings.
- One `BeginDraw` took 84 ms in the article, once. Still whole-paragraph per key, and next to measure:
`Prepare` and the prefix compare walk the text, the splices and the copy of everything after the
window move it, and `Draw` draws every line.

## Phase 2: edited text on the analyzer

### Scope

- Only `IsReadOnly` false. `DirectTextLayout` keeps its runtime class and its API, and delegates to
  a new `EditableTextLayout` (C++, `Telegram.Native`) while edited. The read-only path, which bubbles
  may move to, does not change and must not get slower.
- What the editor's lines use, which is what the new layout has to do (`RichLeafView`,
  `DirectTextBlock`):
  - one paragraph per leaf, with a direction, and hard breaks inside it (`\n` in code and in a
    collapsed quote);
  - the family chain through `FontChains` - the packaged emoji font first, its fallback, the
    system's - the size, the weight, italic, and per range bold (semibold), italic, monospace
    (`m_monospaceFamily`, system collection), underline, strikethrough, subscript and superscript
    (at `ScriptScale`, off the baseline);
  - colours per range (`SetColor`, `ClearColor`): links, code tokens, spoilers;
  - inline objects: boxes for custom emoji and buttons, which the caller draws, and formulas
    (`SetMathObject`), which the layout draws;
  - the East Asian locale where the paragraph is East Asian text (`Locales()`);
  - wrapping that breaks inside a word that does not fit (`DWRITE_WORD_WRAPPING_EMERGENCY_BREAK`);
  - the alignment modes (a pull quote is centred);
  - the queries: `Measure`, `Ranges`, `Caret`, `Lines`, `LineLengths`, `HitTest`,
    `HitTestContent`, `ContentEnd`, `LineCount`, `Bounds`, `RenderedPixels`.
- Not needed while edited: `MaxLines`, trimming and the ellipsis, `SetParagraphs`.

### Building a paragraph

1. **Analysis**, through an `IDWriteTextAnalysisSource` and `Sink` of our own over the paragraph:
   `AnalyzeScript`, `AnalyzeBidi` with the paragraph's base direction, `AnalyzeLineBreakpoints`.
2. **Items**, split wherever the script, the bidi level, the style (family, weight, italic, size)
   or an inline object changes, then by font: `IDWriteFontFallback::MapCharacters` with the same
   fallback object `FontChains` gives `IDWriteTextLayout2::SetFontFallback` today, so the fonts
   chosen are the ones chosen now.
3. **Chunks.** An item is shaped in chunks that end at a word boundary, a few hundred characters
   long: a plain paragraph is one item, and shaping it whole is the 121 ms. Shaping does not reach
   across a space - no ligature does, and kerning against a space is negligible - so a chunk's
   glyphs do not depend on its neighbours. A chunk never splits a cluster, a ZWJ sequence or an
   Arabic or Indic word; its ends are break opportunities.
4. **Shaping**, per chunk: `GetGlyphs` (glyphs, cluster map, properties) and `GetGlyphPlacements`
   (advances, offsets), with the shaper's default features, which is what `IDWriteTextLayout`
   asks for when nobody sets typography.
5. **Lines**, broken by us from the cluster advances and the break opportunities: greedy, white
   space hanging at the end of a line, a hard break at `\n` and U+2028, and a break inside a word
   when the word alone is wider than the line. Inline objects are a box of their width.
6. **A line's metrics**: the largest ascent and descent of the fonts and objects on it plus the
   line gap, which is DirectWrite's default spacing. Then the runs of the line in visual order by
   their bidi levels (UAX #9, L2).

### Drawing

- Glyph runs through D2D. Colour glyphs through `IDWriteFactory4::TranslateColorGlyphRun`, each
  layer drawn as its format asks: COLR outlines for the system's Segoe UI Emoji (`DrawGlyphRun` per
  layer), PNG bitmaps for the packaged `apple.ttf`, which is CBDT with GSUB for its sequences
  (`ID2D1DeviceContext4::DrawColorBitmapGlyphRun`), SVG where a font has it (`DrawSvgGlyphRun`).
  `DrawTextLayout` with `D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT` does all of this today; the
  output has to match it.
- Colours split a glyph run where they change, by the cluster map; they do not touch shaping.
- Underline and strikethrough from the font's own metrics. Sub- and superscript shaped at their
  size and drawn off the baseline directly, with no second layout (`DrawScript` today).
- Formulas drawn through their `InlineObject`, as now.

### Changing it

- **What changed.** `SetText` keeps taking the whole text, so `RichLeafView` and `DirectTextBlock`
  do not change. The layout finds the edit itself: the common prefix and suffix of the old and new
  text, and the style parts that differ. A compare of 100,000 characters is tens of microseconds.
  An edit API from the engine can replace it later.
- **Analysis** again over the edit widened to the break opportunities around it. Bidi levels are
  the paragraph's: kept when neither the old text nor the new holds a right to left character or
  a bidi control, analysed for the whole paragraph again when one does (rare, and measured).
- **Chunks**: those the edit touches are shaped again and spliced in; those after it move by the
  length the edit added. Nothing else is shaped.
- **Lines** broken again from the line before the edit's, and only until a line ends where an old
  line ended, moved by the edit: every line after that is the old line, moved. Vertical positions
  after it move by the change in height.
- A width change breaks every line again from the cached advances, shaping nothing. A size, family
  or direction change builds the paragraph again.

### The surface

- A `CompositionVirtualDrawingSurface` per paragraph, as tall as the paragraph: it is sparse, so
  the 40,000 pixel paragraph is a surface like any other, and only what is drawn takes memory.
- Drawn in tiles of fixed height (a few hundred pixels), only those in the viewport and a margin
  around it; tiles that leave the margin are given back (`Trim`). An edit redraws the tiles of the
  lines that changed, and when the lines below moved, the visible tiles below.
- The viewport comes from `DirectTextBlock`, from `EffectiveViewportChanged`, handed to the layout;
  the brush and the visual are sized to the paragraph rather than to the surface's pixels.
- Device replaced: the visible tiles are drawn again.

### Proving it

- **Against a known answer.** A test mode lays out the same text both ways, `IDWriteTextLayout`
  and the analyzer, and compares line breaks (exact), caret positions (within half a DIP) and line
  heights. The corpus: the messages of `formatted-text-block-test-messages.py`, and text in Latin,
  Cyrillic, Greek, Arabic, Hebrew, Devanagari, Thai, Chinese and Japanese, emoji with skin tones and
  ZWJ sequences in both emoji fonts, mixed directions, custom emoji boxes and formulas.
- **Incremental against cold.** After every edit of a scripted run, the layout must equal a cold
  build of the same text: chunks, lines and metrics. A debug switch checks it on every key.
- **The meter.** Targets: the 100,000 character paragraph at 2 ms a key or less, layout and draw
  together; the 4,096 one at 1 ms; opening the 100,000 one by shaping its text and drawing only the
  visible tiles.

### Steps

1. **2a.** The analyzer layout built cold, drawn with colour glyphs, answering every query; the
   known-answer comparison against `IDWriteTextLayout`, and its cold cost through the meter.
2. **2b.** Chunks, the incremental update and the convergence, checked against cold builds.
3. **2c.** The virtual surface, its tiles and the viewport.
4. **2d.** Edited text switched to it; the meter's targets met.

### Risks

- Colour glyphs: CBDT bitmaps and COLR layers have to come out as `DrawTextLayout` draws them,
  including at fractional scales.
- Shaping windows in complex scripts, and caret and selection semantics across direction changes,
  which hit testing gives today for free.
- Line spacing has to match DirectWrite's default, or text moves when a line becomes editable.
- It is a text layout engine: thousands of lines of C++, against an API that does the same today
  in one call.

## Open questions

- Whether edited lines should take one line height from the primary font instead of DirectWrite's
  default spacing: it would stop a line growing for an emoji, a custom emoji or an IME composition
  (the Chinese testers' jumping text), and would make an edited line differ from the same text shown.
- Whether the read-only path moves to the analyzer too: not for build speed, which is DirectWrite's
  or a little worse cold (measured 2026-10-15); only if drawing it ourselves is worth it, which the
  draw times (0.7 ms against 5.7 for 4,096 characters) suggest it might be.
- How a virtualized document answers the automation peer, which reads the flattened document:
  from the model, but every range it hands out has to stay valid across realization.
- Find in the article editor, and the spell checker's squiggles, over blocks that are not realized.
- Whether a paragraph per line in `MessageTextBox` stays, or one paragraph with line breaks: one
  layout and one surface for the whole message, the input service seeing all of it - worth it once
  a keystroke no longer pays for the paragraph's length.
