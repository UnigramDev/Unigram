# ChatHistoryView — ItemsUpdatingScrollMode

Findings from a read-through of `Telegram/Controls/Chats/ChatHistoryView.cs`, its writers in
`DialogViewModel{,.Handle}.cs` and `DialogEventLogViewModel.cs`, its readers in
`ChatView.xaml.cs`, `ChatView.Bubbles.xaml.cs` and `MessageBubble.xaml.cs`, and the mirror that
actually delivers the mutations, `Telegram/Collections/SynchronizedList.cs`.

**All of it is implemented and compiles (Modern, Debug x64); none of it has been run.** The
sections below are kept as written so the reasoning survives; what actually landed is at the foot
of the file, along with what is worth watching for when it is first exercised.

---

## How it works today

`ItemsStackPanel.ItemsUpdatingScrollMode` is one bit naming the edge the scroll offset is
anchored to across a collection change:

- `KeepLastItemInView` — anchored to the end. Prepending at 0 does not move the view; appending
  pulls the view to the bottom. This is what gives stick-to-bottom.
- `KeepItemsInView` — anchored to the start. Appending does not move the view; prepending pushes
  everything down.

The rule is therefore *flip the bit to match whichever end the next mutation touches, then
mutate*. `ChatHistoryView.SetScrollingMode` is the only writer; `DialogViewModel.SetScrollMode`
is a forwarder. Callers:

| Site | Compensating for |
| --- | --- |
| `DialogViewModel.cs:916/921` | `RawInsertRange(0, ...)` / `RawAddRange(...)` in `LoadNextSliceAsync` |
| `DialogViewModel.cs:1317` | the mode `LoadMessageSliceImpl` derived from the requested alignment |
| `DialogViewModel.cs:1789`, `DialogEventLogViewModel.cs:144/191` | scheduled and event-log loads |
| `DialogViewModel.cs:1861/1876` | `ScrollToBottom` / `ScrollToTop` |
| `DialogViewModel.cs:2355`, `ChatView.xaml.cs:735` | navigation-time default |
| `ChatHistoryView.cs:319` (`LoadPreviousSlice`) | every scroll frame, from `OnViewChanging` |
| `ChatHistoryView.cs:186/246` | sponsored message insertion |

`InsertMessageInOrder` — the path every incoming message takes — sets nothing and inherits
whatever the bit happens to be.

The bit is also **read**, as "which edge is the layout anchored to", by the size-change
compensation in `ChatView.Bubbles.xaml.cs:69`, `:1554`, `:1593`, `ChatView.xaml.cs:622` and
`MessageBubble.xaml.cs:2752`. It is not only an insertion hint.

**Why it is brittle**, in one sentence: it is an implicit global whose correctness rests on
temporal coupling — whoever mutates the collection next must have been preceded by whoever set
the bit — with no link between the two, three writers on unrelated triggers, and a mirror
collection that can deliver the mutation a frame after the bit was set.

---

## Task 1 — Cleanups with no behaviour change

- [x] **1.1** `ChatHistoryView.cs:347` compares against the wrong variable:

  ```csharp
  if (_currentMode == _pendingMode)
  ```

  `_pendingMode` here is leftover state from an earlier deferred call, not the argument. The
  block is only reached once the panel exists, so it fires when a call landed before the panel
  was ready (stashing a pending mode) and a second call arrives before `OnLoaded` drains it. If
  the stale pending equals `_currentMode`, the **new** mode is silently dropped. Reads as if it
  were meant to be `_currentMode == mode`.

- [x] **1.2** Even the intended check is redundant: `if (panel.ItemsUpdatingScrollMode != mode)`
  two lines down does the same job correctly, by reading the panel instead of a shadow copy.
  `_currentMode` has no other reader — delete the field and the early-out together rather than
  fixing 1.1 in place.

