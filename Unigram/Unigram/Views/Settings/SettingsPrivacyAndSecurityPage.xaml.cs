using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.ViewModels.Settings;
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
            DataContext = UnigramContainer.Current.ResolveType<SettingsPrivacyAndSecurityViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            while (Frame.BackStackDepth > 1)
            {
                Frame.BackStack.RemoveAt(1);
            }
        }

        private void Sessions_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsSessionsPage));
        }

        private void BlockedUsers_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsBlockedUsersPage));
        }

        private void StatusTimestamp_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyStatusTimestampPage));
        }

        private void PhoneCall_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyPhoneCallPage));
        }

        private void ChatInvite_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyChatInvitePage));
        }

        private async void Passcode_Click(object sender, RoutedEventArgs e)
        {
            var service = UnigramContainer.Current.ResolveType<IPasscodeService>();
            if (sender != null && service.IsEnabled)
            {
                var dialog = new SettingsSecurityPasscodeConfirmView();

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    Frame.Navigate(typeof(SettingsSecurityPasscodePage));
                }
            }
            else
            {
                Frame.Navigate(typeof(SettingsSecurityPasscodePage));
            }
        }

        #region Binding

        private string ConvertSync(bool sync)
        {
            return sync ? Strings.Android.SyncContactsInfoOn : Strings.Android.SyncContactsInfoOff;
        }

        #endregion

    }
}
