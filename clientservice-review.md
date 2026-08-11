# ClientService review — to-do

Read-through of the seven `Telegram/Services/ClientService.*.cs` partials (5,356 lines),
cross-checked against `Telegram/Td/Client.cs`, `Telegram/Collections/ReaderWriterDictionary.cs`,
and the call sites in `Controls/`, `Views/` and `ViewModels/` that decide whether a finding
is hot or cold.

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

- [ ] **`Clear()` misses eight caches** — `ClientService.cs:914` **[live]**

      Not cleared: `_communities`, `_welcomeMessages`, `_textCompositionStyles`,
      `_activeStories`, `_canceledDownloads`, `_completedDownloads`, `_explicitDownloads`,
      `_preparedLogsFileIds`.

      `_activeStories` is the one that bites. `_storyList` and `_haveFullStoryList` *are*
      cleared (`:951-952`), so after logout the ordering is gone but the previous account's
      `ChatActiveStories` objects are still served by `GetActiveStories` and
      `TryGetActiveStoriesFromUser` — cross-account leakage of another user's story state.

      Adding eight lines fixes today's misses. The eight misses are themselves the argument
      that a hand-maintained field-by-field `Clear()` doesn't survive contact with new
      fields; grouping the per-session caches into one object replaced wholesale would make
      the next miss impossible. Your call whether that's worth it now — see *Needs your call*.

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

- [ ] **`ReaderWriterDictionary` → `ConcurrentDictionary` on the entity caches** — `ClientService.cs:319-345` **[live]**

      `_chats`, `_users`, `_usersFull`, `_supergroups`, `_supergroupsFull`, `_basicGroups`,
      `_basicGroupsFull`, `_secretChats`, `_usersToChats` all go through
      `ReaderWriterLockSlim.EnterReadLock`/`ExitReadLock` per lookup
      (`ReaderWriterDictionary.cs:88`) — thread-local bookkeeping, interlocked ops, spin.

      These are overwhelmingly read; writes only happen on the receive thread.
      `ConcurrentDictionary` makes `TryGetValue` a lock-free volatile read. The file already
      uses `ConcurrentDictionary` for `_chatActions`, `_topicActions` and `_unreadCounts`, so
      the precedent and the migration path both exist. Largest measurable win in the file.

      While in there: `ReaderWriterDictionary.Find` (`:133`) is
      `Values.FirstOrDefault(x => predicate(x))` — a closure wrapping the predicate plus a
      LINQ enumerator per call, to do what a `foreach` does allocation-free.

- [ ] **`GetChatFolders` — allocations and O(n² log n) per chat cell** — `ClientService.cs:1711` **[live]**

      Called from `ChatCell.xaml.cs:1312` on every row update when tags are enabled. Per
      call: a `List<ChatFolderInfo>`, a closure capturing `this`, and
      `result.Sort((x, y) => _chatFolders.IndexOf(x) - _chatFolders.IndexOf(y))` — a linear
      scan per comparison. The existing `// TODO: can this be improved?` is right.

      It doesn't need a sort. Iterate `_chatFolders` in its own order and keep the ones the
      chat belongs to; the result is ordered by construction and `_chatFolders2` drops out
      of this path.

- [ ] **`OnResult` is 105 sequential type tests** — `ClientService.cs:3147-4047` **[live]**

      Roslyn emits type patterns as a chain of `isinst`, so cost is proportional to
      position. The ordering is partly deliberate — `UpdateChatPosition` and
      `UpdateChatLastMessage` are first — but `UpdateNewMessage` sits at `:4017`, roughly
      case 95 of 105, and is one of the highest-frequency updates in the protocol. Every
      incoming message pays ~95 type checks on the thread that must not fall behind.

      Cheap: hoist `UpdateNewMessage`, `UpdateChatAction`, `UpdateUserStatus`,
      `UpdateMessageInteractionInfo`, `UpdateMessageEdited` to the top.
      Better: a static `Dictionary<Type, Action<ClientService, Object>>` keyed on
      `update.GetType()` — one hash lookup regardless of case count.

