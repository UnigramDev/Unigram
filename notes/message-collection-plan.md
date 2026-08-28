# MessageCollection

`Telegram/ViewModels/Dialogs/MessageCollection.cs`, 641 lines. Two jobs in one class:

- the **live list** behind `DialogViewModel.Items` and the chat `ListView`;
- a **throwaway slice buffer** (`ctor:72`) that a history response is decanted into so that
  attach state and separators are computed off to the side, then copied over by
  `AppendSlice` / `PrependSlice` / `ReplaceSlice`.

On top of `Collection<T>` it maintains three things: an id -> item map (`_messages`, so that
`Handle(...)` can find a message by id), the date and forum-topic separator rows, and the
`IsFirst`/`IsLast` grouping flags of the *neighbours* of every mutation, which it reports back
to the view through `AttachChanged`.

## How hot is it, really

Worth pinning down before optimising anything:

- the list is capped at 200 items (`DialogViewModel.cs:938`);
- a slice is 24 messages (`Constants.HistoryLimit`);
- mutations are one 24-item batch per scroll-to-edge, or one insert per incoming message.

So the per-item CPU inside this class is nowhere near the cost of realising the bubble that
each insert causes. **This is not a CPU bottleneck.** What is worth fixing is (1) the garbage it
produces per message, (2) the fact that the insert logic exists in three near-identical copies,
and (3) a handful of traps and dead ends in the API.

Line numbers below are against the tree as it was before phases 1-2, i.e. on top of the staged
change that rebases the class from `RangeObservableCollection` onto `SuppressObservableCollection`.

## Findings

### A. DONE — `InsertItem` is the same algorithm written three times — 206:390

`_suppressNext` (225), `_suppressPrev` (265) and the fall-through (305) are one algorithm with
two independent halves:

- **prev half**: `UpdateSeparatorOnInsert(prev, item)`, `UpdateForumTopicSeparatorOnInsert(prev, item)`,
  `UpdateAttach`, insert the separators *before* `item`, note `prev`'s attach hash;
- **next half**: the mirror image, separators *after* `item`, note `next`'s hash.

`_suppressNext` runs the prev half, `_suppressPrev` runs the next half, the fall-through runs
both. That is `bool doPrev = !_suppressPrev; bool doNext = !_suppressNext;` and two `if`s —
roughly 185 lines collapse to ~70, with one place to get the insertion order right instead of
three. Every other finding below is easier to land once this is done.

### B. DONE - Garbage on the load path

1. `ObservableCollection.InsertItem` builds a `NotifyCollectionChangedEventArgs` (plus its
   single-item list wrapper) *before* it calls the virtual `OnCollectionChanged` where
   `SuppressObservableCollection` drops it. So both suppressed paths still allocate per item:
   the slice ctor (`87`, nobody is subscribed at all) and `ReplaceSlice` (`193-199`, inside
   `SuppressEvents`). Opening a chat on a 100-message slice throws away ~400 of these.
   `RangeObservableCollection.AddArrangeCore` already avoids this by writing into `Items`
   directly, and both of these paths can do the same.
2. `AttachChanged` is `Action<IEnumerable<MessageViewModel>>` (`61`), so each notified insert
   or remove allocates a 1- or 2-element array (`262`, `302`, `383`, `433`) and then an array
   enumerator in `ChatView.OnAttachChanged`'s `foreach`. It is never more than two items and
   they are always `(prev, next)`, so `Action<MessageViewModel, MessageViewModel>` makes it
   free and also drops the `message == null` filtering at `ChatView.xaml.cs:1088`.

Neither is a bottleneck; both are free to remove and this is a UWP AOT app where GC pressure on
the chat-open path is the thing we do care about.

Fixed by an `InsertCore`/`RemoveCore` pair that writes straight into `Items` when
`EventsAreSuppressed`, with the slice ctor now wrapping its fill in `SuppressEvents()` so it
takes that path too. `ReplaceSlice` emits `Count` and `Item[]` once at the end, which the
per-item `Add` used to emit N times.

### C. DONE — The batch seam keys off the loop index, not off "have we inserted anything yet"

`AppendSlice:145` sets `_suppressOperations = i > 0` and `PrependSlice:174` sets it to
`i < source.Count - 1`. The intent is "the first item I actually insert joins onto the existing
list, so it needs the full treatment; the rest already have their attach state from the slice
buffer". But `i` counts *candidates*, not insertions — if the item at the seam index is dropped
by the filter above it, the next one is inserted raw and the seam gets no attach recomputation
and no date separator.

