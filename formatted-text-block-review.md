# FormattedTextBlock review — to-do

Read-through of `Telegram/Controls/FormattedTextBlock.cs` (2,704 lines) and
`Telegram/Controls/FormattedTextBlock.Selectable.cs` (405), cross-checked against
`Telegram.Native/Controls/FormattedTextBlockBase.{h,cpp}`, the host
`Telegram/Controls/Messages/MessageTextBlock.cs`, `Telegram/Common/TextStyleRun.cs`
(`StyledText`/`StyledParagraph`), the template in `Telegram/Themes/Generic.xaml:2367`
and the call sites in `Controls/Cells/`, `Controls/Messages/` and `Controls/Chats/`.

Line numbers are as of `73b3ec369` (the three files are untouched since `c23b13d6d`), i.e.
**before** the P0 commits below; anything still open has shifted by ~17 lines since.

Legend: **[live]** = confirmed reachable from current app code, with the call site named ·
**[latent]** = correct today only by convention or because no caller exercises it.

The three facts most of this rests on:

- `MessageTextBlock` hands each child block a **paragraph range of a shared `StyledText`**
  (`MessageTextBlock.cs:313-354`). Every offset in `StyledText.Text` is absolute for the whole
  message; the rendered/`TextHighlighter` index space is per block and starts at 0. `_indexMap`
  is the only thing that reconciles the two.
- Only `MessageBubble.Message` gets a `RecyclePool` (`MessageBubble.xaml.cs:190/278`). Every
  other `FormattedTextBlock` in the app — `ChatCell.BriefText`, `WebPageContent`, `PollContent`,
  `MessageReply.Label`, `ChatPinnedMessage`, … — runs with `_pools == null`, which changes what
  `OnLoaded`/`OnUnloaded` do.
- The app runs **several XAML threads** (see the `[ThreadStatic] RelativeDateService` at
  `:2543`). Anything cached statically here must be a value type or `[ThreadStatic]` —
  `Brush`/`FontFamily` are `DependencyObject`s and thread-affine.

---

## P0 — wrong text comes out

- [x] **The fast path drops the index map for blocks that don't start at paragraph 0** —
      `FormattedTextBlock.cs:1005-1007` **[live]**
      → fixed in the commit that checked this box (`git log --follow formatted-text-block-review.md`)

      ```csharp
      // Plain single run: rendered index == styled offset, so the converter's
      // 1:1 fallback is exact — no map needed.
      _indexMap = null;
      ```

      The identity only holds when `_first == 0`. The fast path is entered whenever
      `rangeStart == rangeEnd` and that paragraph `IsPlain` (`:929`), and `MessageTextBlock`
      emits exactly that shape for **every single normal paragraph sandwiched between typed
      ones** (`MessageTextBlock.cs:230/234`) — e.g. a message of `code block / plain line /
      code block` gives the middle block `_first == _last == 1`. A fresh block has
      `_plain == true` and `HasCodeBlocks == false`, so the `_plain == prevPlain` guard does
      not save it.

      With a null map, `RenderedToStyled` and `StyledToRendered`
      (`Selectable.cs:278/311`) return the rendered index unchanged, so everything built on
      them is off by `styled.Paragraphs[_first].Offset`:

      - `GetSelectedText` (`Selectable.cs:168`) — selecting that line and copying yields text
        from the **start of the message**, of the right length.
      - `GetSourceOffset` (`:182`) — the cross-block range `TextSelectionManager` builds is
        anchored at the wrong paragraph.
      - `GetSelectionBoundary` (`:199`) — double-click word / triple-click paragraph resolves
        against the wrong paragraph, so `ParagraphRange` returns another paragraph's `[lo, hi)`.
      - `ApplyHighlighters` (`:505`) — `StyledToRendered(find)` places the search highlight past
        the end of the block's content.

      Fixed with an `_origin` field — the styled offset that rendered index 0 maps to — set
      next to `_first`/`_last` in `SetText`, so it is right before every early return and on
      both paths, and consulted only by the no-map fallback in `RenderedToStyled` /
      `StyledToRendered`. The fast path stays allocation-free.

      Setting it in `SetText` rather than in the fast-path branch also covers the case the
      original note missed: the slow path can leave `_indexMap` **empty** (every entity skipped
      by the `entity.Length + entity.Offset > text.Length` guard at `:1141`), which takes the
      same fallback.

