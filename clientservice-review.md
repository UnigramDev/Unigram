# ClientService review — to-do

Read-through of the seven `Telegram/Services/ClientService.*.cs` partials (5,356 lines) plus
the two topic services they own — `ForumTopicService.cs` (960) and
`FeedbackChatTopicService.cs` (329, which holds `DirectMessagesChatTopicService`) —
cross-checked against `Telegram/Td/Client.cs`, `Telegram/Collections/ReaderWriterDictionary.cs`,
and the call sites in `Controls/`, `Views/` and `ViewModels/` that decide whether a finding
is hot or cold.

Findings in the `ClientService` partials are grouped P0–P3 below. The two topic services have
their own sections at the end, prefixed `F` and `D`, each item carrying its own priority.

Line numbers are as of `03513e401` with a clean `Telegram/Services/`.

Legend: **[live]** = confirmed reachable from current app code, with the call site named ·
**[latent]** = correct today only by convention or because no caller exercises it.

The two facts most of this rests on:

- `Client.Run` (`Client.cs:159`) is a **single** dedicated thread, and both `handler?.OnResult`
  (`:171`) and `action(response)` (`:175`) run on it. It is the only thread draining
  `td_receive`. Anything that blocks it stops all updates app-wide.
- `_chats` / `_users` / `_supergroups` lookups are the most-called functions in the file —
  ~40 branches of `OnResult` plus every chat-list and message render.

---

## P0 — can wedge the whole app — **done**

- [x] **`Monitor.Enter`/`Monitor.Exit` without `try`/`finally` — 13 sites** **[live]**
      → fixed in the commit that checked this box (`git log --follow clientservice-review.md`)

      `ClientService.cs:3155/3182`, `3192/3197`, `3224/3229`, `3243/3245`, `3256/3258`,
      `3324/3326`, `3441/3446` · `ChatList.cs:23/40`, `50/103` ·
      `SavedMessages.cs:39/50`, `107/160` · `StoryList.cs:36/48`, `85/138`

      **13, not the 11 first written here** — the review missed `UpdateChatDraftMessage`
      (`ClientService.cs:3441/3446`), found by grepping for `Monitor.` after converting the
      listed ones. Worth remembering that the enumeration in a review doc is a starting
      point, not the set.

      One throw between any pair leaks the lock permanently. Worst case is
      `ClientService.cs:3192`, where `UpdateChatLastMessage` constructs a
      `MessageAlbumLastMessageService` while holding the `Chat`: if that throws, the chat is
      locked forever, the next `UpdateChatPosition` for it blocks the TDLib receive thread,
      and update delivery stops app-wide with no crash to report it. `GetChatFolders`
      (`:1718`) also takes `lock (chat)` from the UI thread, so the chat list freezes too.

      Fixed: plain `lock (x) { … }` at all 13. The ten synchronous sites are a straight
      wrap. The three async paging methods could not be, because `await` is illegal inside
      `lock` — which is exactly why they were written with a hand-placed `Monitor.Exit`
      before the await. Each now decides *under* the lock how much is still to be loaded
      (`int missing`, or `bool load` for stories, since `LoadActiveStories` takes no count)
      and either builds its result and returns inside the lock, or falls out of it and does
      the await with no lock held. Same semantics, same lock ordering, one exit path.

      `using System.Threading;` dropped from the three partials that only had it for
      `Monitor`; it stays in `ClientService.cs`, which uses `Thread`.

      Left alone deliberately: the `#if MOCKUP` blocks in the three paging methods. `MOCKUP`
      is not defined in any configuration in `Telegram.csproj`, and the blocks reference an
      undefined `index` variable, so they have not compiled in a long time. Restructuring
      moved them but did not touch their contents — fixing dead code in a lock-safety commit
      would be a silent, untestable change.

      Still open, and deliberately not folded in: the locks are `Chat` and
      `SavedMessagesTopic` — TDLib data objects also handed to UI code, so the lock is
      publicly reachable. The current graph (`chat → _chatList`, `_chatFoldersLock → chat`)
      is acyclic by accident, not design. A private lock object, or no chat lock at all,
      would be sturdier. See *Needs your call*.

- [x] **The same bug outside the reviewed files, found by an app-wide sweep** **[live]**
      → fixed in the commit that checked this box

      `grep -rn "Monitor.Enter"` after the F1/D1 commit turned up four more files. Two are
      fixed here:

      - `Services/Calls/VoipGroupCallParticipants.cs:34/45`, `:55/62/112` — a **sixth** copy
        of the paging method, `SetParticipantOrder` + `GetParticipantsAsyncImpl`, the same
        two shapes as the five already converted. Same `int missing` restructure; its three
        return paths (`null` on a non-`Ok`/`Error` response, `null` on a non-404 error, the
        reentrant retry otherwise) are unchanged.
      - `Td/Api/TdExtensions.cs:2950`, `:2972` — `GetPosition` and `GetOrder`, each with an
        early `Monitor.Exit` inside a loop and another after it. These matter more than the
        line count suggests: **they lock `chat`**, the very objects `ClientService.OnResult`
        locks on the receive thread, so a throw in `AreTheSame` on the UI thread would have
        wedged update delivery app-wide. `return` inside `lock` releases correctly, so both
        collapse to one block with no early exit at all.

      `using System.Threading;` dropped from both — it was there only for `Monitor`.

      **Still open, same bug, not touched:** `Controls/DiceView.cs:239` and
      `Controls/Messages/Content/VideoNoteContent.xaml.cs:580`. Both are single-`Enter`,
      two-`Exit` shapes in UI code rather than on the receive thread, so they can wedge a
      control but not the whole update pipeline. Worth doing, lower stakes.

