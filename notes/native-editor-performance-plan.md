# Typing at any length in the native editor

A plan (2026-10-15); the meter is built and has measured, phase 2 is done (2026-10-16), phase 3 is
under way, 3a and 3b built and measured. The native editor (`Controls/RichEditor/`) is to become the
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
- One `BeginDraw` took 84 ms in the article, once.

The meter now also counts collections per run: of every generation, and the keys whose handlers ran
into one or whose frame had a full one, with their median against the rest.

**Phase 2c, written (2026-10-15), not yet built or run.** An edited text on the analyzer whose
surface would be taller than 2,048 pixels is drawn into a `CompositionVirtualDrawingSurface`
instead (`DirectTextLayout::RenderVirtual`), as wide as the box and twice as tall as the text,
returned through `Render` as the `CompositionDrawingSurface` it derives from. It is drawn in tiles
of 256 pixels, only those in the viewport and 512 pixels around it; the others are trimmed.
`EditableTextLayout` reports what each build or update changed (`EditableChange`: everything, or
the rows of the lines broken again, down to the end when the height after them moved), and the
layout gathers that until the next draw, which redraws those rows only, cleared and clipped, with
the lines a little above and below drawn into them for what reaches across. Colours are compared
against the ones last drawn, moved by the edit (`DiffColors`): one that only moved costs nothing,
one added, removed or recoloured redraws its lines; two edits between draws redraw everything.
`DirectTextBlock` hands the viewport to `SetViewport` from `OnViewportChanged`, through the
registration `DirectTextBlockBase` holds in C++ for the custom emoji players, kept while either
needs it; `SetViewport` draws what came into view at once. Before the first viewport, the start
of the text is drawn. To check by eye: scrolling the 100,000 paragraph, typing in it at the top,
in the middle and at the end, revealing a spoiler and following a link past an edit. Still whole-paragraph per key, and next to measure:
`Prepare` and the prefix compare walk the text, the splices and the copy of everything after the
window move it, and `Draw` draws every line.

**Phase 2d, the switch (2026-10-16).** Edited text is on the analyzer by default: Diagnostics'
Analyzer Text Layout is on unless turned off, which is the way back to `IDWriteTextLayout` should
a text come out wrong, and the comparison and the checked runs stay in the meter. The targets, in
Release: the 100,000 paragraph at 0.75 ms an update and 0.13 ms a draw against 2 ms, the 4,096 one
at about 0.3 ms against 1. Not met, and not the layout's: the spikes of full collections (below),
and the many-paragraph documents, which are phase 3.

**Full collections (2026-10-15, Release).** In the 100,000 paragraph, 9 gen2 collections in 200
keys, the frames they fall in 38 ms at the median against 4.15; one every run in the 4,096 one.
What a key copies whole, from reading the code: the leaf's string itself (`RichTextLeaf.Insert`,
an immutable `string`), on the large object heap past 42,500 characters; on a space, everything
before the caret as well, for the markdown rules (`TryApplySpaceRule`); twice the whole message in
the message box, for the composer and the chat; and natively, the direction of each leaf up to the
first with a strong character, twice for the one edited. Done: the rules look at no line longer
than any of them can match; `GetDirectionality` takes its string by reference, and a leaf caches
its direction per version; `MessageTextBox` converts its document once per state of it. Left: the
leaf's string, which takes a buffer in the leaf and an edit call to the layout instead of a whole
string per key; a code block tokenized whole per key; and what a screen reader's `GetText` builds.

**Phase 2e, the edit instead of the string (2026-10-16, unbuilt).** `RichTextLeaf` holds its text in
a `char` buffer edited in place, shared with a clone - an undo step - until either writes; `Text`
is made when asked and kept until the next edit, and what runs on every key reads `Chars`, the
indexer, `Substring` or `IndexOf` instead: the caret and word stops (`ReadOnlySpan<char>`, with
`StringInfo` asked only past a character on its own), the markdown rules, suggestions
(`AutocompleteEntityFinder.TrySearch`), the composer's emoticons and length, the input service's
requests, the height estimates. Each change is journalled, the last eight (`RichTextEdit`, merged
by `TryGetEdit`, which also says whether a style changed on its own), and the direction is worked
out from a prefix and again only for an edit before where it was decided. `RichLeafView` hands
`DirectTextBlock.ReplaceText` the edit rather than the text whenever only the text changed and
the line holder stayed; the block keeps a length and reads its text back from the layout when
asked. `DirectTextLayout::ReplaceText` puts the text together natively, moves its colours with it,
and gathers the edits until the build, which hands them to `EditableTextLayout::Update` in place
of comparing the texts from both ends; the inline objects outside the edit are still compared, as
they are handed over again with every text, and the layout's own copy is replaced in place. The
spell checker moves its ranges by the journal instead of keeping a copy of every leaf to compare
against. A code block is tokenized one run at a time, again on the text as it is once a run comes
back stale, 150 ms after typing when it was typed into; the tokens move with each edit meanwhile,
and a run's colours go over in one call (`AddColors`) after the moved ones are cleared, where they
were set one by one, each looked up among the rest. A screen reader's `GetText` with a limit builds
only that much. Still whole per key, natively: `Prepare`'s per-position styles, objects and
analysis text, the copy of the runs and glyphs after the window, and putting the `hstring`
together; and a word ended checks the whole leaf (`SpellingWork`), a string of it per check.

