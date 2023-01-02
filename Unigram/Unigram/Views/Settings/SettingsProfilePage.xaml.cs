using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Settings;
using Unigram.Views.Settings.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsProfilePage : HostedPage, IUserDelegate
    {
        public SettingsProfileViewModel ViewModel => DataContext as SettingsProfileViewModel;

        public SettingsProfilePage()
        {
            InitializeComponent();
            Title = Strings.Resources.lng_settings_information;
        }

        private async void Phone_Click(object sender, RoutedEventArgs e)
        {
            var popup = new ChangePhoneNumberPopup();

            var change = await popup.ShowQueuedAsync();
            if (change != ContentDialogResult.Primary)
            {
                return;
            }

            var confirm = await MessagePopup.ShowAsync(Strings.Resources.PhoneNumberAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                Frame.Navigate(typeof(SettingsPhonePage));
            }
        }

        private async void Username_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.NavigationService.ShowAsync(typeof(SettingsUsernamePopup));
        }

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOutPage));
        }

        #region Delegate

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Photo.SetUser(ViewModel.ClientService, user, 140);

#if DEBUG
            PhoneNumber.Badge = "+42 --- --- ----";
#else
            if (ViewModel.Settings.UseTestDC)
            {
                PhoneNumber.Badge = "+42 --- --- ----";
            }
            else
            {
                PhoneNumber.Badge = Common.PhoneNumber.Format(user.PhoneNumber);
            }
#endif

            if (user.HasActiveUsername(out string username))
            {
                Username.Badge = username;
            }
            else
            {
                Username.Badge = Strings.Resources.UsernameEmpty;
            }
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {

        }

        public void UpdateUserStatus(Chat chat, User user) { }

        #endregion
    }
}
