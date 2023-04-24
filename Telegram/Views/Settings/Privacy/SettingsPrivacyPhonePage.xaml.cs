//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Settings.Privacy;
using Windows.UI.Xaml;

namespace Telegram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyPhonePage : HostedPage
    {
        public SettingsPrivacyPhoneViewModel ViewModel => DataContext as SettingsPrivacyPhoneViewModel;

        public SettingsPrivacyPhonePage()
        {
            InitializeComponent();
            Title = Strings.PrivacyPhone;
        }

        #region Binding

        private Visibility ConvertNever(PrivacyValue value)
        {
            return value is PrivacyValue.AllowAll or PrivacyValue.AllowContacts ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertAlways(PrivacyValue value)
        {
            return value is PrivacyValue.AllowContacts or PrivacyValue.DisallowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertFinding(PrivacyValue value)
        {
            return value == PrivacyValue.DisallowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertPhoneLink(PrivacyValue value1, PrivacyValue value2)
        {
            if (value1 is PrivacyValue.AllowAll or PrivacyValue.AllowContacts)
            {
                return Visibility.Visible;
            }

            return value2 == PrivacyValue.AllowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        private Thickness ConvertPhoneLinkMargin(PrivacyValue value)
        {
            if (value is PrivacyValue.AllowAll or PrivacyValue.AllowContacts)
            {
                return new Thickness(24, -16, 24, 0);
            }

            return new Thickness(24, 0, 24, 0);
        }

        private string ConvertFooter(PrivacyValue value)
        {
            if (value == PrivacyValue.AllowAll)
            {
                return Strings.PrivacyPhoneInfo2;
            }

            return Strings.PrivacyPhoneInfo3;
        }

        #endregion

    }
}