- [x] **1.3** Half the method is unreachable. Every live call site passes `force: true`; the only
  `force: false` calls in the tree are the two commented-out lines in
  `BidirectionalIncrementalLoader` (`:910`, `:914`). The `|| scroll.VerticalOffset < 200` and
  `|| scroll.ScrollableHeight - scroll.VerticalOffset < 200` proximity heuristics never run.
  Drop the parameter and both clauses, or keep the parameter and delete the clauses — but not
  the current state, where the scariest-looking code in the method is dead.

- [x] **1.4** `ViewChanging` (`:292`) computes `lastSlice` and never uses it.

- [x] **1.5** `LoadPreviousSlice` loads nothing — it only sets the mode. The loading moved to
  `BidirectionalIncrementalLoader`. Rename or inline it.

- [x] **1.6** `ViewModel` is dereferenced unguarded in `SetScrollingMode` (`:357`),
  `OnDirectManipulationStarted` (`:161`) and `OnPointerWheelChanged` (`:235`), while
  `ViewChanging` just above null-checks it. The pending path (`OnLoaded` → `SetScrollingMode()`)
  can reach `:357` before `Messages.ViewModel` is assigned.

---

## Task 2 — The bit is slammed on every scroll frame

- [x] **2.1** `ChatHistoryView.cs:298`:

  ```csharp
  if (direction != PanelScrollingDirection.Backward && panel.LastCacheIndex == ViewModel.Items.Count - 1)
  ```

  The trigger is `LastCacheIndex`, not the viewport. The cache reaches well past the visible
  range, so this forces `KeepLastItemInView` while the user is still comfortably above the
  bottom — after which an incoming message yanks the view down. The condition asks "is the end
  realized" where it means "is the user at the end"; `IsBottomReached` (`:38`) already answers
  the second question.

  **Reproduce before fixing.** Scroll up by roughly one viewport in a busy chat and have someone
  send a message.

  This is not only a bug: it is the `pinned` flag of Task 4, in the wrong units. The intent —
  "the user is following the end, so keep anchoring there" — is right, and it is the reason
  stick-to-bottom works at all today. Fixing it in place and fixing it properly are the same
  work, so consider doing 2.1 as `pinned` directly rather than twice.

- [x] **2.2** Same line: `panel.LastCacheIndex` indexes the ListView's `ItemsSource` — `_messages`,
  a `SynchronizedList` that is *reversed* in the saved-messages tab and can lag the source by a
  frame. It is compared against the view model's count. Compare against the list's own count.

---

## Task 3 — Set-then-mutate is not actually guaranteed

- [x] **3.1** `SynchronizedList.OnCollectionChanged` (`:238-256`) applies an `Add` straight
  through *unless* a capture-worthy `Remove` is queued for the dust effect, in which case it
  queues behind it and applies up to a frame or 100 ms later. So `SetScrollMode(...)` followed
  immediately by `RawInsertRange(...)` is only tightly coupled most of the time; any scroll event
  in that window re-runs 2.1 and flips the bit before the panel ever sees the insert.

  Nothing to fix here on its own — this is the argument for Task 4.

- [x] **3.2** Pending state is never cleared on unload, and only `OnLoaded` drains it, and only
  if `ItemsPanelRoot != null` at that moment. Navigate away with a pending mode and a stale one
  is applied on the way back.

- [x] **3.3** Three overlapping owners: `BidirectionalIncrementalLoader` was evidently meant to
  own this (its calls are commented out), `DialogViewModel.LoadNextSliceAsync` actually does it,
  and `ChatHistoryView.ViewChanging` does it a third time on a different trigger. Settle on one.

---

## Task 4 — Ask the question the panel is actually being asked

The mode is not a setting. It is the answer to one question, asked once per mutation:

> the collection is about to be mutated — is that mutation inside the viewport, and on which
> side of it?

with one term in front of it, which is what a chat is actually for: **is the list pinned to the
end?** If it is, the user is following the conversation and new messages must appear without
being scrolled to.