- [x] **`ProcessCodeBlock`'s execution guard is an ABA** — `:941`, `:886`, `:1931` **[live]**
      → fixed in the commit that checked this box

      `var execution = ++_templateExecuted;` (`:941`) versus `_templateExecuted = 0;` in
      `OnUnloaded` (`:886`). The counter restarts, so `execution == 1` is handed out again after
      every unload. A tokenization started before the unload (`SyntaxToken.TokenizeAsync`, an
      `await` that can outlive a scroll) resumes, finds `_templateExecuted == 1` and passes the
      "still the same content" test for **different** content.

      What it then does is worse than a stale repaint: `inlines` is the `Paragraph_Inlines`
      collection of a paragraph that `Recycle` (`:807-812`) has already returned to the shared
      `FormattedTextBlockRecyclePool`, so `direct.ClearCollection(inlines)` (`:1940`) wipes
      whichever block dequeued it, and `ProcessCodeBlock` fills it with the old message's syntax
      spans. Only pooled blocks (message bubbles) are affected, which is also the only place
      code blocks scroll fast.

      Fixed with a second, never-reset `_generation` counter for the async guard.
      `_templateExecuted` kept only its other job — the "text already applied" flag `OnLoaded`
      tests, which is exactly why it has to be cleared on unload — so it became
      `bool _textApplied`.

---

## P1 — allocation on the hottest text surface in the app

- [x] **`_light` and `_dark` are instance fields** — `:2023` and `:2054` **[live]**
      → fixed in the commit that checked this box

      Two `Dictionary<string, Color>` of 27–28 entries, built **per `FormattedTextBlock`**,
      i.e. per message block, whether or not the message contains code. Colors are structs and
      the tables are constant, so `static readonly` is a straight win (≈3 KB per instance) with
      no thread affinity to worry about.

      `_brushes` (`:2087`) must stay per-thread at least, because `SolidColorBrush` is
      thread-affine, but it can be allocated lazily on the first `GetColor` call instead of in
      the field initializer — only code blocks ever touch it.

      Both done: the tables are `static readonly` (never mutated — `OnActualThemeChanged`
      writes through to the *brushes*, not the tables), and `_brushes` is now null until
      `GetColor` needs it, with the theme handler returning early when it is.

- [x] **`monospaceFontFamily` is never assigned** — `:1060-1064` **[live]**
      → fixed in the commit that checked this box

      ```csharp
      FontFamily monospaceFontFamily = null;
      FontFamily GetMonospaceFontFamily()
      {
          return monospaceFontFamily ?? new FontFamily("Cascadia Mono, Consolas, " + Theme.Current.XamlAutoFontFamily);
      }
      ```

      The local is only ever read, so the memoization does nothing: every inline-code entity in
      the paragraph allocates a fresh `FontFamily` plus the concatenated string.

      Same in the recursive `ProcessCodeBlock` (`:1956`) — `new FontFamily(...)` is built once
      per **token node**, so a syntax-highlighted block allocates one per span in the tree.

      Worth remembering that a font chain is not free at use either: see the
      "packaged font fallback cost" note — RichEdit pays ~1 ms per run for a chain whose first
      entry misses.

      Fixed one level up, on Fela's call: the chain is now `Theme.MonospaceFontFamily`, built in
      `UpdateEmojiSet` beside the other families — which is also the only place it can change —
      and every site in the app reads it. That covers the eight other call sites, six of which
      (`TextBlockHelper.cs:226/347`, `GameContent.xaml.cs:240/245`,
      `ProfileHeader.xaml.cs:1735/1740`) were allocating one `FontFamily` **per code entity
      inside a render loop**.

      Those six built `"Cascadia Mono, Consolas"` with no fallback tail, so they now inherit
      one: a character the monospace faces don't cover (an emoji in a code span in a bio) will
      render instead of dropping to the system default. Deliberate, but it is a rendering
      change — say so if you'd rather keep two chains.