---

## P1 — correctness

- [ ] **`GetChatFromMessageSenderAsync` returns null for every chat sender** — `ClientService.cs:2225` **[live]**

      ```csharp
      TryGetChat(messageSender, out Chat chat);
      if (chat == null && messageSender is MessageSenderUser senderUser) { … return chat; }
      return null;                                    // ← chat found, thrown away
      ```

      `TryGetChat(MessageSender, out Chat)` (`:2165`) only resolves `MessageSenderChat`, so
      when it *succeeds* `chat != null`, the `if` is skipped, and the method returns null.
      The success path is unreachable.

      Both call sites are gift sending: `ReceivedGiftPopup.xaml.cs:1094` (gifting to a
      channel) and `GiftCraftChoosePopup.xaml.cs:227` (which passes `null`, so it is null
      either way — worth checking whether that call site means something else entirely).

- [x] **`Clear()` misses eight caches** — `ClientService.cs:914` **[live]**
      → fixed in the commit that checked this box

      Not cleared: `_communities`, `_welcomeMessages`, `_textCompositionStyles`,
      `_activeStories`, `_canceledDownloads`, `_completedDownloads`, `_explicitDownloads`,
      `_preparedLogsFileIds`.

      `_activeStories` was the one that bit. `_storyList` and `_haveFullStoryList` *were*
      cleared (`:951-952`), so after logout the ordering was gone but the previous account's
      `ChatActiveStories` objects were still served by `GetActiveStories` and
      `TryGetActiveStoriesFromUser` — cross-account leakage of another user's story state.

      The three download sets moved into a `ClearDownloads` helper in `Files.cs`, since they
      need `_downloadsLock` and the reason they must not survive an authorization is worth
      stating where they are declared: file ids and unique ids only mean anything within one
      session, and those sets are also the only state here that grows for the life of the
      process, one entry per file ever downloaded.

      Verified by enumerating every `private` field across the seven partials and diffing
      against what `Clear()` touches: the only fields it now leaves alone are the injected
      dependencies (`_client`, `_session`, `_aggregator`, `_locale`, `_deviceInfoService`)
      and the lock objects. Worth re-running that check rather than re-reading the method
      whenever a field is added.

      Still true, and the reason the misses happened: a hand-maintained field-by-field
      `Clear()` doesn't survive contact with new fields. Grouping the per-session caches into
      one object replaced wholesale would make the next miss impossible — see
      *Needs your call*.

- [ ] **Plain `Dictionary` shared across threads — 3 fields** **[latent]**

      - `_chatAccessibleUntil` (`:1147`) — written from the `CheckChatInviteLinkAsync`
        continuation, cleared on the TDLib thread, read from the UI thread
        (`ChatView.xaml.cs:6858`, `TLNavigationService.cs:445`).
      - `_cachedReactions` (`:381`) — written from `GetReactionsAsync` /
        `GetAllReactionsAsync` continuations, read from `TryGetCachedReaction` while
        rendering stories (`StoryContent.xaml.cs:604`).
      - `_preparedLogsFileIds` (`:353`) — `PrepareLogs` writes from the UI thread,
        `UpdateFile` (`:3120`) reads and removes on the TDLib thread.

      A concurrent read during a resize doesn't throw, it spins. All three are
      low-frequency, which is exactly why this would be a bug that never reproduces.

- [ ] **`NewDictionary` / `DefaultDictionary` getters mutate** — `ChatList.cs:144-158`, `:170-185` **[latent]**

      The indexer *getter* inserts on a miss, so every "read" is a write.
      `_haveFullChatList[chatList] = true` (`ChatList.cs:69`) and
      `_haveFullStoryList[storyList] = true` (`StoryList.cs:104`) both run **after**
      `Monitor.Exit`, from an async continuation, against a dictionary another thread reads
      — and therefore writes — under the monitor. Same hazard as the item above, but
      disguised as a read.

- [ ] **`SetResult` → `TrySetResult`, and `RunContinuationsAsynchronously`** — `ClientService.cs:1085`, `Files.cs:83`, `ClientService.cs:397` **[latent]**

      `SendAsync` and `GetFileAsync(int)` use `SetResult`: a duplicate response throws on
      the TDLib thread. `TrySetResult` costs nothing.

      Separately, none of the three `TaskCompletionSource` instances (`SendAsync`,
      `GetFileAsync`, `_authorizationStateTask`) pass `RunContinuationsAsynchronously`, so
      an `await SendAsync(…)` from a context-free thread resumes **inline on the receive
      thread**. `DialogViewModel.cs:3179` and `:3245` already call `.Result` on `SendAsync`;
      if either ever runs on that thread it is a hard deadlock. Cheap insurance.