Ninth run (Release, 1x and about 200 Hz), the 100,000 paragraph: full collections 4 in 200 keys
against 9, every collection a full one; a key's handlers 3.3 ms in the middle and 2.5 at the end,
against 1.5 to 1.8 in phase 2d, with the layout ~1.7 ms an update once the cold halves of the ten
Enters (24 ms each) are taken out, against 0.75. Not measured where it goes, as only cold builds
were timed by stage. Read from the code since: an update moved its per-position styles and
objects out to compare against and `Prepare` allocated them again, 400 KB each a key here, and
the runs and glyphs after the window were copied into new vectors and back. Since: told the edit,
the per-position state keeps its capacity; the tail goes through vectors the layout keeps; and
updates are timed by stage (`EditableTextLayout::UpdateStages`: preparing, finding the edit,
splicing, analysing, shaping, putting the tail back, breaking lines, and how many were told their
edit). The collections left, from the code: the spell checker took the whole leaf as a string
for every word ended, and the first key of every undo step copies the buffer it shares. Since: a
result keeps the range edits left unchecked (`DirtyStart`, `DirtyEnd`) instead of a flag, and a
check takes only that, out to the ends of its words, and keeps what was found around it.

Tenth run (Release, 1.5x), the mixed article: a key 0.75 ms against 0.85 to 1.1, one full
collection in 300 keys; updates 133 µs each - preparing 7, finding the edit 0.5, splicing 18,
analysing 18, shaping 64, the tail 2, breaking 23 - 300 of 1,330 told their edit, the typed keys,
the rest pooled views given a whole new text. The 50 cold builds mapped fonts for 1.5 ms each
against 7 µs in the seventh run, and setting up took 137 µs against 20, with an open of 179 ms
against 17 and one build of 66 ms: as if the fonts were loaded in this run, not shown. Not yet
run on the 100,000 paragraph, where the 1.7 ms an update is.

Eleventh run, the 100,000 paragraph (1.5x): a key's handlers 1.50 ms in the middle and 0.89 at
the end, against 3.30 and 2.53; every typed key told its edit, finding it 0.4 µs. What a typed key
still pays whole, from the stages: preparing ~210 µs, splicing ~135, the tail ~150 (the averages
also hold the halves of the ten Enters, ~20 ms each, which inflate shaping, analysing and
breaking). Full collections still 7 in 300 keys: `RichTextWindow.UpdateSendLock`, on every change
and then every 500 ms while typing, measured the document for its limits
(`RichDocumentLimits.Measure`) with `ToPlainText` and `ToRichText` of every leaf, which is the leaf
as a string and as a rich text. Since: the length and the depth of styles come from the runs, with
only an object's own rich text made. Left: for a user without Premium the send lock converts the
whole document (`ToInputRichMessage`) on the same timer.

**Phase 3a, blocks kept in place (2026-10-16, unbuilt).** Every list of presenters - the root
blocks, a quote's, a details block's, a list's items and an item's blocks - is reconciled in
place (`RichEditorView.Reconcile`): what left is removed, what came in inserted, what moved moved,
where each of them used to be cleared and filled again whenever one differed. The meter's typing
scenarios gain a third run, Enter every tenth key in the middle, with the Enter keys' handlers and
layout split out. Moved to 3b, where it has something to do: routing the geometry queries through
`EnsureRealized`. Measured in Release, 2,000 paragraphs: a key 6.3 ms in the view against 0.9 in
100 lines, and Enter 61 ms against 5.4 - about 3 µs a block a key and 30 µs a block an Enter, all
of it in the view, so all of it what the window is for.

