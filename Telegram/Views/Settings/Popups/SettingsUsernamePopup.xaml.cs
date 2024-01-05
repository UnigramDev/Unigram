//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.ViewModels.Settings;
using Telegram.Views.Host;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Settings.Popups
{
    public sealed partial class SettingsUsernamePopup : ContentPopup
    {
        public SettingsUsernameViewModel ViewModel => DataContext as SettingsUsernameViewModel;

        public SettingsUsernamePopup()
        {
            InitializeComponent();

            Title = Strings.Username;
            PrimaryButtonText = Strings.Done;
            SecondaryButtonText = Strings.Cancel;

            var debouncer = new EventDebouncer<TextChangedEventArgs>(Constants.TypingTimeout, handler => Username.TextChanged += new TextChangedEventHandler(handler));
            debouncer.Invoked += (s, args) =>
            {
                if (ViewModel.UpdateIsValid(Username.Text))
                {
                    ViewModel.CheckAvailability(Username.Text);
                }
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Username.Focus(FocusState.Keyboard);
            Username.SelectionStart = Username.Text.Length;
        }

        #region Binding

        private string ConvertAvailable(string username)
        {
            return string.Format(Strings.UsernameAvailable, username);
        }

        private string ConvertUsername(string username)
        {
            return MeUrlPrefixConverter.Convert(ViewModel.ClientService, username);
        }

        private string UsernameHelpLink => string.Format(Strings.UsernameHelpLink, string.Empty).TrimEnd();

        private string BotUsernameHelpLink => Strings.BotUsernameHelp.Replace("*Fragment*", "[Fragment](https://fragment.com)");

        #endregion

        private async void ContentPopup_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsPrimaryButtonEnabled = false;

            try
            {
                var deferral = args.GetDeferral();
                var confirm = await ViewModel.SendAsync();

                args.Cancel = !confirm;
                deferral.Complete();
            }
            catch
            {
                // Deferral already completed.
            }

            IsPrimaryButtonEnabled = true;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            var container = ScrollingHost.ContainerFromItem(e.ClickedItem) as SelectorItem;
            if (container == null || e.ClickedItem is not UsernameInfo username)
            {
                return;
            }

            if (username.Value == ViewModel.Username)
            {
                Username.Focus(FocusState.Keyboard);
                return;
            }

            var popup = new TeachingTip
            {
                Title = username.IsActive
                    ? Strings.UsernameDeactivateLink
                    : Strings.UsernameActivateLink,
                Subtitle = username.IsActive
                    ? Strings.UsernameDeactivateLinkProfileMessage
                    : Strings.UsernameActivateLinkProfileMessage,
                ActionButtonContent = username.IsActive ? Strings.Hide : Strings.Show,
                ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                CloseButtonContent = Strings.Cancel,
                PreferredPlacement = TeachingTipPlacementMode.Top,
                Width = 314,
                MinWidth = 314,
                MaxWidth = 314,
                Target = /*badge ??*/ container,
                IsLightDismissEnabled = true,
                ShouldConstrainToRootBounds = true,
            };

            popup.ActionButtonClick += (s, args) =>
            {
                popup.IsOpen = false;
                ViewModel.ToggleUsername(username);
            };

            if (Window.Current.Content is IToastHost host)
            {
                void handler(object sender, object e)
                {
                    host.Disconnect(popup);
                    popup.Closed -= handler;
                }

                host.Connect(popup);
                popup.Closed += handler;
            }

            popup.IsOpen = true;
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count == 1 && e.Items[0] is UsernameInfo username && (username.IsActive || username.IsEditable))
            {
                ScrollingHost.CanReorderItems = true;
                e.Cancel = false;
            }
            else
            {
                ScrollingHost.CanReorderItems = false;
                e.Cancel = true;
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ScrollingHost.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is UsernameInfo username)
            {
                ViewModel.ReorderUsernames(username);
            }
        }
    }
}
