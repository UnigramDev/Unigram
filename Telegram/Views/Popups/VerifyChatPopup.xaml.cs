//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.Views.Popups
{
    public sealed partial class VerifyChatPopup : ModalPopup
    {
        public string Text { get; set; } = string.Empty;

        public VerifyChatPopup(IClientService clientService, Chat chat, bool remove, bool canSetCustomDescription)
        {
            InitializeComponent();

            if (remove)
            {
                Title = Strings.BotRemoveVerificationTitle;
                MessageLabel.Text = chat.Type is ChatTypePrivate
                    ? Strings.BotRemoveVerificationText
                    : Strings.BotRemoveVerificationChatText;

                Label.Visibility = Visibility.Collapsed;

                PrimaryButtonStyle = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
                PrimaryButtonContent = Strings.Remove;
            }
            else
            {
                if (clientService.TryGetUser(chat, out User user))
                {
                    Title = user.Type is UserTypeBot
                        ? Strings.BotVerifyBotTitle
                        : Strings.BotVerifyUserTitle;
                }
                else if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    Title = supergroup.IsChannel
                        ? Strings.BotVerifyChannelTitle
                        : Strings.BotVerifyGroupTitle;
                }

                TextBlockHelper.SetMarkdown(MessageLabel, string.Format(Strings.BotVerifyText, chat.Title));

                if (canSetCustomDescription)
                {
                    Label.MaxLength = (int)clientService.Options.BotVerificationCustomDescriptionLengthMax;
                    Label.PlaceholderText = Strings.BotVerifyDescription;
                    Label.Description = chat.Type is ChatTypePrivate
                        ? Strings.BotVerifyDescriptionInfo
                        : Strings.BotVerifyDescriptionInfoChat;
                }
                else
                {
                    Label.Visibility = Visibility.Collapsed;
                }

                PrimaryButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
                PrimaryButtonContent = Title;
            }

            SecondaryButtonContent = Strings.Cancel;
        }

        public static async Task<InputPopupResult> ShowAsync(XamlRoot xamlRoot, IClientService clientService, Chat chat, bool remove, bool canSetCustomDescription)
        {
            var popup = new VerifyChatPopup(clientService, chat, remove, canSetCustomDescription)
            {
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                IsLightDismissEnabled = true,
            };

            var confirm = await popup.ShowAsync(xamlRoot);
            return new InputPopupResult(confirm, popup.Text, 0);
        }
    }
}