**Phase 3b, the window (2026-10-16, unbuilt).** `RichBlocksPanel` replaces the `StackPanel`: it
places each realized root by its index (`RichBlockPresenter.RootIndex`) at the sum of the heights
before it, a height per root kept by block across structural changes, measured when realized and
estimated when not (`EstimateHeight`: each leaf's length wrapped at half its font size a character,
a line 1.33 of it; a guess per block selected whole). Past 100 roots (`VirtualizeAbove`) `Realize`
binds only the window: a screen either side of the panel's effective viewport, or of the caret before
there is one, and pinned the caret's root and its two neighbours, both ends of the selection, the
selected block, and what `EnsureRealized` was asked for until the window next moves. A selection
change that leaves a pin outside realizes again (Ctrl+End, Select All). The viewport moving by more
than half the margin realizes again, then redraws the selection, decorations and squiggles without
scrolling to the caret. Realized roots are scroll anchors. Only the automation peer's
`ScrollIntoView` calls `EnsureRealized`: every other query is of a pinned root, or of what is on
screen. Not done: Page Up and Down still walk lines; a playing video or a drag inside a root that
leaves the window is not pinned.

First run, by hand: content slow to appear while scrolling 2,000 paragraphs, and dragging the
scrollbar very laggy. Each window move built every incoming paragraph from nothing - template, text
block, layout, surface - for three screens, and a dragged scrollbar passes the whole window every
frame. Since: paragraph presenters leaving the window go to a pool (up to 128) that those coming in
take from, before they are made; a view reused for another leaf drops the selection it drew; while
the view moves the margin is half a screen, widened to one when the dispatcher is next idle. The
meter scrolls twice, half a screen and a twentieth of the document a frame, and counts the passes,
their time, and the paragraphs made and reused.

Second run (Release, 2,000 paragraphs): opened in 25 ms against 1,808, 53 blocks realized; a key
1.3 ms against 6.3. Not yet: scrolling half a screen a frame, 118 ms frames with 5.5 ms of `Realize`
in them; a twentieth a frame, 44 ms with 22; Enter 123 ms against 61 with everything realized. And
a bug: a pooled paragraph reused for another kind drew in the old kind's style before settling, a
visual state taking hold late. Since: the pool is by kind (`PoolKey`); narrowing the window while
it moves keeps what is realized inside the wide margin; the meter splits a frame into following the
viewport, realizing, the overlay, and the panel's measure and arrange, and a key into realizing and
the blocks' measure, with passes counted for Enter.

Third run: 2,000 paragraphs, a key 1.1 ms, Enter 9.3 ms against 123; the 100,000 article, Enter
22 ms. Scrolling half a screen a frame, 37 ms frames: the editor's own 8.3 ms of it - following the
viewport 3.0, the blocks' measure 3.6, overlay and arrange 1.8 - and the rest XAML's or a missed
vsync, a frame over 16.7 ms counting as 33. Two things in the editor's: every move bound all ~88
realized roots again, and a paragraph's text bound to a pooled view cost ~235 µs, an update with
nothing in common, against 118 for a cold build. Since: `RealizeWindow` moves the window by what
left and what came in, for paragraphs, and anything else takes the whole pass; and
`EditableTextLayout::Update` builds cold when its window is the whole paragraph, which is every
paragraph of one run, typed into or rebound.

Fourth run: scrolling half a screen a frame, 16.9 ms frames at the median in 2,000 paragraphs (41
of 142 over 25 ms), 20.3 in the 100,000 article; following the viewport 1.65 ms against 3.0. A
paragraph's text still ~240 µs: the cold build did not move it, if Telegram.Native was rebuilt,
and the 118 µs at open was likely 2,000 builds back to back, everything warm. Dragging still 32 ms frames: 56 paragraphs realized a jump, 13.7 ms
of their text measured. Since: a viewport that lands clear of the window realizes itself only,
widened when idle.

Fifth run: dragging, 17.3 ms frames, none over 25 ms, against 32 (the 100,000 article 19.3); half
a screen a frame unchanged. What is left in every scroll is a paragraph's text, ~250 µs for 60
characters. A cold build makes, for every paragraph, a text format for its tab stop, the system
fallback, the base font's face and a text analyzer, none of which depend on the text: the counters
now split cold builds into preparing, setting up, analysing, mapping fonts, shaping and breaking
lines (`EditableTextLayout::BuildStages`), before any of it is cached.