- [ ] **`GetChats` has a side effect inside the enumerator** — `ClientService.cs:2245` **[live]**

      `UpdateMessageTopicNewChat` (`ForumTopics.cs:126`) runs per chat, per enumeration: a
      supergroup lookup, two `ContainsKey` calls, and possibly the construction of a
      `ForumTopicService`. This runs during chat-list rendering. Constructing services is
      not something an enumeration should do — it belongs on `UpdateNewChat` /
      `UpdateSupergroup`, where the state actually changes.

      `GetRecentlyOpenedChats` (`:1397`) compounds it by running that enumeration while
      holding `_recentChatsLock`.

- [ ] **Serial round trips where a fan-out belongs** **[live]**

      | Method | Line | Items in practice |
      |---|---|---|
      | `GetMessagePropertiesAsync` | `:1798` | up to 100 (multi-select) |
      | `GetAllReactionsAsync` / `GetReactionsAsync` | `:1750`, `:1774` | ~20–40 emoji |
      | `GetCustomEmojiStickerSets` | `:1204` | one per distinct set |
      | `GetMessageEffectsAsync` | `:1250` | one per uncached effect |

      `GetMessagePropertiesAsync` is the visible one: selecting 100 messages costs 100
      sequential request/response cycles before the selection toolbar can decide what's
      enabled (`DialogViewModel.Messages.cs:586`, `ChatView.xaml.cs:3085`, and 8 more).
      TDLib handles concurrent requests fine; `Task.WhenAll` over the cache misses collapses
      this to one round-trip latency.

- [ ] **Property getters that fire network requests** — `ClientService.cs:1449`, `:1463` **[live]**

      `OwnedStarCount` and `OwnedGramCount` send a request on *every* read until the update
      lands — and they are read from bindings, which re-evaluate. A `_requested` flag fixes
      it; making the fetch explicit rather than a side effect of a getter fixes it better.

- [ ] **Sync filesystem I/O on the receive thread** — `ClientService.cs:3015`, `Files.cs:379` **[live]**

      `ParseFile` and `ProcessFile` both call `NativeUtils.FileExists(file.Local.Path)` for
      every file whose download reports complete — a synchronous syscall on the single
      thread draining `td_receive`, on a path that fires constantly while media loads.

- [ ] **Unbounded session-lifetime growth** — `Files.cs:78-80` **[live]**

      `_explicitDownloads`, `_completedDownloads`, `_canceledDownloads` accumulate one entry
      per file for the whole process lifetime — nothing removes from them except an explicit
      cancel, and they survive `Clear()` (see P1). `_files` (`ClientService.cs:348`) is
      likewise never evicted; defensible for a singleton `File` cache, but worth being
      deliberate about rather than incidental.

---

## P3 — hygiene and duplication

Worth doing only while already in the file.

- [ ] **Three copies of the same paging algorithm** — `ChatList.cs:48`, `StoryList.cs:83`, `SavedMessages.cs:105`

      ~55 near-identical lines each, differing only in the `SortedSet`, the `Load*`
      function, and the return type. The real argument for merging isn't the ~110
      duplicated lines — it's that the P0 lock leak and the P1 mutating-getter bug are
      *each present in all three*, so every fix is a three-way fix until they're one method.

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

**Needs measuring, not reasoning:**

- The `ReaderWriterDictionary` → `ConcurrentDictionary` swap (P2) is the biggest claim in
  this doc and the one most worth confirming with a profile before and after, rather than
  taking on argument.

**Suggested order:** ~~P0~~ **done** → `GetChatFromMessageSenderAsync` and `Clear()` from P1
(small, live call sites) → the `ConcurrentDictionary` swap → `GetChatFolders` and `GetChats`
(the two chat-list-render costs).