I could not construct a live repro: the slice ctor's `exclude` list (`77`) already removes the
ids the `_messages.ContainsKey` filter would catch, and the `Id < lastId` / `Id > firstId`
filter does not fire for the offsets `LoadNextSliceAsync` uses. So this is a trap rather than a
bug — but it is one line to make it honest (`bool first = true;` flipped after a real insert).

### D. DONE — `PrependSlice`'s `index` parameter is ignored — 156, 177

The body always does `Insert(0, message)`. Both callers pass 0. Drop the parameter.

### E. DONE — `RawRemoveAt` — 392 — has no callers

### F. `IsEndReached` — 64 — only means anything on the slice instance

It is assigned solely by the slice ctor (`90`) and read solely off a slice
(`DialogViewModel.cs:949,954`). On the live collection it is permanently `false`. It is the
clearest sign that the two roles want to be two types.

### G. The 45-argument synthetic `Message`, written four times

`447` and `465` here, `DialogViewModel.cs:1325,1327,1338`. All positional, all constructing a
header/service row. A schema change silently shifts arguments across all five. One
`CreateSyntheticMessage(MessageViewModel template, MessageContent content, int date)` factory
removes that.

### H. Per-item predicates

- `AreOnTheSameDay` costs two `Formatter.ToLocalTime` calls (a `DateTime.AddSeconds` plus a
  timezone conversion each) and runs about twice per insert. That is sub-microsecond against a
  24-item batch — **not worth caching a local-day on `MessageViewModel`**, which would then need
  invalidating in both `Replace` overloads.
  Worth noting while passing: `Formatter.cs:408` discards the result of
  `DateTime.SpecifyKind`, so `dtDateTime.Kind` stays `Unspecified`. It happens to be correct
  because `ToLocalTime` assumes UTC for `Unspecified`, but the line reads as though it does
  something.
- `AreTogether:585` reads `message1.ClientService.Options.VerificationCodesBotChatId` through
  two interface hops on every call for a value that is fixed for the whole collection. Hoist it
  to a field in the ctor — a clarity fix that happens to be cheaper.

### I. DONE — The suppression flags are ambient and not exception-safe — 21-23

Three `bool` fields cleared by assignment at the end of each range method. If `Add`/`Insert`
throws (`CheckReentrancy`), the collection is stuck in raw mode for the rest of its life and
silently stops maintaining separators. A `readonly struct` scope like the existing
`SuppressEventsDisposable`, or a single enum field set in a `try`/`finally`, closes that.

### J. DONE — Undocumented invariant of `ReplaceSlice`

It runs entirely with `_suppressOperations = true`, so it assumes the source already carries
correct attach state and separators. True today for every caller (they are all slice
collections, or a collection built through `Add`), but nothing says so.

### K. DONE - `ProcessEvents` reverses nothing

`DialogEventLogViewModel:367` ended with `result.Reverse();`. `result` is a `MessageCollection`,
which has no in-place `Reverse` - neither `ObservableCollection<T>` nor `Collection<T>` declares
one - so it bound to `Enumerable.Reverse` and the sequence it returns was discarded. The line
has never done anything.

Deleting it is the fix, not making it work: the `result.Insert(0, message)` loop above already
turns TDLib's newest-first events into the ascending order the chat list wants, so an in-place
reverse would render the event log upside down.

Surfaced by typing `ReplaceSlice`'s parameter, which forces `ProcessEvents` to declare that it
returns a `MessageCollection` rather than an `IList<MessageViewModel>`.

### L. DONE - `LoadScheduledSliceAsync` never built a slice

`DialogViewModel:1793` builds `replied` with `.Select(CreateMessage).ToList()` - a plain
`List<MessageViewModel>` that never went through `MessageCollection`'s insert path. So no
message in it has attach state (they all keep the `IsFirst = IsLast = true` defaults, i.e. every
scheduled message renders as its own ungrouped bubble) and there are no date separators beyond
the single one hand-inserted at index 0.