```
anchor = pinned || i <= first ? KeepLastItemInView : KeepItemsInView
```

`pinned` needs no case analysis: end-anchoring is correct for *every* mutation while pinned. A
prepend compensates and leaves the user at the bottom; an append reveals the new message. It is
the dominant case and it costs one term.

The index comparison only has to answer for the *un*pinned case — the user reading history above,
where the invariant is that content already on screen must not move. With `first`/`last` the
visible range:

| Where | Anchor | Why |
| --- | --- | --- |
| `i <= first` | end (`KeepLastItemInView`) | the content above changes height, the offset has to absorb the delta |
| `i > last` | start (`KeepItemsInView`) | nothing above changes, so nothing moves |
| `first < i <= last` | start | something must move; the convention is that the rows below the mutation point do |

So a single comparison, `i <= first`. "Direction" is that same comparison — it is what the four
hand-rolled `direction` computations in `AnimateSizeChanged` and `MessageBubble.ComputeDirection`
are reconstructing after the fact, complete with a `TODO: I'm not sure it's correct`.

What this buys beyond deleting the writers: **mutations nobody currently sets the bit for come
out right.** `MoveMessageInOrder` is a remove followed by an insert, and the two can need
opposite anchors; today both inherit one global bit. Same for any mid-list insert — a scheduled
message going live, a date or topic separator materialising, the sponsored message.

### `pinned` is the part to get right

It is the only state left, so it carries the whole design.

- **It has to be sticky, not a live query at apply time.** Inertial scrolling, the `Suspend`
  /`Resume` window inside `ScrollToItem`, and the deferred flush in Task 3.1 all read a transient
  offset. What is wanted is "the user last came to rest at the end", updated on
  `ViewChanged` with `IsIntermediate == false` and on `DirectManipulationCompleted`.
- **`IsBottomReached` (`ChatHistoryView.cs:38`) is too strict to reuse as-is.** It is
  `VerticalOffset.AlmostEquals(ScrollableHeight)`, and `AlmostEquals` defaults to `epsilon = 1e-5`
  (`Extensions.cs:963`) — exact equality in practice. A pixel of overscroll bounce, a fractional
  DPI offset or a composer mid-resize would silently unpin. It needs a real threshold of a few
  DIPs. Note this is *not* the 200 px of 1.3, which was asking a different and wrong question at
  a different time.
- **Unpinning must be the user's act, never a layout event.** A message arriving, the composer
  growing, or a slice loading all change `ScrollableHeight`; none of them mean the user stopped
  following.
- **Tall messages.** Pinned + append of a message taller than the viewport lands its *bottom* at
  the viewport bottom, cutting off the top. `LoadMessageSliceImpl` already knows about this case
  (`DialogViewModel.cs:1630`, "it might be taller than the window height"). Decide whether pinned
  means "bottom of the list visible" or "top of the new message visible".

### Traps

- **Index space.** `Inserting`/`Removing` are handed `pending.SourceIndex`, but panel indices are
  mirror indices, and the two diverge in the saved-messages tab (`_reverse`). `ChatView.Removing`
  (`ChatView.xaml.cs:611`) already compares that source index against `panel.FirstCacheIndex` and
  `LastCacheIndex` — check that case before building on it. Either `Apply` passes `pending.Index`
  as well, or the anchor is decided inside `Apply`.
- **Staleness within a batch.** `panel.FirstVisibleIndex` does not update until layout runs, so
  the second and later mutations of one flush would compare against a stale value. That is what
  `_messagesShift.Translate` exists for — reuse it rather than reading the panel raw.
- **The header.** `ListView.Header` (`SavedMessagesTabHeader`) — confirm whether it participates
  in the panel's index space before trusting any comparison against `FirstVisibleIndex`.

### Steps