- [ ] **`OnPointerMoved` re-sets the cursor on every sample** — `:322-331` **[live]**

      ```csharp
      if (hyperlink == null)
      {
          _textSelectionIBeam = true;
          Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(CoreCursorType.IBeam, 0);
      }
      ```

      `_textSelectionIBeam` is written but never tested, so moving over message text allocates a
      `CoreCursor` and writes `CoreWindow.PointerCursor` at pointer sample rate. Guard on
      `!_textSelectionIBeam` (the flag already exists for exactly this) and keep one cursor
      instance per type — the two `Arrow` sites at `:316`/`:340`/`:346` and the `Hand` at `:304`
      allocate too, though those are edge-triggered.

- [ ] **`MeasureOverride` runs a native text measure per pass for expandable quotes** —
      `:256-277` **[live]**

      `PlaceholderHelper.Foreground.MaxLines(...)` on every measure, with no memo on
      (text, width, size). Quote blocks re-measure with the rest of the bubble on every window
      resize and on every `InvalidateMeasure` from the panel. The inputs are all cheap to
      compare — cache the last `(width, partial, size)` and reuse `IsTextTrimmable`.

      Related: `IsTextTrimmableChanged` is raised **from inside** `MeasureOverride` (`:271`) and
      `BlockQuote.OnIsTextTrimmableChanged` (`BlockQuote.cs:62`) reacts by changing state that
      feeds `ComputedIsExpandable`, which `MessageTextBlock.ArrangeOverride` reads
      (`MessageTextBlock.cs:445`). That is the shape the layout-cycle audit is about; worth
      checking it can't re-enter.

- [ ] **Seven collections per instance, before any content** — `:97-99` and `:555-558`

      `_links`, `_dates`, `_spoilers` + four `HashSet`s for the active elements. The vast
      majority of blocks have no hyperlink, no spoiler and no date. Lazy `??=` at the first
      `Add` costs a null check on a path that is already doing XAML interop.

      While there: the four `_active*` sets only need `Contains`/`Remove` for
      `_activeRuns` (`:1934`, the async code path). `List<T>` is cheaper to fill and to
      enumerate for the other three, and `_activeRuns`' single linear scan happens once per
      tokenized code block.

---

## P2 — state that survives when it shouldn't

- [ ] **A block with no `RecyclePool` never rebuilds after unload/reload** — `:864` vs `:887`
      **[latent]** — needs a repro before fixing

      `OnUnloaded` calls `ClearEntities()` unconditionally (`:887`) — tooltips detached, relative
      dates unsubscribed, `_spoilers` emptied, `_effectiveViewportChanged` dropped and the
      viewport registration revoked — and only *then* returns early for `_pools == null` (`:889`),
      leaving the inline tree in place. `OnLoaded` then returns early for the same reason
      (`:864`), so nothing re-runs `SetText`.

      The visible tree still shows the right text, but: link tooltips are gone, relative dates
      freeze, custom emoji stop being told about the viewport, and `_spoilers` is empty while
      the transparent `_spoiler` highlighter is still applied — so the next `UpdateSpoilers`
      (any inner size change reaches it through `HandleSizeChanged`,
      `FormattedTextBlockBase.cpp:73-77`) removes the particle overlay and leaves the spoilered
      text *invisible* rather than revealed.

      Most non-pooled hosts re-`SetText` on reuse, which is why this hasn't shown up; the ones to
      check are those that unload/reload without re-setting text (popups, pivots,
      `ChatPinnedMessage`). Either move `ClearEntities()` behind the same early-out, or let
      `OnLoaded` re-apply when `_text != null` regardless of `_pools`.