Sixth run, 60 characters a build, µs: setting up 8.3, analysing 11.5, mapping fonts 64, shaping
129, breaking lines 19; the 300 character leaves of the mixed article twice that, which scales with
words, not with 256 character chunks. Sharing the setup across layouts - all four are immutable
DirectWrite objects - saves 8 µs of 233. The likely cause, from the August `RichEditBox` freeze
(memory: packaged font fallback): an editor's text has no family, so its base font is the packaged
emoji font, letters fall back to the system's, and the emoji font has a space - mapping cuts a
paragraph at every space, a run for each word and each space, mapped and shaped one by one. The
counters now give runs a build to confirm it. The cold build `Update` took for a whole-paragraph
window is reverted: Telegram.Native was rebuilt for the fifth run, and it changed nothing.
The emoji font maps, besides emoji, only the space, `#`, `*`, the digits, © and ®, plus U+200D
and U+20E3; its space is Segoe UI's (561 units, the same ascent and descent), and was put back the
day the font was trimmed (ec0529bcc, 2026-08-19) because TextBlock drew no spaces without it, as
Fela recalls. Decided: every `DirectTextBlock` defaults to `EmojiTextThemeFontFamily`, text font
first, as `ChatTextBox` already was - the implicit style, the two keyed ones in App.xaml and the
editor's leaf template. A paragraph should be one run; keycaps, © and ® are drawn as text, in the
bubbles too. Headings keep `EmojiThemeFontFamilyWithSerif`, emoji first.

Seventh run: 1.0 runs a build; a paragraph's text 89 µs against 258, the mixed article's 180
against 448; the blocks' measure 1.3 ms a frame scrolling against 3.6, frames over 25 ms 8 of 142
against 21, the editor's own share of a frame about 4 ms. Of a cold build's ~100 µs, setting up
was now 20 - but only 88 of 3,450 builds were cold: a pooled view's new text is an update, which
keeps what the last build set up, so setup is about 1 ms of an open. A per-thread cache of it was
written and dropped unbuilt, not worth what it would hold.

**Phase 3c, navigation (2026-10-16).** Most of it came with 3b's pins: Up and Down always
have the caret's neighbours to land in, the document's edges and Select All move the selection and
so realize its ends, and what a selection reaches is drawn as it scrolls in. What was left, Page Up
and Down, no longer walk some 30 lines: `MovePage` scrolls the view by its height, realizes the root
a viewport from the caret (`EnsureRealizedAt`), and hit tests there at the run's column; with Shift
it takes the far edge of the new page instead, the last line wholly on it; when the hit would not
move it forward, it goes to the document's edge. A scroll the view asked for is remembered until
it lands (`_scrollingTo`), so bringing the caret into view meanwhile starts from where it is going.
The caret is drawn over a selection now, at its moving end, which is what shows where Shift+Page
Down left it; the far edge is inside `BringIntoView`'s margin, or revealing it would scroll on.

**Phase 3d, pools (2026-10-16).** Measured first: in the mixed article - a section is a
heading, three paragraphs, a quote, code and a list of three, a table every fifth - the editor's
own share of a scroll frame was 6.1 ms against 4.2 in 2,000 paragraphs, 13 frames of 146 over 25
ms against 8. A third of its roots are not paragraphs, so nearly every window move took the whole
pass, and every quote, list, item, table and cell that came in was built new, its text cold: 325 of
1,488 builds. Since: `RealizeWindow` takes any root out by itself (`Unrealize`: its leaves through
its stops, what is nested through the model, which a window move has not changed), and the full
pass does the same for roots leaving the window before anything comes in. Every presenter but
media and atoms is pooled, by kind and the state its look depends on - a heading's size, a quote
collapsed, details open, a list's style, a header cell - up to 192 in all. The meter counts
presenters of every pooled kind, and whole passes. Not done: virtualizing inside a huge root, which
no document measured so far has.

Eighth run, the mixed article, on a display at 1x and about 200 Hz, so the frames themselves do not
compare: half a screen a frame, no whole passes in 144, 3 presenters made and 973 reused, no cold
builds and no new surfaces; a pass 1.67 ms against 3.00, the blocks' measure 1.11 against 2.24, the
editor's own share of a frame 3.8 ms against 6.1; frames over 25 ms 1 of 146 against 13, at 63.9
ms. A twentieth a frame: none over 25 ms.

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

## Phase 3: blocks in view only

Designed 2026-10-16, against a map of `RichEditorView` taken the same day.

### What it costs today

- **Every block is a presenter** in the `Blocks` `StackPanel`, nested blocks as presenters inside
  their parent's template; 2,000 paragraphs open in 1.8 seconds and every frame lays them out.
- **A structural change touches all of them.** `Realize()` empties `_leafViews`, `_nodeViews` and
  `_itemViews` and binds and updates every presenter again; when the root list changed at all -
  Enter at the top level - it clears `Blocks` and adds every root back, so the whole content
  leaves the tree and comes back.
