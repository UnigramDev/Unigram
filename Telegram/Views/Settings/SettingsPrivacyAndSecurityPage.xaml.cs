//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsPrivacyAndSecurityPage : HostedPage
    {
        public SettingsPrivacyAndSecurityViewModel ViewModel => DataContext as SettingsPrivacyAndSecurityViewModel;

        public SettingsPrivacyAndSecurityPage()
        {
            InitializeComponent();
            Title = Strings.PrivacySettings;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (ApiInfo.IsPackagedRelease && ViewModel.ClientService.Options.CanIgnoreSensitiveContentRestrictions)
            {
                FindName(nameof(SensitiveContent));
            }
        }

        #region Binding

        private string ConvertNewChat(bool? value)
        {
            return value switch
            {
                true => Strings.LastSeenEverybody,
                false => Strings.PrivacyMessagesContactsAndPremium,
                _ => null
            };
        }

        private string ConvertOnOff(bool value)
        {
            return value ? Strings.NotificationsOn : Strings.NotificationsOff;
        }

        private string ConvertSync(bool sync)
        {
            return sync ? Strings.SyncContactsInfoOn : Strings.SyncContactsInfoOff;
        }

        private string ConvertP2P(int mode)
        {
            switch (mode)
            {
                case 0:
                default:
                    return Strings.LastSeenEverybody;
                case 1:
                    return Strings.LastSeenContacts;
                case 2:
                    return Strings.LastSeenNobody;
            }
        }

        private string ConvertTtl(int days)
        {
            if (days == 0)
            {
                return Strings.NotificationsOff;
            }

            return Locale.FormatTtl(days);
        }

        #endregion
    }
}