- [ ] **Relative dates can stall for good** — `:2600-2648` **[live]**

      In `GetNextUpdateInterval`, an item that isn't due yet contributes
      `remainingSeconds = (long)(item.NextUpdateAt - tickCount) / 1000` (`:2640`) and is skipped
      when that truncates to 0. If every item is within a second of its deadline — the normal
      case for the `< 60s` bucket, where the interval is 1 s and `DispatcherTimer` can fire a
      few ms early — `minSeconds` stays `int.MaxValue` and the timer is rearmed
      **68 years out**. Nothing else restarts it, so every relative timestamp on the thread
      freezes until another `Subscribe` happens.

      Use `Math.Max(1, ...)` (or ceil the division) for the not-due branch, and clamp the final
      interval before `TimeSpan.FromSeconds`.

- [ ] **`record TextDate`** — `:2494` **[live]**

      The project rule is that .NET Native has no records. It is also the wrong shape: it's a
      mutable dictionary *value* (`NextUpdateAt { get; set; }`) that never needs value equality,
      and the generated `Equals`/`GetHashCode`/`PrintMembers` walk five reference members. A
      plain class is smaller and generates nothing.

- [ ] **Revealing a spoiler wipes the search highlight** — `:435-436` **[live]**

      ```csharp
      SetText(_clientService, _text, _first, _last, _fontSize);
      SetQuery(string.Empty);
      ```

      `SetText` already calls `ApplyHighlighters` (`:1584`), so the `SetQuery(string.Empty)` is
      not needed to repaint — it only sets `_query = ""`, dropping the in-message search
      highlight (`MessageBubble.xaml.cs:147`) when the user taps a spoiler. Delete the line.

