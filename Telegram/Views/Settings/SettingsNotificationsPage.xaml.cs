//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsNotificationsPage : HostedPage
    {
        public SettingsNotificationsViewModel ViewModel => DataContext as SettingsNotificationsViewModel;

        public SettingsNotificationsPage()
        {
            InitializeComponent();
            Title = Strings.NotificationsAndSounds;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            BackgroundControl.Update(ViewModel.ClientService, ViewModel.Aggregator);
        }

        #region Binding

        private string ConvertName(bool value, bool _)
        {
            if (value
                && ViewModel.IsAllAccountsAvailable
                && ViewModel.IsAllAccountsNotifications
                && ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
            {
                return string.Format("{0} \u2b62 {1}", Strings.NotificationPreviewLine1, user.FullName());
            }

            return value
                ? Strings.NotificationPreviewLine1
                : Strings.AppName;
        }

        private string ConvertText(bool value)
        {
            return value
                ? Strings.NotificationPreviewLine2
                : Strings.YouHaveNewMessage;
        }

        private string ConvertCountInfo(bool count)
        {
            return count ? "Switch off to show the number of unread chats instead of messages" : "Switch on to show the number of unread messages instead of chats";
        }

        #endregion

    }
}
