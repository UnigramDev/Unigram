//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Td;
using Telegram.ViewModels.Settings;
using Telegram.ViewModels.Settings.Privacy;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage : HostedPage
    {
        public SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel ViewModel => DataContext as SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel;

        public SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage()
        {
            InitializeComponent();
            Title = Strings.PrivacyVoiceMessages;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (ViewModel.IsPremium)
            {
                Group.Footer = Strings.PrivacyVoiceMessagesInfo;
            }
            else
            {
                var formatted = Extensions.ReplacePremiumLink(Strings.PrivacyVoiceMessagesPremiumOnly2, null);
                var markdown = ClientEx.GetMarkdownText(formatted);

                Group.Footer = markdown.Text;
            }
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