- [ ] **`async void` with an unguarded await** — `Files.cs:202` **[latent]**

      `AddFileToDownloads` awaits `Future.ContainsAsync` outside the `try`. In `async void`
      that throw goes straight to the unhandled-exception handler. (`TrackDownloadedFile`
      and `CancelDownloadFile` are `async void` too but do guard their awaits.)

---

## P2 — performance and memory

Ordered by measured reach, not by size of change.

- [x] **~~`ReaderWriterDictionary` → `ConcurrentDictionary` on the entity caches~~ — measured, and the answer is no** — `ClientService.cs:319-345`

      **This was the biggest claim in the doc and it was wrong.** It ranked the swap as "the
      largest measurable win in the file" by reasoning about per-operation cost without ever
      asking what the operation *rate* was. Counting the rate reverses the recommendation.

      Fan-out of one full `ChatCell` refresh (`ChatCell.xaml.cs:1273`), counting only calls
      that reach a `ReaderWriterDictionary`:

      | Call | Dictionary | Lookups |
      |---|---|---|
      | `UpdateChatTitle` → `GetTitle` → `GetUser(chat)` | `_users` | 1 |
      | `UpdateChatEmojiStatus` → `TryGetUser` / `TryGetSupergroup` | `_users` / `_supergroups` | 1–2 |
      | `UpdateFromLabel` → `ShowFrom` → `TryGetSavedMessagesTopic` | `_savedMessagesTopics` | 1 |
      | `ShowFrom` → `TryGetUser(SenderId)` / `TryGetChat` | `_users` / `_chats` | 1–2 |
      | `UpdateBriefLabel` → `TryGetMediaAlbum` | `_lastMessageAlbums` | 1 |
      | `UpdateBriefLabel` → `GetSecretChat` / `GetTitle` | `_secretChats` / `_users` | 0–2 |
      | `UpdateChatUnreadMentionCount` → `TryGetUser` | `_users` | 1 |
      | `UpdateChatMessageAutoDeleteTime` → `TryGetUser` | `_users` | 1 |
      | `UpdateBotOpen` → `TryGetUser` | `_users` | 1 |

      ≈ **8–12 per cell** for an ordinary text message, more for service and forwarded ones.

      The rate is bounded by UI control realization: a ~64 px row, a hard flick at 2–3 k px/s
      → ~30–50 rows/s, plus aggregator refreshes of the ~14 visible cells. Call it 50–500
      cell updates/s → **0.5–6 k lookups/s**. At the ~30 ns/lookup a lock-free read would
      save, that is **0.015–0.18 ms per second, or 0.002 %–0.02 % of one core**. Wrong by
      100× it still does not reach 2 %. At one operation per ~200 µs the multi-core
      cache-line contention that motivated the idea never arises either.

      `ConcurrentDictionary` costs a node allocation per entry — the axis this repo ranks
      first — to buy that back. Bad trade. **The custom class stays.**

      Two scoping corrections found while counting: `GetChatActions` is already a
      `ConcurrentDictionary`, and `GetChatFolders` uses `_chatFolders2`, a plain `Dictionary`
      under `_chatFoldersLock` — neither was ever in this item's scope.

- [x] **`ReaderWriterDictionary.Find` allocates per call** — `ReaderWriterDictionary.cs:133`
      → fixed in the commit that checked this box

      `Values.FirstOrDefault(x => predicate(x))` — a closure wrapping the predicate plus a
      LINQ enumerator every call, to do what a `foreach` does allocation-free. All that
      survives of the item above, and it needed no type change. `System.Linq` stays: `Values`
      still uses `ToArray`.

- [x] **`GetChatFolders` — a closure per chat cell** — `ClientService.cs:1711` **[live]**
      → fixed in the commit that checked this box

      Called from `ChatCell.xaml.cs:1312` on every row update when tags are enabled.

      **The "O(n² log n)" in the original wording was overstated**, the same mistake as the
      item above it: `_chatFolders` holds at most a few dozen entries and a chat is usually
      in one or two, so `Sort` runs about one comparison. And when the chat is in no folder —
      the common case — `result` stays null, nothing is allocated and the sort never runs.

      What was real: `result.Sort((x, y) => …)` allocated a fresh closure over `this` on
      every call that produced a result. Now a `Comparison<ChatFolderInfo>` field created
      once, and the sort is skipped entirely below two elements.

      Rewriting it to walk `_chatFolders` in order instead — the "no sort at all" idea in the
      original wording — would have been *slower*: it turns the common zero-folder case from
      "scan the chat's two lists" into "scan every folder."

