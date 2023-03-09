//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class DeleteChatPopup : ContentPopup
    {
        public DeleteChatPopup(IClientService clientService, Chat chat, ChatList chatList, bool clear, bool asOwner = false)
        {
            InitializeComponent();

            //Photo.Source = PlaceholderHelper.GetChat(clientService, chat, 36);

            var position = chat.GetPosition(chatList);
            if (position?.Source is ChatSourcePublicServiceAnnouncement)
            {
                Title = Strings.Resources.PsaHideChatAlertTitle;
                Subtitle.Text = Strings.Resources.PsaHideChatAlertText;
                CheckBox.Visibility = Visibility.Collapsed;

                PrimaryButtonText = Strings.Resources.PsaHide;
                SecondaryButtonText = Strings.Resources.Cancel;

                return;
            }

            Title = clear ? Strings.Resources.ClearHistory : Strings.Resources.DeleteChatUser; // clientService.GetTitle(chat);

            var user = clientService.GetUser(chat);
            var basicGroup = clientService.GetBasicGroup(chat);
            var supergroup = clientService.GetSupergroup(chat);

            var deleteAll = user != null && chat.Type is ChatTypePrivate privata && privata.UserId != clientService.Options.MyId && chat.CanBeDeletedForAllUsers;
            if (deleteAll)
            {
                CheckBox.Visibility = Visibility.Visible;

                var name = user.FirstName;
                if (string.IsNullOrEmpty(name))
                {
                    name = user.LastName;
                }

                if (clear)
                {
                    CheckBox.Content = string.Format(Strings.Resources.ClearHistoryOptionAlso, name);
                }
                else
                {
                    CheckBox.Content = string.Format(Strings.Resources.DeleteMessagesOptionAlso, name);
                }
            }

            if (clear)
            {
                if (user != null)
                {
                    if (chat.Type is ChatTypeSecret)
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryWithSecretUser, user.FullName()));
                    }
                    else if (user.Id == clientService.Options.MyId)
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, Strings.Resources.AreYouSureClearHistorySavedMessages);
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryWithUser, user.FullName()));
                    }
                }
                else if (basicGroup != null)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryGroup));
                }
                else if (supergroup != null)
                {
                    if (supergroup.IsChannel)
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryChannel));
                    }
                    else if (supergroup.HasActiveUsername())
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryGroup));
                    }
                    else
                    {
                        TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureClearHistoryWithChat, chat.Title));
                    }
                }
            }
            else if (user != null)
            {
                if (chat.Type is ChatTypeSecret)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureDeleteThisChatWithSecretUser, user.FullName()));
                }
                else if (user.Id == clientService.Options.MyId)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, Strings.Resources.AreYouSureDeleteThisChatSavedMessages);
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureDeleteThisChatWithUser, user.FullName()));
                }

                if (user.Type is UserTypeBot)
                {
                    CheckBox.Visibility = Visibility.Visible;
                    CheckBox.Content = Strings.Resources.BotStop;
                }
            }
            else if (basicGroup != null)
            {
                TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.AreYouSureDeleteAndExitName, chat.Title));
            }
            else if (supergroup != null)
            {
                if (asOwner)
                {
                    if (supergroup.IsChannel)
                    {
                        Subtitle.Text = Strings.Resources.ChannelDeleteAlert;
                    }
                    else
                    {
                        Subtitle.Text = Strings.Resources.MegaDeleteAlert;
                    }
                }
                else if (supergroup.IsChannel)
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.ChannelLeaveAlertWithName, chat.Title));
                }
                else
                {
                    TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.Resources.MegaLeaveAlertWithName, chat.Title));
                }
            }

            if (clear)
            {
                PrimaryButtonText = Strings.Resources.ClearHistory;
            }
            else if (user != null || basicGroup != null)
            {
                PrimaryButtonText = Strings.Resources.DeleteChatUser;
            }
            else if (supergroup != null)
            {
                if (supergroup.IsChannel)
                {
                    PrimaryButtonText = asOwner ? Strings.Resources.ChannelDeleteMenu : Strings.Resources.LeaveChannelMenu;
                }
                else
                {
                    PrimaryButtonText = asOwner ? Strings.Resources.DeleteMegaMenu : Strings.Resources.LeaveMegaMenu;
                }
            }

            SecondaryButtonText = Strings.Resources.Cancel;
        }

        public bool IsChecked => CheckBox.Visibility == Visibility.Visible && CheckBox.IsChecked == true;

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