`ReplaceSlice(MessageCollection)` makes that call impossible to write, which is the point. Now
built through the slice ctor, with `OrderBy(x => x.Id)` flipped to `OrderByDescending` because
the ctor inserts each item at the top and so wants TDLib's newest-first order. Going through the
ctor also switches `CreateMessage` to its `forLanguageStatistics` overload, which is a no-op
here: `UpdateLanguageStatistics` returns early on outgoing messages and every scheduled message
is outgoing.

### M. DONE - `ProcessMessages` / `ProcessAlbums` mutate a slice by index

Both take `IList<MessageViewModel>` and are handed a `MessageCollection` at four call sites, then
walk it with an index while mutating it:

- `slice.RemoveAt(i); i--;` (`DialogViewModel:1916,1930` and `ProcessAlbums`) assumes exactly one
  item disappears at `i`. `MessageCollection.RemoveItem` also drops the date or topic separator
  the removal orphans, and that one can sit at `index - 1` - so the loop can skip the next item.
  The same trap is already spelled out in a comment at `DialogViewModel.Handle.cs:1566`.
- `slice[i] = group` in `ProcessAlbums` goes through `SetItem`, which `MessageCollection` does
  **not** override. `_messages` keeps mapping the replaced message's id to the old item and never
  learns the album's id or its children's.

Neither reaches the live `Items` today - `ProcessMessages` is only ever given a slice or a plain
`List` - so the stale map is thrown away before it matters. Latent, but it is the third thing the
tightened signature points at.

Fixed by deferring instead of walking backwards. Both loops now collect the items to drop and
remove them by identity once the walk is over, so no index survives a removal and nothing has to
reason about how many items `RemoveItem` took. The `List` is only allocated when something is
actually dropped, which is the rare case. Deferral also makes `ProcessAlbums`' `slice[i] = group`
trivially valid, since the slice no longer shrinks underneath it.

The removals were placed immediately after the loop and before the `groups` block, which reads
`first.IsFirst` and `album.Messages[^1].IsLast`: removing there replays the same `UpdateAttach`
sequence the interleaved removals produced, so that block still sees what it saw.

`SetItem` is overridden and maintains the id map only. Its one caller swaps an album root in over
the child that seeded it, so neither the day nor the neighbours change and there is no attach
state to recompute - worth knowing before a second caller appears.

### N. DONE - `OnAttachChanged` gave up after the first neighbour

`ChatView.OnAttachChanged` looped over the reported items, and the `if
(ViewModel.IsSavedMessagesTab)` guard two thirds of the way down was a `return`, not a
`continue`. So on the Saved Messages tab the second of the two neighbours never reached
`bubble.UpdateAttach`, and its grouping stayed stale until something else redrew it.

Splitting the body into a per-message overload - which the two-argument `AttachChanged` wanted
anyway - makes the `return` mean what the `continue` meant. The diff looks large because the
body dedents by one level; `git diff -w` shows the eight real lines.

### O. DONE - the ordering lived outside the collection

`NextIndexOf` in `DialogViewModel.Handle.cs` read `Items.Count`, `Items[i]` and
`Items.ContainsKey` and nothing else - a `MessageCollection` method in the wrong class. Because
it was outside, `InsertMessageInOrder` had to hand an index back across the boundary, and 4425c24
had to patch the case where the index went stale: `RemoveItem` can take the orphaned separator
too, so the collection shrinks by two and the adjusted index lands past the end
(`ArgumentOutOfRangeException`, reached from `PendingMessage_Completed`).

Moved in behind one public `InsertInOrder` that owns the whole move, so no index crosses the
API boundary any more and `MoveMessageInOrder` is gone.

The cost matters here and 4425c24 got it wrong: the code before it did one backward pass and an
O(1) insert; the fix replaced the insert with a *second full pass*. Neither reproducing that nor
splitting the pass in two is acceptable, so:

- The single pass survives verbatim - both indices out of one walk, the ordering position
  normally settled on the first iteration, the rest of the walk spent looking for the message.
- The reinsertion is back to O(1)-ish. A removal only ever shifts rows **down**, so the index
  worked out before it is too high, never too low, and by at most the three extra rows
  `RemoveItem` can take. Walking back from that index settles within those few rows instead of
  rescanning the list. Clamping the start of the walk to `Count` is also what stops the
  `ArgumentOutOfRangeException` - not a blind clamp of the insert, which is what the commit
  message rightly rejected.

`force` came out as `Reinsert`: it was never an ordering concern, it exists so the list builds a
fresh container when a content template changes (expired media), and
`InsertMessageInOrder(message, 0, true)` said none of that.

