# Where TDLib vectors are mutated

Every place the app writes into a list that came from TDLib, collected while deciding whether to
expose `vector<x>` as an immutable `Vector<T>` rather than `IList<T>`. Useful on its own: a list
reaching the app from an update is shared with the cached object it came from, so a write into one
is visible to every other reader of that object.

## How this was collected

No single method sees all of it, so three were used and unioned.

1. **Compile against an immutable type.** The generator was flipped to emit `Vector<T>` and the app
   built. The compiler reports a mutation only where the concrete type reaches the call, so this
   sees 32 sites and is blind to anything that has already widened to `IList<T>`.
2. **Match the schema against mutating calls.** All 238 `vector<...>` field names were read out of
   `td_api.tl` and matched against `Add`/`Insert`/`Remove`/`RemoveAt`/`Clear`/`Sort`/`AddRange` and
   indexer writes. 259 raw hits, most of them noise: `Items`, `Blocks`, `Values` and `Attributes`
   are schema field names *and* XAML or settings properties, so `flyout.Items.Add(...)` matches.
   44 survive filtering.
3. **Make the widening visible.** `IList<T>` and the non-generic `IList` were removed from
   `Vector<T>` while `IReadOnlyList<T>` was kept, and the app rebuilt. Every point where a vector
   escapes into a slot that could mutate it then fails to compile, while `foreach` and LINQ stay
   quiet. 212 escapes, resolved to their sink below.

1 and 2 overlap on 25 sites; 7 are compiler-only and 19 grep-only, which is the measure of how
partial either is alone.

## What to do with each group

- **A** is the real list: the app writes directly into a vector it does not own.
- **B** and **C** are one step removed - the vector is handed to something that mutates it, or
  stored in a field that is mutated later. Same effect, one indirection away.
- **D** is the bulk and needs no thought: 140 escapes into a parameter that only reads. Tightening
  the declaration from `IList<T>` to the concrete type is enough, and is what buys TG1001 the
  visibility the whole exercise is for.

