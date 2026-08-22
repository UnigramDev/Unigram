# Pending messages — review

All of it is applied. Each item below records what was done.

`updatePendingMessage` streams a bot's message as it is generated. The client shows it as a fake
bubble, types it out, and either promotes it to the real message or drops it.

Pieces:

- `Telegram/ViewModels/Dialogs/DialogPendingTextMessage.cs` — `DialogPendingMessage` and its two
  subclasses drive the typewriter over a `MessageViewModel` they own.
- `Telegram/ViewModels/DialogViewModel.Pending.cs` — the collection of them, and the glue to `Items`.
- `Telegram/ViewModels/DialogViewModel.Handle.cs` — `Handle(UpdatePendingMessage)` and the
  interaction with `Handle(UpdateNewMessage)`.

---

## 1. Ordering: what to use as the message id

`NextIndexOf` (`DialogViewModel.Handle.cs:1556`) orders `Items` by ascending `Id`, so the id **is**
the sort key, and it has to be assigned by us.

`draft_id` can't be it. It is an *input* parameter of `sendTextMessageDraft` (`td_api.tl:13200`),
so the value originates in the bot; it reaches the wire as
`sendMessageTextDraftAction#376d975c random_id:long` and `DialogActionManager.cpp:192` forwards it
verbatim. A bot picking a random int64 lands near 2^62 and looks like it sorts last, which is why
it seems to work — but `1` is just as legal, and "unique" is not "increasing", so two concurrent
drafts have no order between them either.

The bubble is not pinned to the end — it takes a position when it is created, is pushed up by
anything that legitimately comes after it, and is moved to its real position by the existing
`InsertMessage(message, oldMessageId)` in `PendingMessage_Completed` once the bot's real message
arrives. So the id only has to sort after everything loaded **at the moment the draft starts**.

### Why `LastId + 1` isn't it

`MessageSelector.xaml.cs:762` already records the layout: a server message id is `server_id << 20`,
and the low 20 bits carry the type and the local counter. A message the user sends is yet-unsent,
so TDLib gives it an id low in the band above the newest server message — `(S << 20) + 1` first.
That is exactly `LastId + 1`, so a pending bubble taking it collides with the next message the user
sends while the bot is streaming.

### Take the top of the local band instead

**Done** — `NextPendingMessageId` in `DialogViewModel.Pending.cs`.

```csharp
// td_api gives a yet-unsent message an id low in the band above the newest server message
// (MessageSelector.MessageTypeMask). Pending bubbles take the last slots of that band, so they
// sort after everything loaded, stay after a message the user sends meanwhile, and stay below
// the server id the bot's real message will get.
private const long PendingMessageIdCount = 64;

var band = (Items.LastId | MessageTypeMask) + 1;   // start of the next server slot
var id = band - PendingMessageIdCount + _pendingCount++;
```

- Sorts after every loaded message, and after a message the user sends meanwhile — which is the
  right order, since that message is being sent now and the bot's is not.
- Sorts before the bot's real message, whose id is the next server slot, so the promotion in
  `PendingMessage_Completed` is a move, not a no-op.
- Several drafts take consecutive slots in arrival order, which is the whole point.
- `draft_id` goes back to being nothing but the key of `_pendingMessages`.

`_pendingCount` resets whenever `_pendingMessages` empties. 64 slots is arbitrary; nothing near that
many drafts is ever live at once.

### The collection has to know which items are synthetic

**Done** — `MessageViewModel.IsSynthetic`, set on pending messages and on the new-thread footer,
honoured by `FirstId`/`LastId` and by `ChatView.Bubbles`.

Today the pending id is `long.MaxValue`, which every consumer special-cases by value. With ids in
the local band that stops working, and the value checks were already wrong:

- **`MessageCollection.LastId` (`MessageCollection.cs:43`)** skips `Id == 0` but nothing else, so
  with a pending bubble present it returns the synthetic id. Two consequences today:
  - `LoadNextSliceAsync` (`DialogViewModel.cs:834`) bails on `long.MaxValue` — loading newer
    messages is dead while a pending message is shown.
  - `RawAddRange`'s filter (`MessageCollection.cs:130`) drops **every** appended message, since
    `message.Id < lastId` always holds.
- **`ChatView.Bubbles.xaml.cs:567`** — `if (message.Id is 0 or long.MaxValue) continue;` is what
  keeps synthetic ids out of `viewMessages`.

A `bool IsSynthetic` on `MessageViewModel` (beside `GeneratedContentUnread`,
`MessageViewModel.cs:165`) is cleaner than a magic range: `LastId`/`FirstId` skip it,
`ChatView.Bubbles` tests it, and the id stays free to sit wherever it needs to sort.

`MessageHeaderNewThread` (`DialogViewModel.cs:1340`) wants the same flag. It is inserted with
`long.MaxValue` in a **private bot chat** — the same chat type that produces pending messages — so
today the two share a key in `MessageCollection._messages` and one silently overwrites the other.
It also causes the same `LastId` paging break on its own, without any pending message involved.

---

## 2. Findings

Roughly by severity.

### 2.1 An expired pending message is never removed — `DialogViewModel.Pending.cs:152`

**Done** — every lookup is keyed on `DialogPendingMessage.MessageId`.

`OnTick` fires after `pending_text_message_period` and raises `Completed` with a null message. The
handler's null branch looks the bubble up as `Items.TryGetValue(sender.DraftId, …)`, but it was
inserted under `long.MaxValue`. The lookup always misses, so **the bubble stays in the chat
forever** — until the history is reloaded. Same wrong key at `Handle.cs:768`.

### 2.2 The surviving pending message leaks — `Handle.cs:775`

**Done** — the survivor stays in `_pendingMessages` until `Completed` removes it, and the
others go through `RemovePendingMessage`.

