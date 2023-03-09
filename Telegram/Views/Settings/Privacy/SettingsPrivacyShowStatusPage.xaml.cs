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
    public sealed partial class SettingsPrivacyShowStatusPage : HostedPage
    {
        public SettingsPrivacyShowStatusViewModel ViewModel => DataContext as SettingsPrivacyShowStatusViewModel;

        public SettingsPrivacyShowStatusPage()
        {
            InitializeComponent();
            Title = Strings.Resources.PrivacyLastSeen;
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

        #endregion

    }
}
