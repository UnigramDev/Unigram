using System.Collections.Generic;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Unigram.ViewModels.Delegates
{
    public interface IDialogDelegate : IProfileDelegate
    {
        void UpdateChatActions(Chat chat, IDictionary<int, ChatAction> actions);

        void UpdateChatReplyMarkup(Chat chat, MessageViewModel message);
        void UpdateChatUnreadMentionCount(Chat chat, int unreadMentionCount);
        void UpdateChatDefaultDisableNotification(Chat chat, bool defaultDisableNotification);
        void UpdateChatOnlineMemberCount(Chat chat, int count);

        void UpdatePinnedMessage(Chat chat, MessageViewModel message, bool loading);
        void UpdateCallbackQueryAnswer(Chat chat, MessageViewModel answer);

        void UpdateComposerHeader(Chat chat, MessageComposerHeader header);



        void PlayMessage(MessageViewModel message, FrameworkElement target);
    }
}
