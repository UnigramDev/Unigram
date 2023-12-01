//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

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

        private void LogOut_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogOutPage));
        }

        #region Delegate

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Photo.SetUser(ViewModel.ClientService, user, 96);

            if (ViewModel.ClientService.Options.TestMode || SettingsService.Current.Diagnostics.HidePhoneNumber)
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

            ProfileColor.SetUser(ViewModel.ClientService, user);
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {

        }

        public void UpdateUserStatus(Chat chat, User user) { }

        #endregion
    }
}