- [x] **~~`OnResult` is 105 sequential type tests~~ — measured, not worth the dispatch table** — `ClientService.cs:3147-4047`

      Roslyn emits type patterns as a chain of `isinst`, so cost is proportional to position,
      and `UpdateNewMessage` sits at roughly case 95 of 105. All true, and all irrelevant at
      the rate this runs.

      An `isinst` against a sealed type is a type-handle compare, so ~105 of them is on the
      order of 100–200 ns. Updates arrive at maybe tens per second in normal use, and
      thousands per second briefly during an initial sync. Even at 10 k/s that is **2 ms per
      second, 0.2 % of the receive thread** — and normal use is a hundredth of that.

      Same trap as the `ConcurrentDictionary` item: a real per-operation cost, no rate behind
      it. The `Dictionary<Type, …>` dispatch table is not worth the churn. Hoisting the five
      hottest cases is five lines and free if anyone is in there anyway, but it buys nothing
      measurable either.

- [x] **~~`GetChats` has a side effect inside the enumerator~~ — load-bearing, left alone** — `ClientService.cs:2245`

      `UpdateMessageTopicNewChat` (`ForumTopics.cs:126`) runs per chat, per enumeration, and
      may construct a `ForumTopicService`. Constructing services from an enumeration is a
      genuine smell, and the original wording said it belongs on `UpdateNewChat` /
      `UpdateSupergroup` instead.

      **It can't move to `UpdateSupergroup` as things stand.** That update carries a
      supergroup id, and there is no supergroup→chat index to get back to the `Chat` it needs
      — only `_usersToChats` exists. So `GetChats` is the only path that notices a supergroup
      that *became* a forum after its `updateNewChat`. Removing it would break late forum
      conversion.

      The cost is also smaller than the wording implied: for a non-supergroup — most of the
      list — it is one type check and nothing else.

      Worth revisiting only alongside a supergroup→chat index, which is a bigger change than
      this buys. `GetRecentlyOpenedChats` (`:1397`) running that enumeration under
      `_recentChatsLock` is still ugly and still true.

- [x] **Serial round trips where a fan-out belongs** **[live]**
      → fixed in the commit that checked this box

      | Method | Line | Items in practice |
      |---|---|---|
      | `GetMessagePropertiesAsync` | `:1798` | up to 100 (multi-select) |
      | `GetAllReactionsAsync` / `GetReactionsAsync` | `:1750`, `:1774` | ~20–40 emoji |
      | `GetCustomEmojiStickerSets` | `:1204` | one per distinct set |
      | `GetMessageEffectsAsync` | `:1250` | one per uncached effect |

      **The one P2 item the rate check strengthens rather than weakens**, because it is
      latency, not throughput: selecting 100 messages cost 100 sequential request/response
      cycles before the selection toolbar could decide what was enabled
      (`DialogViewModel.Messages.cs:586`, `ChatView.xaml.cs:3085`, and 8 more). Nothing else
      in P2 is on a path a person waits on.

      All four now issue their requests together and `Task.WhenAll` them. Notes:

      - `GetAllReactionsAsync` became `GetReactionsAsync(_activeReactions)` — it was a
        verbatim copy.
      - `GetMessageEffectsAsync` keeps its results **in request order**, indexed by position,
        because the effect drawer and the reaction menu both display them in the order they
        asked for. The obvious rewrite — cached first, fetched appended — silently reorders
        them.
      - `_cachedReactions` and `_effects` are still written after the `WhenAll` in a single
        loop on one thread, so this does not worsen the open item about `_cachedReactions`
        being an unsynchronized `Dictionary`.
      - `GetReactionsAsync` now skips duplicate emoji in its input, which sequential awaits
        used to absorb via the cache.

- [x] **Property getters that fire network requests** — `ClientService.cs:1449`, `:1463` **[live]**
      → fixed in the commit that checked this box

      `OwnedStarCount` and `OwnedGramCount` sent a request on *every* read until the update
      landed — and they are read from bindings, which re-evaluate. Now guarded by
      `_requestedStarCount` / `_requestedGramCount`, both reset in `Clear()` so a new
      authorization fetches again.

      Making the fetch explicit rather than a side effect of a property getter would still be
      better, but that changes every call site.

- [ ] **Sync filesystem I/O on the receive thread** — `ClientService.cs:3015`, `Files.cs:379` **[live]**

      `ParseFile` and `ProcessFile` both call `NativeUtils.FileExists(file.Local.Path)` for
      every file whose download reports complete — a synchronous syscall on the single
      thread draining `td_receive`, on a path that fires constantly while media loads.

