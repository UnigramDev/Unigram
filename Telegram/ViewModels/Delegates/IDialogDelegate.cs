//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Controls.Chats;
using Telegram.Controls.Messages;
using Telegram.Td.Api;
using Telegram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.ViewModels.Delegates
{
    public interface IDialogDelegate : IProfileDelegate
    {
        void UpdateChatActions(Chat chat, IDictionary<MessageSender, ChatAction> actions);

        void UpdateChatTheme(Chat chat);
        void UpdateChatBackground(Chat chat);
        void UpdateChatPermissions(Chat chat);
        void UpdateChatActionBar(Chat chat);
        void UpdateChatHasScheduledMessages(Chat chat);
        void UpdateChatReplyMarkup(Chat chat, MessageViewModel message);
        void UpdateChatUnreadMentionCount(Chat chat, int unreadMentionCount);
        void UpdateChatUnreadReactionCount(Chat chat, int unreadReactionCount);
        void UpdateChatDefaultDisableNotification(Chat chat, bool defaultDisableNotification);
        void UpdateChatMessageSender(Chat chat, MessageSender defaultMessageSenderId);
        void UpdateChatPendingJoinRequests(Chat chat);
        void UpdateChatIsTranslatable(Chat chat, string language);
        void UpdateChatVideoChat(Chat chat, VideoChat videoChat);
        void UpdateChatBusinessBotManageBar(Chat chat, BusinessBotManageBar businessBotManageBar);

        void UpdateUserRestrictsNewChats(Chat chat, User user, UserFullInfo fullInfo, CanSendMessageToUserResult result);

        void UpdatePinnedMessage(Chat chat, bool known);
        void UpdateCallbackQueryAnswer(Chat chat, MessageViewModel answer);

        void UpdateComposerHeader(Chat chat, MessageComposerHeader header);
        void UpdateSearchMask(Chat chat, ChatSearchViewModel search);

        void UpdateAutocomplete(Chat chat, IAutocompleteCollection collection);

        void UpdateGroupCall(Chat chat, GroupCall groupCall);

        void UpdateDeleteMessages(Chat chat, IList<MessageViewModel> messages);



        void PlayMessage(MessageViewModel message, FrameworkElement target);

        void ViewVisibleMessages();


        void HideStickers();

        void ChangeTheme();

        void UpdateMessageSendSucceeded(long oldMessageId, MessageViewModel message);

        void UpdateContainerWithMessageId(long messageId, Action<SelectorItem> action);

        void UpdateBubbleWithMessageId(long messageId, Action<MessageBubble> action);
        void UpdateBubbleWithMediaAlbumId(long mediaAlbumId, Action<MessageBubble> action);

        void UpdateBubbleWithReplyToMessageId(long messageId, Action<MessageBubble, MessageViewModel> action);

        void ForEach(Action<MessageBubble, MessageViewModel> action);
        void ForEach(Action<MessageBubble> action);

        bool HasContainerForItem(long id);
        SelectorItem ContainerFromItem(long id);
    }
}