Three behaviours preserved on purpose, each with a comment:

- **The album asymmetry.** The map is album-aware, but an album is listed under its first
  child's id alone, so a later child answers `ContainsKey` and is then found nowhere by the
  scan. Rewriting the scan as a map lookup - which looks like a clean simplification - would
  start dragging album roots around by their children.
- **`NextIndexOf` returning `Count` when nothing sorts below the message.** That reads like an
  off-by-one but it is what puts sponsored messages at the bottom: they carry a sponsor
  identifier smaller than every real message id, so nothing ever matches.
- **`InsertMessageInOrder` as the entry point.** `ChatHistoryView` calls it for sponsored
  messages, so it stays as a one-line forward rather than making the view reach into `Items`.

## What landed in phases 1-2

Uncommitted, in the working tree on top of the staged base-class change.

- `InsertItem` is one path: 185 lines to 112 including the id-map maintenance and comments.
  `joinPrev`/`joinNext` gate the two halves, which are otherwise the code that was there.
- The three `bool` flags became one `AttachMode` enum (`Both` / `Previous` / `Next` / `None`)
  set inside `try`/`finally`, so a throw can no longer strand the collection in raw mode.
- `AppendSlice` / `PrependSlice` pick the seam off `empty` - "have I inserted anything yet" -
  instead of the loop index, which counted candidates. That is finding C.
- `PrependSlice` lost its ignored `index` parameter; both callers updated.
- `RawRemoveAt` deleted.
- `ReplaceSlice` documents that it recomputes nothing.
- The three take a `MessageCollection` rather than a loose `IList`/`IEnumerable`, and
  `ProcessEvents` declares the `MessageCollection` it was already returning. That is
  findings K, L and M.
- `ProcessEvents`' no-op `result.Reverse()` deleted (K).

Behaviour is meant to be identical throughout, with one deliberate exception: the two
single-sided paths used to notify `AttachChanged` with a one-element array and now send the
same two-element `(prev, next)` array as the both-sided path, with the unchanged side null.
`ChatView.OnAttachChanged` already skips nulls, and phase 3 removes the array outright.

Worth a look while testing: date separators and avatar grouping at the **seam** of a loaded
slice - scroll up to pull in older messages and check the join between the batch and what was
already there, in a chat where the batch crosses midnight, and in a forum topic.

## Plan

Ordered so that each phase stands on its own and can be committed and tested separately.

**1. DONE. Collapse `InsertItem` to one path.** Finding A. Pure refactor, no behaviour change —
worth landing alone so the diff is reviewable against the three existing copies.

**2. DONE. Correctness and API cleanup.** Findings C, D, E, I, J. Small, and each is a one-liner
once A is in: seam flag, drop `index`, delete `RawRemoveAt`, scope the flags, comment the
`ReplaceSlice` invariant.

**3. DONE. Stop index-walking a slice while mutating it.** Finding M. Both loops defer their
removals and remove by identity; `SetItem` is overridden to maintain the id map. Walking
backwards was the other candidate and was rejected: it revisits the item below a removal that
also took a separator, and `ProcessAlbums` accumulates `album.Messages` in encounter order, so
reversing it would reverse the album.

**4. DONE. Stop allocating per message.** Findings B and N. `AttachChanged` is
`Action<MessageViewModel, MessageViewModel>`, and the suppressed paths write into `Items`
directly.

**Extra, done 2026-08-27: move the ordering into the collection.** Finding O, Fela's call
after spotting that 4425c24 patched the symptom.

**5. Factor the synthetic-message construction.** Finding G, plus hoisting the verification-
codes chat id (H). Touches `DialogViewModel` as well.

**6. Optional, larger: split the two roles.** Finding F. A `MessageSlice` that is a plain
`List<MessageViewModel>` carrying `IsEndReached`, built by the same separator/attach engine,
and a `MessageCollection` that is only the live list. This is what makes phase 4's
"write into `Items` directly" unnecessary rather than a special case, and it removes the
`ObservableCollection` machinery from the slice path entirely. Only worth doing if phases 1-5
land first.

Explicitly **not** planned: caching dates or attach state on `MessageViewModel` (H), and
replacing the per-item `CollectionChanged` notifications with a batched one — the `ListView`
needs one notification per item.