Two shapes are mixed together in A and worth separating before touching anything. `ClientService`
mutating a cached object under `lock (value)` to apply an update is deliberate and correct today;
immutability turns each into a rebuild, which costs an allocation on the update path but hands
readers a consistent snapshot instead of a torn one. Everything in `Controls\` and most of
`ViewModels\` is the other shape: app code editing a list it was merely handed, where the existing
defensive copies nearby (`ChatCell.xaml.cs:1520`, `ClientEx.cs` `MergeEntities`) show the invariant
was already understood and just not enforced.

## Caveats

Groups 2 and 3 rest on textual heuristics - a name match for the first, a one-hop sink resolution
for the second - so this is a floor, not a proof. A vector passed through two levels of `IList<T>`
parameter, or stored in a collection of lists, is not caught. 27 escapes resolved to neither a call
nor an assignment and were left out; most are `return x.SomeVector;` from an `IList<T>`-returning
member, which propagates the question to that member's callers rather than answering it.

## A. Direct mutation of a TDLib vector property

- `Common\RichHtml.cs:279` — `items[items.Count - 1].Blocks.Add(block);`
- `Common\TLNavigationService.cs:259` — `features.Features.Remove(appIcons);`
- `Common\TLNavigationService.cs:265` — `features.Limits.Remove(archivedChats);`
- `Common\TLNavigationService.cs:268` — `features.Limits.Add(new PremiumLimit(new PremiumLimitTypeConnectedAccounts(), 3, 4));`
- `Controls\Cells\ChatCell.xaml.cs:1521` — `message.Entities.Add(new TextEntity(match.Index, match.Length, new TextEntityTypeSpoiler`
- `Controls\Chats\ChatTranslateBar.xaml.cs:173` — `markdown.Entities.Add(new TextEntity(index, 2, new TextEntityTypeCustomEmoji(51972528272`
- `Controls\Chats\ChatTranslateBar.xaml.cs:179` — `link.Entities.Add(new TextEntity(link.Entities[0].Offset, link.Entities[0].Length, new T`
- `Controls\Messages\EmojiMenuFlyout.xaml.cs:690` — `_message.UnreadReactions.Add(unread);`
- `Controls\Messages\EmojiMenuFlyout.xaml.cs:692` — `_message.UnreadReactions.Remove(unread);`
- `Controls\Messages\MessageEffectMenuFlyout.xaml.cs:423` — `_message.UnreadReactions.Add(unread);`
- `Controls\Messages\MessageEffectMenuFlyout.xaml.cs:425` — `_message.UnreadReactions.Remove(unread);`
- `Controls\Messages\MessageServiceText.cs:1408` — `content.Entities.Add(new TextEntity(index, 2, new TextEntityTypeCustomEmoji(forumTopicEd`
- `Controls\Messages\MessageServiceText.cs:2551` — `if (source.Entities.IsReadOnly)`
- `Controls\Messages\MessageServiceText.cs:2637` — `source.Entities.Add(new TextEntity(index, name.Length, id ?? new TextEntityTypeBold()));`
- `Controls\Messages\MessageServiceText.cs:2693` — `if (source.Entities.IsReadOnly)`
- `Controls\Messages\MessageServiceText.cs:2738` — `source.Entities.AddRange(entities);`
- `Controls\Messages\ReactionsMenuFlyout.xaml.cs:1088` — `_message.UnreadReactions.Add(unread);`
- `Controls\Messages\ReactionsMenuFlyout.xaml.cs:1090` — `_message.UnreadReactions.Remove(unread);`
- `Services\ClientService.cs:3551` — `value.ChatLists.Add(updateChatAddedToList.ChatList);`
- `Services\ClientService.cs:3568` — `value.ChatLists.Remove(chatList);`
- `Td\ClientEx.cs:92` — `merge.Entities.Add(entity);`
- `ViewModels\Business\BusinessBotsViewModel.cs:582` — `recipients.ChatIds.Add(chat.ChatId);`
- `ViewModels\Business\BusinessBotsViewModel.cs:590` — `recipients.ExcludedChatIds.Add(chat.ChatId);`
- `ViewModels\Business\BusinessRecipientsViewModelBase.cs:158` — `recipients.ChatIds.Add(chat.ChatId);`
- `ViewModels\ChatListViewModel.cs:524` — `folder.ExcludedChatIds.Remove(data.Chat.Id);`
- `ViewModels\ChatListViewModel.cs:525` — `folder.IncludedChatIds.Add(data.Chat.Id);`
- `ViewModels\ChatListViewModel.cs:544` — `folder.IncludedChatIds.Remove(data.Chat.Id);`
- `ViewModels\ChatListViewModel.cs:561` — `folder.IncludedChatIds.Remove(data.Chat.Id);`
- `ViewModels\ChatListViewModel.cs:562` — `folder.ExcludedChatIds.Add(data.Chat.Id);`
- `ViewModels\Chats\ChatStoriesViewModel.cs:249` — `story?.AlbumIds.Add(album.Id);`
- `ViewModels\Chats\ChatStoriesViewModel.cs:278` — `story.AlbumIds.Add(album.Id);`
- `ViewModels\DialogViewModel.cs:1455` — `foundChatMessages.Messages.Insert(i, album.MessagesValue[j]);`
- `ViewModels\DialogViewModel.cs:1610` — `messages.MessagesValue.Insert(index + 1, new Message(0, target.SenderId, null, target.Ch`
- `ViewModels\DialogViewModel.cs:888` — `messages.MessagesValue.RemoveAt(messages.MessagesValue.Count - 1);`
- `ViewModels\Drawers\EmojiDrawerViewModel.cs:313` — `defaultStickers.StickersValue.Insert(0, new Sticker(0, 0, 0, 0, string.Empty, null, null`
- `ViewModels\Folders\FolderViewModel.cs:444` — `folder.PinnedChatIds.Add(item);`
- `ViewModels\Folders\FolderViewModel.cs:473` — `folder.IncludedChatIds.Add(chat.ChatId);`
- `ViewModels\Folders\FolderViewModel.cs:496` — `folder.ExcludedChatIds.Add(chat.ChatId);`
- `ViewModels\Folders\FolderViewModel.cs:95` — `folder.IncludedChatIds.Add(createArgs.IncludeChatId);`
- `ViewModels\Premium\PromoViewModel.cs:78` — `features.Features.Remove(appIcons);`
- `ViewModels\Premium\PromoViewModel.cs:84` — `features.Limits.Remove(archivedChats);`
- `ViewModels\Premium\PromoViewModel.cs:87` — `features.Limits.Add(new PremiumLimit(new PremiumLimitTypeConnectedAccounts(), 3, 4));`
- `ViewModels\Profile\ProfileGiftsTabViewModel.cs:211` — `gift?.CollectionIds.Add(collection.Id);`
- `ViewModels\Profile\ProfileGiftsTabViewModel.cs:240` — `gift.CollectionIds.Add(collection.Id);`
- `ViewModels\Profile\ProfileGiftsTabViewModel.cs:255` — `param.gift.CollectionIds.Remove(param.collection.Id);`
- `ViewModels\Profile\ProfileGiftsTabViewModel.cs:268` — `param.gift.CollectionIds.Add(param.collection.Id);`
- `ViewModels\Settings\SettingsStorageViewModel.cs:284` — `chat.ByFileType.Remove(fileType);`
- `ViewModels\Settings\SettingsStorageViewModel.cs:294` — `result.ByFileType.Add(already);`
- `ViewModels\Settings\SettingsStorageViewModel.cs:303` — `value.ByChat.Remove(chat);`
- `Views\MainPage.xaml.cs:3287` — `response.IncludedChatIds.Remove(chat.Id);`
- `Views\Popups\MemberTagInfoPopup.xaml.cs:96` — `markdown.Entities.Add(new TextEntity(index, 3, new TextEntityTypeMention()));`

## B. Passed into a helper whose IList<T> parameter it mutates

- `Common\PageBlockHelper.cs:1166` → `TryAppendInputBlocks(blocks)` — `if (TryAppendInputBlocks(source.Blocks, text, entities))`
- `Common\PageBlockHelper.cs:1190` → `Flatten(entities)` — `Flatten(GetRichText(message?.Blocks), text, entities);`
- `Common\TextBlockHelper.cs:207` → `GetRuns(entities)` — `var runs = TextStyleRun.GetRuns(text, entities);`
- `Common\TextStyleRun.cs:126` → `GetRuns(entities)` — `return GetRuns(formatted.Text, formatted.Entities);`
- `Common\TextStyleRun.cs:404` → `StyledText(entities)` — `return new StyledText(text.Text, text.Entities, GetParagraphs(text.Text, text.Entities));`
- `Common\TextStyleRun.cs:421` → `GetText(entities)` — `return GetText(PageBlockHelper.GetRichText(message.Blocks));`
- `ViewModels\DialogViewModel.cs:894` → `AddHeaderAsync(messages)` — `await AddHeaderAsync(messages.MessagesValue, fromMessage?.Get());`
- `ViewModels\Drawers\AnimationDrawerViewModel.cs:67` → `Merge(destination)` — `BeginOnUIThread(() => Merge(SavedItems, animation.AnimationsValue));`
- `ViewModels\Drawers\AnimationDrawerViewModel.cs:78` → `Merge(destination)` — `BeginOnUIThread(() => Merge(SavedItems, animation.AnimationsValue));`
- `ViewModels\Drawers\StickerDrawerViewModel.cs:91` → `Merge(destination)` — `BeginOnUIThread(() => Merge(_favoriteSet.Stickers, favorite.StickersValue));`
- `ViewModels\Drawers\StickerDrawerViewModel.cs:121` → `Merge(destination)` — `BeginOnUIThread(() => Merge(_recentSet.Stickers, recent.StickersValue));`

## C. Stored into a field or property that is mutated elsewhere

- `Services\ClientService.cs:4257` → `Messages` — `value.Messages = updateQuickReplyShortcutMessages.Messages;`
- `Services\ClientService.cs:4263` → `Messages` — `Messages = updateQuickReplyShortcutMessages.Messages`
- `ViewModels\DialogViewModel.Handle.cs:1112` → `UnreadReactions` — `message.UnreadReactions = update.UnreadReactions;`
- `ViewModels\Dialogs\DialogPendingTextMessage.cs:343` → `_pending` — `_pending = messageRich.Message.Blocks;`
- `ViewModels\MessageViewModel.cs:193` → `UnreadReactions` — `UnreadReactions = message.UnreadReactions;`
- `ViewModels\MessageViewModel.cs:497` → `UnreadReactions` — `UnreadReactions = message.UnreadReactions;`
- `ViewModels\Stories\StoryViewModel.cs:100` → `AlbumIds` — `AlbumIds = story.AlbumIds;`
- `Views\InstantPage.xaml.cs:137` → `Blocks` — `ViewModel.Blocks = instantView.Blocks;`
