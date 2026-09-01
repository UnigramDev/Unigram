# Ordered collections: model + windowed view

Resume doc. The chat list, group call participants and the topic lists all do the same thing —
an ordered source that pages, and an observable window over the top of it — in five to eight
copies of varying quality. This is the extraction of that, plus the incremental-loading contract
underneath it.

## Committed

- **`Make HasMoreItems part of the incremental loading contract`** — `IIncrementalCollectionOwner`
  returns `IncrementalLoadResult(Count, HasMoreItems)`; `IncrementalCollection` owns the flag,
  clamps a load that adds nothing while claiming more (3 strikes), coalesces in-flight loads,
  and bumps a version on `Restart`. 33 owners migrated.
- **`Remove LegacyIncrementalCollection`** — folded into `IncrementalCollection`;
  `ChatMemberGroupedCollection` was dead code.
- **`Split range operations out of DiffObservableCollection`** — `RangeObservableCollection`;
  ~90 declarations no longer instantiate DiffUtil.
- **`Reimplement delete chat undo within ClientService`** + **`Refactor PaidReactionService`** +
  **`Clear pending delete chats on reset`** — `AddPendingDeleteChat` /
  `RemovePendingDeleteChat` / `CommitPendingDeleteChat`, `DeleteChatService`, and
  `UndoToastPopup` shared with the two paid-reaction services.

Then the pair itself, one commit per layer and one per list:

- **`Add a drain for updates applied on a dispatcher`** — `DispatcherDrain<T>`: queue, one post per
  burst, batch apply, reset-then-recheck.
- **`Add a windowed collection over an ordered source`** — `WindowedCollection<TItem, TKey, TOrder,
  TArgs>`: the placed-order map, `IsWithinWindow`, `NextIndexOf(out prev)`, per-key coalescing
  through `Merge`, `RemoveItem` dropping the order with the row, `Restart` dropping the queue.
- **`Add a base for the ordered lists TDLib pages`** — `OrderedSourceService<TItem>`: the sorted
  set, the placed order, the reentrant pager, and `Changed`.
- **`Split the chat list into a model and a windowed view`** — `ChatListService` per `ChatList`,
  reached through `ClientService.GetChatList`; `ItemsCollection` is a window over it.
- **`Put the group call participants on the windowed collection`** — plus the paging fix in the
  Traps section below.
- **`Put the topic lists on the ordered source and its window`** — forum and direct messages, and
  the four dead update types that went with them.
- **`Split saved messages topics into a service`** — `SavedMessagesTopicService`, out of
  `ClientService.SavedMessages`.

## Left in the working tree

- **The archived chats refresh.** `IChatListDelegate.UpdateChatListArchive` is deleted and the
  watcher moved into `MainPage`, which is Fela's file this week: both halves are uncommitted and
  are his to commit. Until then the badge does not refresh — `ItemsCollection` no longer watches
  the archive.
- **`MainPage`'s cell-update drain** — his experiment: dirty bits per chat instead of one closure
  per update, measured at 2169 → 1147 refreshes over a startup.
- **`notes/architecture.md`** owes an update in the Collections and Services sections, and it
  carries his uncommitted edits, so it was left alone rather than half-staged.

## Next

1. **`StoryListViewModel.ItemsCollection`** — the last one on the old shape: a tracked window
   with the `-1` sentinel `NextIndexOf`.
2. **Replace the `-1` sentinel** with a result type carrying `bool HasMoreItems` (Fela's call).
   Six read sites: `ItemsCollection` ×2, `SavedMessagesTopicsCollection`, `StoryListViewModel`,
   `TopicListViewModel` ×2. See [[totalcount-minus-one-sentinel]] — it reads like a bug and is not.

## Traps

- **A source that cannot be paged *yet* must answer "no more", and re-arm when it can.** The
  empty-load clamp turns `HasMoreItems` off after three loads that add nothing while claiming
  more, which is the point of it — but a source answering "nothing now, more later" hits it
  legitimately. `VoipGroupCallParticipants` did: `loadGroupCallParticipants` errors out until the
  call is joined, so every pre-join load was empty-but-more, the clamp fired, and
  `GroupCallParticipantsCollection.Load()` — gated on `HasMoreItems` — did nothing on join, so the
  list stayed empty for the whole call. Fixed by answering `HasMore: false` while unjoinable and
  arming in `Load()` rather than testing. Verified working 2026-09-01.
  The clamp logs `<type> reports more items but never adds any`, which is how to spot the
  next one.

## Open, decide before shipping

- **`Dispose()` has no caller.** `WindowedCollection` subscribes to a session-lived service, so
  the collection is retained until disposed. `MainViewModel` creates `Chats` once per window and
  `GroupCallWindow` disposes its own; the chat list has no teardown hook. Bounded to one page
  graph per closed window, but real. Either wire a disposal point or give the services' `Changed`
  weak subscription semantics like `EventAggregator` has.
- **`Changed` fires with the chat's lock held**, because `SetChatPositions` is called from inside
  it. `VoipGroupCallParticipants` deliberately raises outside its lock. Documented on the event,
  not fixed.
- **`VoipGroupCallParticipants` is not on `OrderedSourceService`.** It is the same shape, but a
  participant is keyed by `MessageSender` and ordered by a *string*: fitting it would mean two more
  type parameters on the base, and two more closed generics under AOT, to share a sorted set.
- **`ClientService.Clear` drops `_forums` and `_directMessagesChats`** while keeping the chat list
  services. A topic collection holding a dropped service re-acquires it in `ReloadAsync`, which the
  authorization-state update triggers — but that is a sequence, not an invariant.
- **`totalCount` counts rows that appeared, not the page size.** A page that only re-places
  known items reports 0 and the clamp counts it. Unreachable while the window holds a prefix of
  the source, which `IsWithinWindow` maintains.

## Not worth doing

- Batching updates in `EventAggregator`. Handlers filter on the update thread first
  (`if (update.ChatId == _chat.Id)`), so delivering everything on the dispatcher would wake it
  for updates that were going to be discarded. Filtering stays with the handler; only accepted
  work is queued.
- `MainPage`'s 22 `BeginOnUIThread` handlers. Most are cold — a title change, your own user
  record. One closure per rename is not worth a queue.

- Making `OrderChangedEventArgs<T>` a `record struct`. It would take the one per-update allocation
  on the path to zero — the queue and the batch both store it inline — but it is the type argument
  of a `TypedEventHandler<,>`, and a WinRT generic delegate over a managed struct is not something
  to change without a build. One short-lived gen0 object per update is not worth that risk.