- [x] **~~Unbounded session-lifetime growth~~ — closed: `_files` is TDLib's model, the rest is noise** — `Files.cs:78-80`, `ClientService.cs:348`

      The original wording lumped four collections together and implied they were one
      problem. They are two, and neither is worth code.

      **`_files` cannot be evicted, and that is a constraint TDLib imposes.** The contract is
      id→instance identity: `updateFile` carries a file id, and `ParseFile` looks the id up
      and mutates *the existing instance in place* so every binding already holding that
      `File` sees the change. TDLib never retires a file id within a session and never says
      "this one is finished with." So if the app dropped id 123 and a later `updateFile` for
      123 arrived, `ParseFile` would mint a *new* instance while the UI still held the old
      one — that thumbnail or progress bar would silently stop updating forever. Eviction is
      only safe when nothing holds the entry, which means weak references
      (`Dictionary<int, WeakReference<File>>` or a `ConditionalWeakTable`), plus a sweep for
      dead slots, plus a dereference per update on the receive thread. That is a lot of
      machinery on a hot path for the size involved.

      Size, since the original said "unbounded" without a number. Per the schema, one entry
      is three objects plus three strings — `local.path`, `remote.id`, `remote.unique_id` —
      and the strings dominate: roughly 700–1000 bytes. Note every photo contributes one id
      *per size variant*. Ten thousand distinct files is on the order of 8 MB, a hundred
      thousand about 80 MB. Real, worth knowing, and **nowhere near** the undiagnosed
      multi-GB growth being chased separately — this is not that lead.

      **The three download sets are Unigram's own but negligible.** `_explicitDownloads` is a
      `HashSet<int>` at ~16 bytes an entry; `_canceledDownloads` only holds files the user
      actually cancelled; `_completedDownloads` only holds files that went through the
      Downloads folder. Hundreds of KB at the top end. They were only in this item because
      they sat next to each other in the file.

      Also already bounded by the `Clear()` fix in P1: all four are dropped on an
      authorization change, so none of this survives a logout.

      If a file cache ever does need bounding, the precedent is `EmojiCache` in
      `memory-leaks.md`, which is the same shape — accumulates one entry per id ever seen,
      never removes. Worth noting that that investigation looked for sustained growth across
      the app and did not flag `_files`.

---

## P3 — hygiene and duplication

Worth doing only while already in the file.

- [ ] **Six copies of the same paging algorithm** — `ChatList.cs:48`, `StoryList.cs:83`,
      `SavedMessages.cs:105`, `ForumTopicService.cs:214`, `FeedbackChatTopicService.cs:141`,
      `Calls/VoipGroupCallParticipants.cs:51`

      ~55 near-identical lines each, differing only in the `SortedSet`, the `Load*`
      function, and the return type. Six, not the three first written here — the count went
      up twice as the sweep widened, which is itself the point.

      The real argument for merging isn't the ~275 duplicated lines — it's that the P0 lock
      leak is present in *all six*, so every fix is a six-way fix until they're one method.
      Now demonstrated rather than argued: closing the lock leak took three commits and the
      identical `int missing` restructure six times, and the sixth copy was only found by
      grepping the whole app rather than by reading the files anyone thought were involved.

- [ ] **`GetAllReactionsAsync` is exactly `GetReactionsAsync(_activeReactions)`** — `ClientService.cs:1750` vs `:1774`

- [ ] **The three `*Impl` methods are `public` but on no interface** — `ChatList.cs:48`, `StoryList.cs:83`, `SavedMessages.cs:105`

- [ ] **`MessageSenderEqualityComparer` allocated per dictionary** — `ClientService.cs:3343`, `:3354`

      `ChatListEqualityComparer` (`ChatList.cs:110`) already demonstrates the static
      `Instance` pattern.

- [ ] **LINQ in small helpers** — `:2776`, `:2788`, `:2796`, `:1847`, `:826`, `:1884`, `:2827`

      `TryGetEmojiChatTheme` ×2, `TryGetGroupCallMessageLevel`,
      `GetQuickReplyShortcut(string)` (two allocations), `IsTextCompositionStyleInstalled`,
      `CheckQuickReplyShortcutName`, `IsDiceEmoji` (allocates via `Trim()`).

      **Checked the call sites: none of these are hot** — context menus, chat open, drawer
      construction. Listed for completeness, not as a priority. Same for the sticker
      helpers (`IsStickerFavorite` etc., `:2736-2774`), whose linear `IList.Contains` is
      only reached from context-menu construction and is fine as it is.

---

## ForumTopicService (960 lines)

`Services/ForumTopicService.cs`. One instance per forum chat, created and owned by
`ClientService.ForumTopics.cs`. Reached from the TDLib thread through every `Update*`
method, and from the UI thread through `TopicListViewModel.cs:691`, `:907`, `:919` and
`ChatCell.xaml.cs:697`.