- **A key lays out the stack twice.** `UpdateSelection` and `UpdateCaret` call
  `Surface.UpdateLayout()`, and a size change anywhere measures the `StackPanel`'s children;
  `UpdateDecorations` and `DrawSpelling` walk every leaf view.
- **Geometry exists only where a view does.** The caret, hit testing, vertical movement, Home and
  End, the selection, grippers, suggestions, composition, decorations and the automation peer's
  rectangles all ask a view; with none they do nothing, or the wrong thing: vertical movement goes
  to the document's edge when nothing is below it, and a caret with no view never scrolls in.
- **The model already has what the view needs:** `Blocks`, `Stops`, `Leaves`, a leaf's
  `StopIndex`, a block's `FirstStop` and `LastStop` - contiguous and increasing at the top level,
  so the root of any stop is a binary search - and `Owner` and `Parent` for the chain.

### Scope

- **Root blocks are the unit.** A root is realized whole, nested blocks and all; one that is not
  stands as a height. A single huge list or table stays realized whole, until measured otherwise.
- **Short documents are not virtualized.** Below a number of roots - every message box, for one -
  the window is the whole document and nothing changes.
- **Pinned roots** are always realized: the caret's and the selection anchor's, the selected
  node's, and one with pointer capture, an upload or a drag in progress.

### The panel

- `Blocks` becomes a panel of our own, not an `ItemsRepeater` (decided 2026-10-16): presenters are
  bound to model identity and `EnsureRealized` has to realize synchronously, where the repeater
  recycles by element kind on a schedule of its own. It lays out the realized roots in document order at the
  offsets a height map gives them, and reports the total height.
- **The height map** holds a height per root, measured when it has been realized and estimated when
  not: a paragraph from its length, the width and the line height; media from its aspect ratio;
  everything else a fixed guess per kind. Offsets are a running sum, a tree if measured to need it.
- **The window** is the viewport and a margin of a screen above and below. The panel follows the
  viewport (`EffectiveViewportChanged`, outside the layout pass), and roots come and go as it moves.
  Before the first viewport, a screen from the caret's root.
- **Anchoring.** An estimate above the viewport replaced by a real height must not move what is
  seen: the realized roots are scroll anchors (`CanBeScrollAnchor`), and the offset is corrected by
  hand if that does not hold.

### Realizing

- `Realize()` reconciles the panel with the window instead of the whole tree: roots that left it
  are unrealized and their registrations go with them, roots that entered it are bound, in place,
  without moving the others. The pass over the model stays; the pass over XAML is the window only.
- **`EnsureRealized(leaf or block)`** realizes the root of what it is given and lays it out. Every
  geometry query that may land outside the window goes through it first: the caret, a selection
  end, `FocusLeaf`, vertical movement past the last realized line, Ctrl+Home and Ctrl+End, the
  automation peer's `ScrollIntoView`, a drop target, the drag's autoscroll.
- **A root realized into a selection** is told its share of it, as `UpdateSelection` tells the
  others; spelling and decorations draw for what is realized, again on a realization.
- **Page Up and Down** scroll by the viewport and hit test where they land, instead of moving a
  line at a time some 30 times.

### Not realized, and how it answers

- The automation peer's text, ranges and attributes are the model's already. Rectangles of what is
  out of view are empty, which is what the platform expects of text off screen; embedded objects
  out of view are not among the children until they are realized.
- Suggestions, composition and the caret live in the caret's root, which is pinned.

### Steps

1. **3a.** Every list of presenters reconciled in place rather than cleared, which ends Enter
   re-adding every root; the meter's run with Enter.
2. **3b.** The panel, the height map and the estimates, the window and the pinned roots,
   anchoring, and `EnsureRealized` at every geometry query that may land outside the window. The
   meter: opening, keys, and a scroll from end to end at a frame each.
3. **3c.** Navigation across what is not realized: vertical movement, Page Up and Down, the
   document edges, selections that reach out of view (Select All, Shift with a click far away).
4. **3d, if measured.** A pool of presenters per kind, and virtualizing inside a huge root.

### Risks

- Interaction inside a presenter - a checkbox, a table's scroller, a playing video - when its root
  leaves the window: what is pinned, and what a player does when taken down.
- Estimates that are badly wrong move the scrollbar as roots are realized; the meter's scroll has
  to show it does not jump.
- Every call site in the map has to be found: below the root count that turns the window on, a
  missed one cannot show, so the meter's documents are what has to find it.

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