- [x] **4.1** Give `SynchronizedList.Apply` (`:283`) the mirror index and the visible range, and
  have it set the anchor from the comparison above immediately before `InsertRange`/`RemoveRange`.
  It is the single funnel every mutation passes through to reach the panel, it already accounts
  for the reversal, and it runs on the frame the panel will process the change — which removes
  the pending/deferred machinery, the saved-messages inversion in `SetScrollingMode`, and every
  ordering hazard in Task 3.

- [x] **4.2** Introduce `pinned` per the section above, and short-circuit 4.1 with it. This
  replaces what `ViewChanging`/`LoadPreviousSlice` is attempting in 2.1 — same intent, measured
  against the viewport instead of the realized range.

- [x] **4.3** Delete the ~15 writers. `SetScrollingMode` becomes private, or disappears.

- [x] **4.4** Have the animation sites read the anchor that was just computed for the mutation in
  flight, rather than recomputing a direction from the panel bit. Fold
  `MessageBubble.ComputeDirection` and both `AnimateSizeChanged` overloads onto it.

---

## What landed

`ItemsUpdatingScrollMode` is no longer set by anyone outside the control. Two things replaced the
fifteen writers, both on `ChatHistoryView`:

- **`IsFollowingEnd`** — sticky, sampled in `UpdateFollowingEnd` from `OnViewChanged` (only when
  `IsIntermediate` is false) and `OnDirectManipulationCompleted`, against a `FollowThreshold` of 8
  DIPs. `SetFollowingEnd(bool)` is the explicit form for the paths that know their intent, and it
  pushes the edge to the panel immediately because a reset never reaches `PrepareAnchor`.
- **`PrepareAnchor(index, delta)`** — called from `ChatView.Inserting`/`Removing`, which
  `SynchronizedList.Apply` raises immediately before it mutates. Picks the edge from
  `IsFollowingEnd ? !IsReversed : index <= first`, and keeps `first` current by hand between layout
  passes (`InvalidateAnchor` clears it from `ItemsPanelRoot_LayoutUpdated`).

`ISynchronizedListDelegate<T>.Inserting`/`Removing` now carry the **mirror** index rather than the
source index — `Pending.SourceIndex` is gone, nothing wanted it. That also fixes the trap noted
above: `ChatView.Removing` was comparing a source index against `panel.FirstCacheIndex`, which only
agreed with itself outside the saved-messages tab.

Translations of the old writers, rather than deletions, where intent existed:

| Was | Is |
| --- | --- |
| `SetScrollMode(..., true)` before `RawReplaceWith` / `ReplaceWith` (a reset) | `SetFollowingEnd(...)` |
| `LoadSliceResult.ScrollMode`, derived from the alignment | `LoadSliceResult.IsFollowingEnd` |
| navigation default, `ChatView.Update` | `SetFollowingEnd(true)` |
| `ScrollToBottom` / `ScrollToTop` followed by a mode set | folded into the control's own methods |
| the two sponsored-message insertions | `SetFollowingEnd(false)` — the ad is revealed by the user pulling past the end, so it must not pull the view |

Deleted outright, because the edge is now derived per mutation: the pair around
`RawInsertRange`/`RawAddRange` in `LoadNextSliceAsync`, the empty-response case beside them, the
prepend in `DialogEventLogViewModel.LoadNextSliceAsync`, `LoadPreviousSlice` and the body of
`ViewChanging`, the whole pending/`_currentMode` machinery, and the commented-out calls in
`BidirectionalIncrementalLoader`.

`ChatHistoryView.GetShiftDirection` is the one copy of the direction computation, used by
`MessageBubble.ComputeDirection` and `ChatView.Bubbles.AnimateSizeChanged`. The removal site in
`ChatView.Removing` deliberately still computes `edge` itself: it stores a flag for later, not a
direction, because the row is gone by the time the shift is animated.

## Worth watching on the first run

