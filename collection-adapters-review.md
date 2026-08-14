# Collection adapters

`SearchCollection`, `IncrementalCollectionView` and `MediaCollection`. Written up after a crash
in `SearchCollection` (PR #3361) turned into a read of the whole area.

## What these are for

The problem they solve: a `ListView` should keep binding to one collection instance for the life
of the page, while the collection *behind* it gets replaced — a new search query, a different
sender filter, a reload. Swapping `ItemsSource` is the expensive way out (the whole plumbing is
rebuilt, virtualization restarts, every container is thrown away); `Clear()` still costs a Reset
and every container. Assigning per index is the cheap one, because the container survives and
only the bindings re-evaluate. Scroll position and item animations come along for free.

So these are adapters, close in spirit to XAML's `CollectionViewSource`: they sit between a
source collection and the list. The difference from `CollectionViewSource` is that **setting the
source morphs the contents in place instead of raising a Reset.**

The source is not an implementation detail. It is owned elsewhere, it stays live, and its items
keep being added, removed and changed after the transition — so the adapter must also forward
those changes for as long as it is attached. That is the second half of the contract and the one
that is currently leaky.

## There are two implementations of the same adapter

| | `IncrementalCollectionView` | `SearchCollection` |
|---|---|---|
| Adapter | yes | yes, separately implemented |
| Transition | `ReplaceSource`: tail adjust, then per-index replace, skipping items `CompareItems` says are equal | `DiffUtil.CalculateDiff` (LCS) on the thread pool, then `ReplaceDiff` |
| Search query | — | `Query` with debounce, factory per query, `UpdateSender`, `Reload` |
| Loading | forwards `LoadMoreItemsAsync` to the source | `_loading`/`_replacing`/`_initialized` bools |

**They differ by one real feature — the query driver — and duplicate everything else.**
`IncrementalCollectionView.ReplaceSource` is the newer and cheaper transition; it is what the
adapter should do everywhere. Keeping an item's existing reference when it compares equal is
load-bearing, not just an optimisation: the same object in both lists is what makes item-level
property changes propagate with no plumbing.

The LCS diff in `SearchCollection` is the wrong instrument for this goal. It buys a minimal edit
script including `Move`, and `Move` is among the more expensive notifications a `ListView` can
receive. When two result sets barely overlap — `"a"` to `"ab"` — it pays for the LCS and then
emits N removes plus M adds, which is more layout work than the Reset it set out to avoid.

## Who uses what

`IncrementalCollectionView` (adapter only):

- `ChatStoriesViewModel.ItemsView`
- `ProfileGiftsTabViewModel.ItemsView` and `.Items` — nested, one view over another
- `SettingsProfileColorViewModel.ItemsView` (non-generic form)

`SearchCollection`:

| Consumer | Search box bound to `.Query` |
|---|---|
| `SearchChatsViewModel` | yes |
| `MediaTabsViewModelBase` | yes — Files, Links, Music, Voice tabs |
| `SupergroupMembersViewModelBase` | yes |
| `DownloadsViewModel` | yes |
| `SendLocationViewModel` | yes |
| `BusinessBotsViewModel` | yes |
| `ChatAffiliateViewModel` | **no** — only `UpdateSender` and `Reload` |
| `ProfileGiftsTabViewModel` | **already migrated** — its `SearchCollection` line is commented out |

Six consumers genuinely need the query driver. `ChatAffiliateViewModel` wants source-swapping
without a search box, which is `IncrementalCollectionView`'s job. `ProfileGiftsTabViewModel` has
already moved, which suggests the convergence was already under way.

## Defects

### `SearchCollection`

- **The diff ran on the thread pool over live UI-thread collections.** `CalculateDiff` copies
  both, so `Array.Copy` ran over a list whose backing array and count were being replaced.
  Fixed in **#3361** by snapshotting first, but the snapshot only exists because the diff is
  asynchronous — it disappears with the diff.
- **`_loading` has two writers.** `UpdateImpl` sets it as a gate; `LoadMoreItemsAsync` clears it
  on completion, including when it was not the one that set it. An in-flight load therefore
  reopens the gate in the middle of an update. This is the root of #3361 and it is still there.
- **`UpdateImpl` is `async void`.** Any failure inside becomes a process-level crash rather than
  a failed search — that is how #3361 reached the unhandled handler.
- **Source changes are dropped during a transition.** `OnCollectionChanged` is detached at the
  top of `UpdateImpl` and reattached only after the diff, with a network load and a `Task.Run`
  in between. For a conduit, losing changes from the collection it is proxying is a contract
  violation, not an edge case. Very likely the cause of
  `// I'm not sure in what conditions this can happen, but it happens` and its recursive
  self-call.
- **`OnCollectionChanged` has no `Replace` case.** Add, Remove, Move and Reset are handled, so an
  item replaced in the source silently never reaches the view.
- **`HasMoreItems` is a getter with a side effect** — it sets `_initialized`, which is what
  decides whether an update diffs at all. Invisible at every call site.
- **`UpdateItems` writes back into the source** (`_source[item.NewSeqIndex] = item.OldValue`) to
  alias references, using an index computed before the diff. If the source grew, that write is
  out of range.
- **Two cancellation sources that do not compose.** `Cancel()` replaces `_cancellation`; the
  `Query` setter also cancels and replaces it for the debouncer, so a scroll-driven load cancels
  a pending debounced query as a side effect. Both are only ever asked "did something newer
  start?", which is a generation counter wearing a `CancellationTokenSource`.
- **Cancellation is checked but the work is not cancellable.** The load and the diff run to
  completion and the token is tested afterwards, by which point the load has already appended.
- `LoadMoreItemsAsync`'s body is mostly commented out.
- `Cancel()` is public and nothing outside calls it.

### `IncrementalCollectionView`

- **`SetSourceAsync` has the detach window, and worse.** It unsubscribes from the *old* source at
  the very top, before awaiting the new source's first page — so the visible list stops updating
  the moment a transition starts, and changes in that window are lost.
- **`OnCollectionChanged` has no `Move` case.** Add, Remove, Replace and Reset are handled. Note
  this is the mirror image of `SearchCollection`, which handles Move but not Replace; a merged
  implementation needs all four.

### `MediaCollection`

- **`LoadMoreItemsAsync(uint count)` shadows its own parameter** with `var count = 0u`, so the
  caller's requested count is discarded and it always asks for 50. Legal C#, silently ignored.
- **No error path.** If `SendAsync` returns an `Error`, neither response branch runs, nothing is
  added, and `_hasMore` keeps its previous value — a transient network failure is
  indistinguishable from reaching the end of the list.
- **The cancellation token is received and never used**, so an abandoned load still runs to
  completion and still appends.
- **Per-item `Add`** raises one notification each; it derives from `ObservableCollection` rather
  than `MvxObservableCollection`, so it cannot batch.

## Proposed direction

Converge rather than rename. `SearchCollection` keeps its name and its public API — six view
models depend on it — but owns only what is about search: `Query`, the debounce, the factory,
`UpdateSender`, `Reload`. The adapter behaviour comes from `IncrementalCollectionView`.

That deletes the LCS diff, the `Task.Run`, the snapshot added in #3361, `_loading`, `_replacing`,
`_initialized`, the `async void`, the two tangled cancellation sources and the reentrancy hack —
because they exist to manage an asynchronous diff that no longer happens.

Two things to fix while converging:

1. **Handle all four notification actions** in the merged mirror: Add, Remove, Replace, Move.
2. **Make the switch atomic.** Stay attached to the old source while the new source's first page
   loads — the user keeps seeing a live list, which is the right behaviour anyway — then detach,
   morph and attach in one synchronous UI-thread step. No window, so nothing to buffer or replay,
   and `_replacing` stops existing. This is only possible because the morph is synchronous; the
   `Task.Run` is what forces the window open today.

Then move `ChatAffiliateViewModel` to `IncrementalCollectionView`, since it never searches.

## Open decisions

- **During the load of a new source's first page, the user sees the old list, live.** Right, or
  should it show a loading state? Today it is the old list, by accident rather than design.
- **Is the morph ever the wrong shape?** It is worse than a diff for prepend and reorder — a new
  item at the head re-binds everything below it. Media lists append at the tail; `DownloadsViewModel`
  inserts in the middle, but through the mirror rather than through a transition. Worth confirming
  across all consumers before committing.
- **Does `Source` stay publicly mutable?** `DownloadsViewModel` reaches in and edits it directly
  from update handlers — `Remove`, `Insert`, `UpdateProperties`. While that is true, no invariant
  about the source can be enforced inside the adapter; it can only react.

## Not measured

The claim that the per-index morph beats the diff here is reasoning about notification counts,
not a benchmark. The measurable version is to count the notifications each approach emits for a
real query change on the profile media tab.
