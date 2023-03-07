//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Controls.Chats;
using Unigram.ViewModels.Chats;

namespace Unigram.ViewModels.Delegates
{
    public interface IDialogDelegate : IProfileDelegate
    {
        void UpdateChatActions(Chat chat, IDictionary<MessageSender, ChatAction> actions);

        void UpdateChatTheme(Chat chat);
        void UpdateChatPermissions(Chat chat);
        void UpdateChatActionBar(Chat chat);
        void UpdateChatHasScheduledMessages(Chat chat);
        void UpdateChatReplyMarkup(Chat chat, MessageViewModel message);
        void UpdateChatUnreadMentionCount(Chat chat, int unreadMentionCount);
        void UpdateChatUnreadReactionCount(Chat chat, int unreadReactionCount);
        void UpdateChatDefaultDisableNotification(Chat chat, bool defaultDisableNotification);
        void UpdateChatMessageSender(Chat chat, MessageSender defaultMessageSenderId);
        void UpdateChatPendingJoinRequests(Chat chat);

        void UpdatePinnedMessage();
        void UpdatePinnedMessage(Chat chat, bool known);
        void UpdateCallbackQueryAnswer(Chat chat, MessageViewModel answer);

        void UpdateComposerHeader(Chat chat, MessageComposerHeader header);
        void UpdateSearchMask(Chat chat, ChatSearchViewModel search);

        void UpdateAutocomplete(Chat chat, IAutocompleteCollection collection);

        void UpdateGroupCall(Chat chat, GroupCall groupCall);



        void PlayMessage(MessageViewModel message, FrameworkElement target);

        void ViewVisibleMessages(bool intermediate);


        void HideStickers();
    }
}