- [x] **F1 · P0 · Four more `Monitor.Enter`/`Exit` pairs without `try`/`finally`** **[live]**
      → fixed in the commit that checked this box

      `:64/75` (`UpdateTopicOrder`) · `:100/107` (`SetPinnedForumTopics`) ·
      `:216/228/269` (`GetForumTopicsAsyncImpl`) · `:284/337` (`LoadForumTopicsAsync`)

      Same class as the P0 item already fixed, missed there because the review only covered
      the `ClientService.*` partials.

      `LoadForumTopicsAsync` was the worst of the four: `tsc.SetResult` was called *inside*
      the monitor (`:325`, `:329`, `:334`), so the awaiting continuation — which is
      `GetForumTopicsAsyncImpl`, which re-enters `_order` — ran inline while the lock was
      held. `Monitor` is recursive, so it did not deadlock; but a throw in that continuation
      would have skipped the outer `Monitor.Exit` at `:337` and wedged the topic list for
      that chat for the rest of the session.

      Fixed: `lock` at all four. `UpdateTopicOrder` keeps its publish *outside* the lock,
      where the hand-written `Monitor.Exit` already put it. `GetForumTopicsAsyncImpl` got the
      same `int missing` restructure as the three in the P0 commit. `LoadForumTopicsAsync`
      now assigns an `Object result` under the lock and calls `SetResult` after releasing it.

      The `_aggregator.Publish` at `:321` is still inside the lock — that is F7, left alone
      on purpose to keep this commit to one kind of change.

- [x] **F2 · P1 · Six unsynchronized collections shared across two threads** **[live]**
      → fixed in the commit that checked this box

      `_topics`, `_messages`, `_pinnedTopicIds`, `_deletedTopicIds`, `_pendingNewTopics`,
      `_pendingLastReadInboxMessageId` (`:27-37`) were plain `Dictionary`/`List`/`HashSet`.
      Every `Update*` method writes them from the TDLib thread. `GetTopic` (`:170`) and
      `GetTopics` (`:185`) read them from the UI thread — and `GetTopic` *writes*
      `_pendingNewTopics` at `:178`. Only `_unreadTopicIds` and `_order` were guarded.

      Concurrent mutation of a `Dictionary` doesn't throw, it spins.

      Fixed with **one private `_lock` for all eight collections**, absorbing the two
      existing lock objects (`_order`, `_unreadTopicIds`) so there is a single domain and no
      ordering to get wrong. Critical sections stay small and **publishes stay outside them**,
      so this does not enlarge F7.

      Why one lock rather than `ReaderWriterDictionary`, which would match
      `DirectMessagesChatTopicService` and `ClientService`: that type only covers the two
      `Dictionary` fields. `_pinnedTopicIds` is a `List` (`IndexOf`/`Insert`/`AddRange`),
      `_order` is a `SortedSet`, and three more are `HashSet`s — six of the eight would still
      need a lock, leaving two domains and real cross-domain compounds (`UpdatePinnedTopics`
      reads `_pinnedTopicIds` then `_topics`; `Order` reads `_deletedTopicIds` and
      `_pinnedTopicIds`; `LoadForumTopicsAsync` touches four in one batch). One lock makes
      those atomic and makes a lock cycle impossible. The measurement under P2 also removed
      the performance argument for `ReaderWriterLockSlim` — at these rates a plain `Monitor`
      is cheaper anyway.

      The two `Dictionary` reads go through `TryGetTopic`/`TryGetTopicByMessage`, so the
      backing store stays cheap to swap if that judgement is ever revisited.

      Deliberately unchanged: the `ForumTopic` objects themselves are still handed to the UI
      and mutated by the update methods without synchronisation, exactly as `ClientService`
      does with `Chat` and `User`. This fixes container corruption, which is the part that
      spins forever; object-level tearing is a wider design question than one class.

- [x] **F3 · P1 · `GetTopics` is missing a `continue`** — `:189-199` **[live]**
      → fixed in the commit that checked this box, in both services

      For `id == int.MaxValue` it yields the synthetic "All topics" row and then *falls
      through* to `GetTopic(int.MaxValue)`, which adds `int.MaxValue` to `_pendingNewTopics`
      and fires `GetForumTopic(chatId, 2147483647)` at the server. It fires once per service
      instance — the id then sits in `_pendingNewTopics` forever — so the cost is one bogus
      round trip per forum opened plus a permanently poisoned pending entry, not a per-frame
      storm.

      **The dependency on F4 is now moot for this path** — the request is no longer made at
      all — but it still holds in general: if some other id ever fails to resolve, F4's rule
      is what stops it being asked for once per enumeration. `DirectMessagesChatTopicService.GetTopics` has the identical omission
      (`FeedbackChatTopicService.cs:113-117`); it is harmless there today only because that
      `GetTopic` doesn't fetch.

- [x] **F4 · P1 · `UpdateNewTopic` leaks `_pendingNewTopics` on any failure** — `:437-447` **[live]**
      → fixed in the commit that checked this box

      `if (newTopic == null) return;` at `:442` sat *before* the `_pendingNewTopics.Remove`
      at `:447`. Any non-`ForumTopic` response left the id pending forever, and the guard in
      `GetTopic` (`:176`) then never retried it: the method returned null for that topic for
      the rest of the session. A topic that failed to load once stayed missing until restart.

      **The obvious fix is a worse bug.** Clearing the entry on every failure means a topic
      that genuinely does not exist gets requested again on every enumeration — a request per
      scroll, forever. That suppression is load-bearing, and it is also the only thing
      currently stopping F3's bogus `int.MaxValue` request from repeating.

      So retry is scoped to failures that repeating can actually fix: `Code >= 500` or
      `Code < 0`, meaning server or transport. Every 4xx stays suppressed, because it says
      the request is wrong or the topic is gone. Keying on 404 alone would not have been
      enough — TDLib reports a missing object as `400` at least as often as `404`, so the
      storm would have stayed open through the more common code.

      `UpdateNewTopic` now takes the id it asked for, since a failure response carries none.
      All three call sites pass it; the one inside `UpdateDeleteMessages` names its lambda
      parameter `inner` because the enclosing callback already binds `response`.