- [ ] **`ApplyHighlighters` silently drops everything when the inner block isn't loaded** —
      `:482` **[live, but pre-existing]**

      The `!TextBlock.IsLoaded` early-out predates the refactor (it was `SetQuery`'s), but it now
      gates the spoiler/cached/marked highlighters too, and there is **no re-apply on load**:
      `OnApplyTemplate` calls `SetText` (`:247`), which sets `_templateExecuted = 1`, and
      `OnLoaded` returns early precisely on `_templateExecuted > 0` (`:864`). So if
      `RichTextBlock.IsLoaded` is false during template application — which is what the guard
      exists for — a first-render spoiler is never covered.

      Spoilers do work today, so `IsLoaded` is presumably already true at that point and the
      guard is dead weight; worth confirming, then either dropping the guard or setting a
      `_highlightersPending` flag that `OnLoaded` honours. As written the behaviour depends on
      an undocumented ordering.

- [ ] **Date-driven spoiler fix-up loses deltas and never touches the ranges** — `:2508-2519`
      **[latent]**

      `TextDate.Update` re-derives each spoiler from `OriginalOffset` plus *its own* delta, so
      with two relative dates before the same spoiler the second update overwrites the first's
      shift. And the highlighter ranges themselves are left alone (the commented-out block at
      `:2521-2532`), so the transparent range drifts off the text whenever a date changes
      length (`"59 seconds ago"` → `"1 minute ago"`). The existing `// TODO: get rid of _spoiler`
      (`:1580`) is the real fix.

---

## P3 — cleanup, naming, dead code

- [ ] **The two `GetOrCreateRun` overloads are the same 80 lines twice** — `:618-699` and
      `:701-782`. The only difference is `text.Substring(offset, length)` vs `text` on the
      pooled path; the non-pooled path already forwards to two `NativeUtils` overloads. The
      range overload can call the other with the substring, or both can share a private
      `ApplyRunProperties(direct, run, ...)`.

- [ ] **`Clear()` has no callers** — `:386-396`. Everything now goes through
      `MessageTextBlock.Clear` → `ClearBlocks`. It is also wrong if resurrected: it nulls
      `_query`/`_spoiler` without calling `ApplyHighlighters`, so the stale highlighters stay on
      the `RichTextBlock`, and it leaves `_cached`/`_marked`/`_selection` alone.

- [ ] **The `SetQuery(string.Empty)` calls in the cells are now no-ops** —
      `ChatCell.xaml.cs:1526`, `ForumTopicCell.xaml.cs:557`,
      `BusinessChatLinkCell.xaml.cs:37`, `ChatPinnedMessage.xaml.cs:422`,
      `MessageReply.xaml.cs:433`, `ProfileHeader.xaml.cs:1054/1391`. With `_query` null, the
      guard at `:466` returns immediately; they only existed to trigger the old "apply
      highlighters" pass.

- [ ] **`SetFontSize` only reaches the first paragraph** — `:451-459`. It sets
      `TextBlock.Blocks[0].FontSize`, so a multi-paragraph block keeps the old size on
      paragraphs 2..n; and on the fast path the `Run` carries its own `FontSize` (`:977`), which
      wins over the paragraph's, making the call a no-op there.

- [ ] **`Selectable.cs`'s header comment contradicts `WalkInlines`** — `Selectable.cs:38-40`
      says the highlighter space counts "1 per inline object (custom emoji, image, math)", but
      `case InlineUIContainer: break;` (`:394-395`) counts 0, which is what `SetText`'s
      `Map(..., 1, entity.Length) // emoji (container=0 rendered) + ZWNJ` (`:1406`) assumes. The
      code is self-consistent; the comment is the thing that will mislead.

- [ ] **Inline mode misses `textOffset` on the query highlight** — `:505` vs `:1280`
      **[latent]**. The spoiler branch adds `_spanForInlines.ContentStart.OffsetToIndex(TextBlock)`
      to its ranges; the query branch doesn't, so a search highlight in a `ChatCell` brief would
      land short by the length of the `"Fela: "` prefix. Latent only because no caller passes a
      non-empty query to an inline-mode block today (the only real query is
      `MessageBubble` → `MessageTextBlock`).

- [ ] **`RenderedToStyled`/`StyledToRendered` are linear** — `Selectable.cs:278/311`. One
      segment per run, scanned per pointer move during a drag (`GetSelectionBoundary`,
      `GetSelectedText`). Segments are sorted and non-overlapping in both spaces, so a binary
      search is a two-line change; worth it for long code blocks, where the segment count is the
      token count.

- [ ] **`WalkInlines` allocates an enumerator per level, per pointer move** —
      `Selectable.cs:367`. `foreach` over `InlineCollection` goes through the projected
      `IEnumerable<Inline>`. Indexing is not obviously better (each `[i]` is its own interop
      call) — measure before changing, but note it sits on the same path `_contentLength`
      (`:85-89`) was cached for.

- [ ] **`OnHyperlinkForegroundChanged` and `OnCodeForegroundChanged` are identical** —
      `:2252-2261` and `:2281-2290`. Both walk **all** hyperlinks and recolor those whose
      `Foreground` matches the old value, so if the link brush and the code brush are ever the
      same instance, one property change recolors both kinds. Tag the hyperlink kind, or keep
      the code links in their own list.

- [ ] **Naming, while in here** — `var hyperlink = GetOrCreateSpan(direct)` for spoilers
      (`:1201`, `:1268`), `foreach (var hyperlink in _spoilers)` over `TextStyleSpoiler` structs
      (`:1732`, `:1796`), and `TextStyleRun Yolo` in the `RelativeDateService` signatures
      (`:2559`, `:2565`). Also `GetNextUpdateInterval` (`:2600`) updates the run texts as a side
      effect of a `Get`.

- [ ] **`Blocks` + re-templating** — `:208-212` / `:231-240`. `_blocks` is never cleared after
      its paragraphs move into `TextBlock.Blocks`, and the loop casts with `as Paragraph`
      without a null check, so a second `OnApplyTemplate` (theme/style change) would either
      re-parent the same `Paragraph`s or add `null`.

- [ ] **`TextBlock` is assumed non-null outside `SetText`** — `MeasureOverride:263`,
      `UpdateSpoilers:1713`, `InvalidateSkeleton:2426`. All three read `TextBlock.FontSize` only
      when `AutoFontSize` is false, which is why it hasn't fired; `SetText` guards with
      `_templateApplied` but these don't.

- [ ] **`HasLineEnding`'s `InvalidateMeasure` is commented out** — `:173-184`, read by
      `MessageBubblePanel.cs:253`. Fine while `SetText` always precedes measure; a
      `SetText` after layout (spoiler reveal, relative-date rebuild) won't re-measure the bubble.
