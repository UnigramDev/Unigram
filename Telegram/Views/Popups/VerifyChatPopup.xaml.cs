//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class VerifyChatPopup : TeachingTipEx
    {
        public string Text { get; set; } = string.Empty;

        private readonly TaskCompletionSource<ContentDialogResult> _tsc = new();

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

                ActionButtonStyle = BootStrapper.Current.Resources["DangerButtonStyle"] as Style;
                ActionButtonContent = Strings.Remove;
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

                ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style;
                ActionButtonContent = Title;
            }

            ActionButtonClick += OnAction;
            CloseButtonContent = Strings.Cancel;

            Closed += OnClosed;
        }

        private void OnAction(TeachingTip sender, object args)
        {
            _tsc.TrySetResult(ContentDialogResult.Primary);
            IsOpen = false;
        }

        private void OnClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
        {
            _tsc.TrySetResult(ContentDialogResult.Secondary);
        }

        public Task<ContentDialogResult> ShowAsync()
        {
            IsOpen = true;
            return _tsc.Task;
        }

        public static async Task<InputPopupResult> ShowAsync(XamlRoot xamlRoot, IClientService clientService, Chat chat, bool remove, bool canSetCustomDescription)
        {
            if (xamlRoot.Content is not IToastHost host)
            {
                return null;
            }

            var popup = new VerifyChatPopup(clientService, chat, remove, canSetCustomDescription)
            {
                PreferredPlacement = TeachingTipPlacementMode.Center,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                IsLightDismissEnabled = true,
                ShouldConstrainToRootBounds = true,
            };

            popup.Closed += (s, args) =>
            {
                host.ToastClosed(s);
            };

            host.ToastOpened(popup);

            var confirm = await popup.ShowAsync();
            return new InputPopupResult(confirm, popup.Text, 0);
        }
    }
}
