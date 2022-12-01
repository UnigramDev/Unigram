using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.ViewModels.Settings;
using Unigram.Views.Settings.Privacy;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsPrivacyAndSecurityPage : HostedPage
    {
        public SettingsPrivacyAndSecurityViewModel ViewModel => DataContext as SettingsPrivacyAndSecurityViewModel;

        public SettingsPrivacyAndSecurityPage()
        {
            InitializeComponent();
            Title = Strings.Resources.PrivacySettings;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (ApiInfo.IsPackagedRelease && ViewModel.ClientService.Options.CanIgnoreSensitiveContentRestrictions)
            {
                FindName(nameof(SensitiveContent));
            }
        }

        private void WebSessions_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsWebSessionsPage));
        }

        private void BlockedUsers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsBlockedChatsPage));
        }

        private void ShowPhone_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyPhonePage));
        }

        private void StatusTimestamp_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyShowStatusPage));
        }

        private void ProfilePhoto_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyShowPhotoPage));
        }

        private void Forwards_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyShowForwardedPage));
        }

        private void PhoneCall_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyAllowCallsPage));
        }

        private void ChatInvite_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyAllowChatInvitesPage));
        }

        private async void VoiceMessages_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ClientService.IsPremium)
            {
                Frame.Navigate(typeof(SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage));
            }
            else if (ViewModel.ClientService.IsPremiumAvailable)
            {
                var confirm = await MessagePopup.ShowAsync(Strings.Resources.PrivacyVoiceMessagesPremiumOnly, Strings.Resources.PrivacyVoiceMessages, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    ViewModel.NavigationService.ShowPromo(/*new PremiumSourceFeature(new PremiumFeaturePrivateVoiceAndVideoMessages)*/);
                }
            }
        }

        #region Binding

        private string ConvertVoiceMessagesChevron(bool premium)
        {
            return premium ? Icons.ChevronRight : Icons.LockClosed;
        }

        private string ConvertOnOff(bool value)
        {
            return value ? Strings.Resources.NotificationsOn : Strings.Resources.NotificationsOff;
        }

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
            if (days == 0)
            {
                return Strings.Resources.NotificationsOff;
            }

            return Locale.FormatTtl(days);
        }

        #endregion
    }
}
