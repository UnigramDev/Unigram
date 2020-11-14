using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Services;
using Windows.UI.Xaml;

namespace Unigram.ViewModels.Delegates
{
    public interface IMessageDelegate : IViewModelDelegate
    {
        IEventAggregator Aggregator { get; }

        bool CanBeDownloaded(MessageViewModel message);
        void DownloadFile(MessageViewModel message, File file);

        void ReplyToMessage(MessageViewModel message);

        void OpenReply(MessageViewModel message);
        void OpenThread(MessageViewModel message);

        void OpenFile(File file);
        void OpenWebPage(WebPage webPage);
        void OpenSticker(Sticker sticker);
        void OpenLocation(Location location, string title);
        void OpenLiveLocation(MessageViewModel message);
        void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button);
        void OpenMedia(MessageViewModel message, FrameworkElement target);
        void PlayMessage(MessageViewModel message);

        void Call(MessageViewModel message, bool video);

        void VotePoll(MessageViewModel message, IList<PollOption> option);

        void OpenUsername(string username);
        void OpenUser(int userId);
        void OpenChat(long chatId, bool profile = false);
        void OpenChat(long chatId, long messageId);
        void OpenViaBot(int viaBotUserId);

        void OpenUrl(string url, bool untrust);
        void OpenHashtag(string hashtag);
        void OpenBankCardNumber(string number);

        void SendBotCommand(string command);

        bool IsAdmin(int userId);
        string GetAdminTitle(int userId);
        string GetAdminTitle(MessageSender sender);
    }
}
