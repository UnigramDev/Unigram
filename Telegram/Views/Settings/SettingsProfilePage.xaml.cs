//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Settings;
using Telegram.Views.Settings.Privacy;
using Windows.UI.Xaml;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsProfilePage : HostedPage, IUserDelegate
    {
        public SettingsProfileViewModel ViewModel => DataContext as SettingsProfileViewModel;

        public SettingsProfilePage()
        {
            InitializeComponent();
            Title = Strings.EditInformation;
        }

        private async void LogOut_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.ShowPopupAsync(typeof(LogOutPopup));
        }

        #region Delegate

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Photo.SetUser(ViewModel.ClientService, user, 96);

            if (SettingsService.Current.Diagnostics.HidePhoneNumber)
            {
                PhoneNumber.Badge = "+42 --- --- ----";
            }
            else
            {
                PhoneNumber.Badge = Common.PhoneNumber.Format(user.PhoneNumber);
            }

            if (user.HasActiveUsername(out string username))
            {
                Username.Badge = username;
            }
            else
            {
                Username.Badge = Strings.UsernameEmpty;
            }

            if (ViewModel.IsPremiumAvailable)
            {
                ProfileColor.SetUser(ViewModel.ClientService, user);
            }
            else
            {
                NameColor.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (fullInfo.Birthdate != null)
            {
                Birthdate.Badge = Formatter.Birthdate(fullInfo.Birthdate);
                BirthdateRemove.Visibility = Visibility.Visible;
            }
            else
            {
                Birthdate.Badge = Strings.EditProfileBirthdayAdd;
                BirthdateRemove.Visibility = Visibility.Collapsed;
            }

            if (ViewModel.ClientService.TryGetChat(fullInfo.PersonalChatId, out Chat personalChat))
            {
                PersonalChannel.Badge = personalChat.Title;
            }
            else
            {
                PersonalChannel.Badge = Strings.EditProfileChannelAdd;
            }
        }

        public void UpdateUserStatus(Chat chat, User user) { }

        #endregion

        #region Binding

        private string ConvertBirthdateFooter(bool contactsOnly)
        {
            return contactsOnly
                ? Strings.EditProfileBirthdayInfoContacts
                : Strings.EditProfileBirthdayInfo;
        }

        #endregion

        private void BioPrivacy_Click(object sender, TextUrlClickEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(SettingsPrivacyShowBioPage));
        }

        private void BirthdatePrivacy_Click(object sender, TextUrlClickEventArgs e)
        {
            ViewModel.NavigationService.Navigate(typeof(SettingsPrivacyShowBirthdatePage));
        }
    }
}