- [x] **F5 · P1 · `UpdateDeleteMessages` stops at the first affected topic** — `:600` **[live]**
      → fixed in the commit that checked this box

      The `break` sat inside `foreach (long messageId in messageIds)`, after refreshing the
      one topic whose last message was deleted. A delete batch spanning several topics —
      deleting all of a member's messages, clearing history — refreshed only one of them; the
      others kept a stale last-message preview and a stale sort order.

      `break` removed. Each `_messages` entry is still handled at most once, because the entry
      is removed as it is handled, so a later id in the batch resolves to a different one.

      Accepted cost: a delete that takes out the last message of *n* topics now issues *n*
      `getForumTopic` calls instead of one. That is bounded by the number of topics actually
      affected, and the alternative — one `getForumTopics` reload — is a much larger change
      to the batch-load path.

- [ ] **F14 · P2 · `LoadForumTopicsAsync` can leave a stale `_messages` entry** — `:345`

      Found while checking F5's safety. It does `_topics[id] = topic` with a **fresh**
      `ForumTopic` instance and then `_messages[topic.LastMessage.Id] = topic`, without
      removing whatever key the previously cached instance was registered under. Every other
      writer (`UpdateLastMessage`, `UpdateMessageSendSucceeded`, the `UpdateDeleteMessages`
      callback) removes the old key first; this one doesn't.

      So after a reload, `_messages` can hold two keys for one topic, the stale one pointing
      at a discarded instance. Consequences are mild — a redundant `getForumTopic` in F5's
      loop, and updates applied to an object no longer in `_topics` — which is why the
      one-entry-per-topic property can't be relied on, and F5's comment says so explicitly.

- [ ] **F6 · P2 · Eight `Update*` handlers are lookup-then-nothing** — `:659-793`

      `UpdateMessageSendFailed`, `UpdateMessageEdited`, `UpdateMessageIsPinned`,
      `UpdateMessageInteractionInfo`, `UpdateMessageContentOpened`, `UpdateMessageMentionRead`,
      `UpdateMessageUnreadReactions`, `UpdateMessageFactCheck` each do a `_messages` lookup
      and then contain only comments describing what they would do.

      So a topic's last-message preview never refreshes when that message is edited, and
      per-message mention/reaction updates never reach the topic's counters — only the
      whole-topic `UpdateForumTopic` does. They also each cost a dictionary lookup per
      update, on the receive thread, to accomplish nothing. Worth deciding: implement, or
      delete the bodies and stop dispatching to them from `ClientService.OnResult`.

- [ ] **F7 · P2 · Aggregator publishes while holding the lock** — `LoadForumTopicsAsync` only

      Originally `UpdateTopicOrder` published after its own `Monitor.Exit`, but when called
      from inside another `_order` critical section recursion meant that `Exit` only
      decremented the count, so the publish ran with the lock still held.

      **Mostly closed by F2**: `SetPinnedForumTopics` and `UpdatePinnedTopics` now collect
      under the lock and reorder — and therefore publish — outside it.

      What remains is `LoadForumTopicsAsync`, whose whole callback body is one critical
      section, so its `UpdateChatUnreadTopicCount` publish and the nested
      `UpdateTopicOrder(topic, false)` calls still run under the lock. Left alone on purpose:
      unpicking it means restructuring the batch load, which is a different change from
      making the collections safe.

- [x] **~~F8 · P2 · `GetTopics` allocates a fresh synthetic topic per enumeration~~ — closed: caching it would pin the language** — `:193`, `:197`

      `ForumTopic` + `ForumTopicInfo` + `ForumTopicIcon` + `ChatNotificationSettings` — four
      allocations every time the topic list enumerates. The original wording called it "a
      constant; hoist it to a field built once per service." **It is not a constant.**

      Its label is `Strings.AllTopicsShort` or `Strings.BotForumNewTopic`, and
      `Strings.AllTopicsShort => Resource.GetString("AllTopicsShort")` is a live lookup on
      every access. The app handles `UpdateLanguagePackStrings` at runtime
      (`ClientService.cs:3859`), and a `ForumTopicService` lives in `_forums` until logout —
      so a hoisted field would show the *previous* language's label for the rest of the
      session after an in-app language change.

      Four allocations against a topic-list enumeration rate, versus a visible wrong-language
      string. Not worth it. Left as it is.

      (The two-branch `if`/`else` also matters and should stay: only the branch taken
      realizes its resource string.)

- [ ] **F9 · P3 · `ViewMessages` throws on an empty list** — `:87` **[latent]**

      `messageIds.Max()` on an empty sequence throws `InvalidOperationException`. Also a LINQ
      allocation on a path that runs per read.