`Handle(UpdateNewMessage)` picks the most recently updated pending, detaches the *others*, then
calls `_pendingMessages.Clear()` and keeps driving the survivor. The survivor is now untracked:
`ClearPendingMessages()` cannot see it, so navigating away mid-typing leaves its two
`DispatcherTimer`s running with `Updated`/`Completed` still attached — holding the
`DialogPendingMessage`, its `MessageViewModel`, and through `PendingMessage_Completed` the whole
`DialogViewModel`. It should stay in the dictionary until `Completed` removes it.

### 2.3 Only one pending bubble can exist — `Handle.cs:823`

**Done** — each draft gets its own identifier and its own bubble.

`if (Items.TryGetValue(long.MaxValue, out _)) return;` means a second draft id builds a
`DialogPendingMessage` around a `MessageViewModel` that is never inserted. Its `Updated` event then
writes into the item at `long.MaxValue` — the *first* pending's bubble — so two concurrent drafts
overwrite each other's content. This is the part §1 unblocks; the id is necessary but not
sufficient, see §3.

### 2.4 A throwaway `Message` + `MessageViewModel` per update — `Handle.cs:802`

**Done** — `CreatePendingMessage` runs on the UI thread, only for a draft not seen before.

The message is built on the TDLib thread for *every* `updatePendingMessage`, but only the first one
is ever inserted; on the common path — an update to an existing draft — it is allocated and
dropped. Bots send a chunk at a time, so this is per-chunk garbage for the whole generation. Build
it inside the `else` branch on the UI thread, where it is actually needed.

### 2.5 `StartsWith` is culture-sensitive and quadratic — `DialogPendingTextMessage.cs:272`

**Done** — `StringComparison.Ordinal`.

`text.Text.StartsWith(_text.Text)` uses `StringComparison.CurrentCulture` — a linguistic comparison,
far more expensive than an ordinal one, over a prefix that grows with the message. Should be
`StringComparison.Ordinal`.

### 2.6 Nothing is shown when the newest slice is not loaded — `Handle.cs:1497`

**Done, deliberately** — the update is now dropped outright when `IsNewestSliceLoaded != true`,
so no timers run for a bubble that has nowhere to go. Scrolling back to the bottom picks the
next update up.

`InsertMessage` only inserts when `IsNewestSliceLoaded == true`; otherwise the pending message falls
through both branches and is silently dropped, while `_pendingMessages` keeps tracking it. The
early-out at `Handle.cs:823` then never fires, so every following update tries to insert again.
Probably the right behaviour, but it should be deliberate rather than incidental.

### 2.7 Dead pacing code — `DialogPendingTextMessage.cs:189`

**Done** — `LastCharacter`, overridden for text. Rich messages keep the default rather than
walking the block tree between every chunk.

`RaiseUpdate` passes a hardcoded character to `GetRandomDelay`; the expression that would pass the
real last character is commented out beside it. The punctuation branches in `GetRandomDelay` are
therefore unreachable and the typewriter runs at a constant random-in-range pace. Either wire it up
or drop the branches.

### 2.8 `GetSpeedMultiplier` is fed the total, not the remainder — `DialogPendingTextMessage.cs:56`

**Done** — both callers pass what is left to type.

`GetRandomChunkSize(remainingLength)` correctly receives `_pendingLength - _textLength`, but then
calls `GetSpeedMultiplier(_pendingLength)`. So the speed-up is keyed on how long the whole message
is rather than how much is left, and never decays as it catches up — which reads as unintended,
given the parameter is named `remainingLength`.

### 2.9 Topic filter is one-sided — `Handle.cs:798`

**Kept** — a topicless view is the whole chat, so accepting every topic is right. What was
wrong is that the bubble was built with `messageTopicForum(0)` even outside a forum, which
makes `UpdateForumTopicSeparatorOnInsert` draw a topic separator above it; it now passes null.

`TopicId == null || TopicId.IsForum(update.ForumTopicId)`: when the view has no topic, an update for
any topic is accepted and rendered in the general list. Correct if a topicless forum view really is
a merged list of every topic — worth confirming, since the message is then built with
`new MessageTopicForum(update.ForumTopicId)` regardless, including `0`.

### 2.10 Naming — `DialogPendingTextMessage2`

**Done** — renamed to `DialogPendingTextMessage`.

The `2` suffix looks like a leftover from a rewrite; the file is `DialogPendingTextMessage.cs` and
there is no `DialogPendingTextMessage`.

---

## 3. What multi-pending needs beyond the id

**Done.** Each `DialogPendingMessage` owns its `MessageId`, and:

- `PendingMessage_Updated` / `PendingMessage_Completed` / `RemovePendingMessage` key off
  `sender.MessageId` instead of `long.MaxValue` — this also fixes 2.1.
- `Handle(UpdatePendingMessage)` drops the "already have one" early-out and simply inserts the new
  bubble; `NextIndexOf` puts it after the previous one.
- `Handle(UpdateNewMessage)` currently promotes the newest pending and drops the rest. Per the TL
  doc, an incoming message deletes *every* pending in the thread, and a pending with a different
  draft id deletes the previous ones — so "keep the newest" is the client's own rule. With several
  bubbles supported, the natural reading is that the arriving message replaces the pending whose
  draft the bot completed and the others are removed, but TDLib does not say which draft a real
  message completes, so it stays a guess. Worth flagging rather than quietly extending.
- `ClearPendingMessages` already handles a dictionary of any size.

## 4. Left open

`RawAddRange` appends with `Add`, not in identifier order, so a slice loaded downwards would land
after a pending bubble instead of before it. It can't happen today — a bubble is only created while
`IsNewestSliceLoaded == true`, and that is exactly when no downward load is issued — but the two
facts are a page apart and nothing ties them together.
