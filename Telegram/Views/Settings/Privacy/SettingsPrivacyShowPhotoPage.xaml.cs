//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Settings.Privacy;
using Windows.UI.Xaml;

namespace Telegram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowPhotoPage : HostedPage, IUserDelegate
    {
        public SettingsPrivacyShowPhotoViewModel ViewModel => DataContext as SettingsPrivacyShowPhotoViewModel;

        public SettingsPrivacyShowPhotoPage()
        {
            InitializeComponent();
            Title = Strings.PrivacyProfilePhoto;
        }

        #region Delegate

        public void UpdateUser(Chat chat, User user, bool secret) { }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (fullInfo != null)
            {
                UpdatePhoto.Content = fullInfo.PublicPhoto == null
                    ? Strings.SetPhotoForRest
                    : Strings.UpdatePhotoForRest;

                RemovePhoto.Visibility = fullInfo.PublicPhoto == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        public void UpdateUserStatus(Chat chat, User user) { }

        #endregion

        #region Binding

        private Visibility ConvertNever(PrivacyValue value)
        {
            return value is PrivacyValue.AllowAll or PrivacyValue.AllowContacts ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertAlways(PrivacyValue value)
        {
            return value is PrivacyValue.AllowContacts or PrivacyValue.DisallowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

    }
}