- **The 8 DIP threshold** is a guess. Too small and the list stops following after a bounce; too
  large and it follows when the user deliberately nudged up a little.
- **Tall messages.** Still open, and untouched: a pinned append of a message taller than the
  viewport lands its bottom at the viewport bottom.
- **`_anchorIndex` between layout passes.** A removal range straddling the viewport edge moves it
  approximately. Layout corrects it, so the worst case is one mutation anchored to the wrong edge.
- **The saved-messages tab**, where `IsReversed` inverts both the measurement and the pinned edge,
  and where the index bookkeeping was demonstrably wrong before.
- `ViewChanging` now only assigns `ScrollingDirection`, which nothing reads, and `OnSizeChanged`
  calls it for no remaining reason. Left alone rather than widening the diff.

---

## First run — the drift, and what it cost

Fela: *"the anchoring drifts a lot when scrolling up, it jumps down of a viewport or so at times
when new messages are loaded"*, with the guess that it happens once `Count > 200` and items are
removed from the bottom. It does, and the guess names the trigger exactly.

**One anchor per layout pass, not per mutation.** `PrepareAnchor` answers for every mutation, but
`ItemsStackPanel` reads `ItemsUpdatingScrollMode` when it measures, so a run of mutations with no
layout between them gets a single answer — whichever the last one wrote. `ChatView`'s own
`ItemsPanelRoot_LayoutUpdated` already reads the bit that way for `_messagesShift`, one value for
the whole accumulated batch. That does not invalidate the per-mutation framing of Task 4, but it
does bound it: the batch is the unit, and the writers inside one have to agree.

`LoadNextSliceAsync` made them disagree. A backward load was 24 inserts at index 0 — above the
viewport, so end-anchored — followed by ~24 `RemoveAt(Count - 1)` trimming back to 200, below the
viewport, so start-anchored, and last. The whole batch therefore anchored to the start and the
prepended height went uncompensated. `HistoryLimit` is 24, which at 40-60 DIPs a row is the
viewport Fela saw it jump by. It only starts past 200 items, about eight scroll-ups in, which is
why it is intermittent. Forward loads have the mirror shape (append at the end, trim from index 0)
and would yank the view to the bottom instead.

Two changes:

- **The trim moved to before the request** (`DialogViewModel.cs`, right after the `fromMessageId`
  guard, threshold `Count + HistoryLimit > 200` so the cap after the load is unchanged). The await
  is what separates the two batches; each is then homogeneous, and the trim's removals are far
  outside the viewport on their own frame. The `IsNewestSliceLoaded`/`IsOldestSliceLoaded` reset
  moved with it and stays correct even if the response comes back empty — the rows are gone either
  way. The cost is trimming on speculation: an empty response drops 24 rows for nothing, at the far
  end of a 200-row history.
- **`_anchorEnd` latching within a batch** — proposed, **not committed**, and still not in the tree.
  Once a mutation lands at or above the first visible index the batch would be end-anchored and a
  later one below the viewport could not take it back: uncompensated content above moves what the
  user is reading, while a mutation below only fails to be absorbed. `IsFollowingEnd` would stay
  assigned rather than latched, being the one term that can legitimately change between two
  mutations of a batch that spans frames. Reordering the trim removed the batch that made this
  visible, so it is an open question rather than a known bug; see the second run below for how to
  tell.

Still open from the list above: the 8 DIP threshold and tall messages, both untouched.

### The other end of the same hole

Fela, straight after: *"the same should happen on UpdateNewMessage, otherwise a chat will keep
increasing forever when receiving messages without scrolling up"*. Right — the trim only ever ran
on a slice load, so a chat left open at the bottom grew for as long as messages arrived.

`TrimHistory(direction, room)` in `DialogViewModel.cs` is now the one implementation, and
`HistoryWindow` the one 200. `InsertMessage` — the funnel for every message that joins the newest
slice, incoming and sent alike — calls it through `TrimHistoryAfterInsert`, which adds the two
conditions that make it safe:

