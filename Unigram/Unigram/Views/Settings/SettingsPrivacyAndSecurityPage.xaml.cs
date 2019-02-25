using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.ViewModels.Settings;
using Unigram.Views.Settings.Privacy;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPrivacyAndSecurityPage : Page
    {
        public SettingsPrivacyAndSecurityViewModel ViewModel => DataContext as SettingsPrivacyAndSecurityViewModel;

        public SettingsPrivacyAndSecurityPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPrivacyAndSecurityViewModel>();
        }

        private void Sessions_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsSessionsPage));
        }

        private void WebSessions_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsWebSessionsPage));
        }

        private void BlockedUsers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsBlockedUsersPage));
        }

        private void StatusTimestamp_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyShowStatusPage));
        }

        private void PhoneCall_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyAllowCallsPage));
        }

        private void P2PCall_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyAllowP2PCallsPage));
        }

        private void ChatInvite_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyAllowChatInvitesPage));
        }

        private void Password_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPasswordPage));
        }

        #region Binding

        private string ConvertSync(bool sync)
        {
            return sync ? Strings.Resources.SyncContactsInfoOn : Strings.Resources.SyncContactsInfoOff;
        }

        private string ConvertP2P(int mode)
        {
            switch (mode)
            {
                case 0:
                default:
                    return Strings.Resources.LastSeenEverybody;
                case 1:
                    return Strings.Resources.LastSeenContacts;
                case 2:
                    return Strings.Resources.LastSeenNobody;
            }
        }

        private string ConvertTtl(int days)
        {
            if (days >= 365)
            {
                return Locale.Declension("Years", days / 365);
            }

            return Locale.Declension("Months", days / 30);
        }

        #endregion
    }
}
