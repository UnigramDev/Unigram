//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Telegram.Td.Api;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowPhotoPage : HostedPage, IUserDelegate
    {
        public SettingsPrivacyShowPhotoViewModel ViewModel => DataContext as SettingsPrivacyShowPhotoViewModel;

        public SettingsPrivacyShowPhotoPage()
        {
            InitializeComponent();
            Title = Strings.Resources.PrivacyProfilePhoto;
        }

        #region Delegate

        public void UpdateUser(Chat chat, User user, bool secret) { }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (fullInfo != null)
            {
                UpdatePhoto.Content = fullInfo.PublicPhoto == null
                    ? Strings.Resources.SetPhotoForRest
                    : Strings.Resources.UpdatePhotoForRest;

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