- **Only while the list follows the end.** The trim runs in the same layout pass as the insert, so
  the two share one anchor. While following, both want the end and nothing moves. While the user is
  reading above, the removal at index 0 is above the viewport and the insert below it, which is the
  disagreement this whole note is about — so it is simply not trimmed there, and the growth is
  bounded by how long the user reads before returning to the end, when the next message trims the
  backlog in one go.
- **Never for scheduled messages.** That list is the whole thing rather than a window into one, and
  nothing would load a trimmed one back.

`TrimHistoryAfterInsert` lives in `DialogViewModel.cs` rather than at the call site because
`PanelScrollingDirection` is `Windows.UI.Xaml.Controls`, which `DialogViewModel.Handle.cs` does not
import — not worth widening its usings for one call.

---

## Second run — following the end of the window instead of the end of the chat

Fela: *"when scrolling down (from old history to new history) the scroll stays attached to the end
instead of moving to the top, causing tons of pages to be loaded and rendered"*, and *"this happens
when scrolling fast towards the bottom"*.

`UpdateFollowingEnd` asked only where the offset was, never whether there was anything left to load
below it. A fast scroll outruns the loader and comes to rest at the bottom of the *loaded window*,
which is a paging boundary and not the end of the chat — so `IsFollowingEnd` latched true there.
From that point every mutation was end-anchored, including the forward slice's own append: each
page pulled the view onto its last message, `CheckContinueLoading` saw `distanceFromBottom ≈ 0`,
and the list raced to the present a page at a time, measuring and realising all of them. The
scroll mode it replaced could not do this — `LoadNextSliceAsync` set `KeepItemsInView` by hand
before `RawAddRange`, so an append never moved the view.

The term was always meant to be "the user is following the conversation", and there is no
conversation to follow while the newest slice is missing: `InsertMessage` drops an arriving message
outright unless `IsNewestSliceLoaded == true`. So `UpdateFollowingEnd` now requires it, and the
offset test only decides the case where it holds. `IsNewestSliceLoaded` rather than
`HasMoreItemsAtBottom`, which names a visual edge and inverts in the saved-messages tab, where
following the end still means following the newest message.

Sampling on `ViewChanged` alone is then not enough: the load that makes the newest slice available
lands without the view having moved, so nothing would ask again and the list would stop following
the end just as it arrived there. `LoadNextSliceAsync` re-samples in its forward branch.

`SetFollowingEnd` is deliberately left unguarded — it is the explicit form, and navigation calls it
before anything is loaded, when `IsNewestSliceLoaded` is still null.

### Logging

There was none. What the log now carries, all at rates bounded by user interaction:

| Where | Line |
| --- | --- |
| `BidirectionalIncrementalLoader.LoadItems` | direction, offset/scrollable, item count, loads in flight |
| `DialogViewModel.LoadNextSliceAsync` | direction, item count, whether the list was following |
| `DialogViewModel.TrimHistory` | direction, how many rows go |
| `ChatHistoryView.Set`/`UpdateFollowingEnd` | the new value and the offset it was read at, on change only |
| `ChatHistoryView.InvalidateAnchor` | one line per batch: how many mutations, from which first-visible index, which edge they ended on, and whether they disagreed on the way |

The last is the one to read first. It is emitted from the layout pass that closes the batch rather
than per mutation, because the panel reads `ItemsUpdatingScrollMode` once when it measures — the
batch is the unit that decides where the view lands, and 24 lines per slice load would bury it.
`disagreed` marks a batch whose writers asked for opposite edges, which is the failure mode the
trim reordering above was for.

### The latch, still open

`PrepareAnchor` still lets a later mutation in a batch take the edge back from an earlier one — only
the trim half of the first run's pair was committed (`a7e6c3644`). `disagreed` in the new batch line
is what answers whether any batch still needs the latch.
