//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Users;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Users
{
    public sealed partial class UserEditPage : HostedPage, IUserDelegate
    {
        public UserEditViewModel ViewModel => DataContext as UserEditViewModel;

        public UserEditPage()
        {
            InitializeComponent();
            Title = Strings.EditContact;
        }

        #region Delegate

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Photo.SetUser(ViewModel.ClientService, user, 140);

            if (user.Type is UserTypeBot userTypeBot && userTypeBot.CanBeEdited)
            {
                FindName(nameof(BotPhoto));

                FindName(nameof(About));

                FindName(nameof(UsernamePanel));
                FindName(nameof(BotPanel));

                LayoutRoot.Footer = string.Empty;

                Username.Badge = user.ActiveUsername();

                FirstName.PlaceholderText = Strings.BotName;
                FirstName.VerticalAlignment = VerticalAlignment.Center;
                FirstName.Margin = new Thickness();

                Grid.SetRowSpan(FirstName, 2);
            }
            else
            {
                FindName(nameof(PhotoPanel));
                FindName(nameof(LastName));

                SuggestPhoto.Content = string.Format(Strings.SuggestPhotoFor, user.FirstName);
                PersonalPhoto.Content = string.Format(Strings.SetPhotoFor, user.FirstName);
            }
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (ResetPhoto != null)
            {
                ResetPhoto.Visibility = fullInfo.PersonalPhoto == null ? Visibility.Collapsed : Visibility.Visible;
            }

            if (fullInfo.NeedPhoneNumberPrivacyException)
            {
                FindName(nameof(SharePhonePanel));

                SharePhoneCheck.Content = string.Format(Strings.SharePhoneNumberWith, user.FirstName);
            }
        }

        public void UpdateUserStatus(Chat chat, User user) { }

        #endregion
    }
}
