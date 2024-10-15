//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.ViewModels.Delegates
{
    public interface IMessageDelegate : IViewModelDelegate
    {
        bool IsDialog { get; }
        bool IsTranslating { get; }

        INavigationService NavigationService { get; }

        ISettingsService Settings { get; }

        IEventAggregator Aggregator { get; }

        IDictionary<long, MessageViewModel> SelectedItems { get; }

        bool IsSelectionEnabled { get; }

        ReactionType SavedMessagesTag { get; set; }

        void Select(MessageViewModel message);
        void Unselect(MessageViewModel message);

        bool CanBeDownloaded(object content, File file);
        void DownloadFile(MessageViewModel message, File file);

        void ForwardMessage(MessageViewModel message);

        void OpenReply(MessageViewModel message);
        void OpenThread(MessageViewModel message);

        void OpenFile(File file);
        void OpenWebPage(MessageText text);
        void OpenSticker(Sticker sticker);
        void OpenLocation(Location location, string title);
        void OpenGame(MessageViewModel message);
        void OpenInlineButton(MessageViewModel message, InlineKeyboardButton button);
        void OpenMedia(MessageViewModel message, FrameworkElement target, int timestamp = 0);
        void OpenPaidMedia(MessageViewModel message, PaidMedia media, FrameworkElement target, int timestamp = 0);
        void PlayMessage(MessageViewModel message);
        bool RecognizeSpeech(MessageViewModel message);

        void Call(MessageViewModel message, bool video);

        void VotePoll(MessageViewModel message, IList<int> option);

        void ViewVisibleMessages();

        void OpenUsername(string username);
        void OpenUser(long userId);
        void OpenChat(long chatId, bool profile = false);
        void OpenChat(long chatId, long messageId);
        void OpenViaBot(long viaBotUserId);

        void OpenUrl(string url, bool untrust);
        void OpenHashtag(string hashtag);
        void OpenBankCardNumber(string number);

        void SendBotCommand(string command);

        string GetAdminTitle(MessageViewModel message);
        bool IsAdministrator(MessageSender memberId);
        void UpdateAdministrators(long chatId);
    }
}