- [ ] **F10 · P3 · Two update classes silently drop a constructor parameter**

      `UpdateForumTopicReadInbox` (`:852-857`) accepts `unreadCount` and never assigns
      `UnreadCount`; `UpdateDirectMessagesChatTopicReadInbox`
      (`FeedbackChatTopicService.cs:249-254`) does the same.

      **Checked, and it is not user-visible today:** both updates are consumed purely as
      signals — `TopicListViewModel.cs:480` and `:524` ignore the payload and re-read the
      live topic object, which the cell then reads. So nothing observes the dropped value.
      It is a trap for whoever reads `update.UnreadCount` next and gets a silent 0.

- [ ] **F11 · P3 · `UpdateUnreadCount` clamps the count to 0 or 1** — `:130-142`

      Both branches force `UnreadCount` to 0 or 1, overwriting the real server count that
      `UpdateNewTopic` assigns at `:455`. **Also not visible today**, because the badge is
      presence-only: `ForumTopicCell.xaml.cs:269` has its `UnreadBadge.Text` assignment
      commented out. It does still feed the `UnreadMentionCount == 1 && UnreadCount == 1`
      branch at `:264`, and it means the field cannot be trusted by anything new.

- [ ] **F12 · P3 · `SetPinnedForumTopics` silently no-ops over the limit** — `:93-96`

      Returns without sending and without telling anyone; `ForumView.xaml.cs:509` has no way
      to know the pin didn't happen.

- [ ] **F13 · P3 · `internal class ForumTopicService` vs `public partial class DirectMessagesChatTopicService`**

      Two classes with the same role and different visibility.

---

## DirectMessagesChatTopicService

`Services/FeedbackChatTopicService.cs` — note the class and file names disagree. Smaller and
in better shape than `ForumTopicService`; `_topics` is a `ReaderWriterDictionary` (`:24`).

- [x] **D1 · P0 · Two `Monitor.Enter`/`Exit` pairs without `try`/`finally`** — `:90/101`, `:143/155/196` **[live]**
      → fixed in the commit that checked this box, alongside F1

- [ ] **D2 · P1 · A topic seen for the first time never publishes** — `:65` **[latent]**

      The `else` branch calls `UpdateTopicOrder(newTopic, newTopic.Order, publish: false)`,
      so a topic arriving for the first time raises no
      `UpdateDirectMessagesChatTopicLastMessage`. `ForumTopicService.UpdateNewTopic` passes
      `true` in the same situation (`:480`). Marked latent because the list may pick it up
      through `GetDirectMessagesChatTopicsAsync` instead — worth confirming against
      `TopicListViewModel` before changing.

- [ ] **D3 · P1 · `_haveFullList` written outside the `_order` monitor** — `:162`

      Same shape as the `_haveFullChatList` item in P1 above.

- [ ] **D4 · P3** — missing `continue` in `GetTopics` (see F3) and the dropped `unreadCount`
      (see F10).

---

## What is left

**Needs your call:**

- Whether `Clear()` (P1) gets eight more lines or a per-session cache object. The object is
  the fix that stops the bug recurring; the eight lines are the fix that ships today.
- Whether `OnResult` (P2) gets a reordering or a type-keyed dispatch table. Reordering is
  five lines and most of the benefit; the table is the one that stops decaying as cases are
  added.
- Whether the chat-object locking called out under P0 is worth changing separately, now
  that the `try`/`finally` hole is closed. Locking a `Chat` still means UI code and the
  receive thread contend on the same publicly reachable object.
- `ICacheService` is ~200 members, so every ViewModel that wants `GetChat` also gets
  `PrepareLogs`. **I'd leave it alone** — splitting it is a large mechanical change across
  the whole app for legibility only.

**Measured, and closed:**

- The `ReaderWriterDictionary` → `ConcurrentDictionary` swap. A static fan-out count put the
  lookup rate at 0.5–6 k/s, three to four orders of magnitude below where lock overhead is
  visible. Recommendation reversed — see P2. The lesson worth keeping: a per-operation cost
  means nothing without the operation *rate*, and the rate here is bounded by UI control
  realization, not by anything inside the service. Cost to find out: one afternoon of
  reading, versus a migration plus a profiler session.

**Suggested order:** ~~P0~~, ~~F1, D1~~, ~~the sweep~~ **done** — 23 `Monitor` pairs across
seven files are now `lock`; only `DiceView` and `VideoNoteContent` remain →
**F2** (the unsynchronized collections, the worst thing in either topic service) →
`Clear()` and `GetChatFromMessageSenderAsync` from P1 →
**F4 and F5**, both small and both leaving the topic list visibly wrong →
`GetChatFolders` and `GetChats` (the two chat-list-render costs, which are allocation
problems rather than lock problems and so survive the measurement above).

**F6 needs your call before anyone touches it:** eight handlers that are stubs, not bugs.
Implementing them is a feature; deleting them is a cleanup. Either is fine, but guessing
which you want would waste the work.
