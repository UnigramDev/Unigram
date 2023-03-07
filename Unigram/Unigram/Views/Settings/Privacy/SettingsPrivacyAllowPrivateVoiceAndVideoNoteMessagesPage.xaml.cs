//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage : HostedPage
    {
        public SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel ViewModel => DataContext as SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesViewModel;

        public SettingsPrivacyAllowPrivateVoiceAndVideoNoteMessagesPage()
        {
            InitializeComponent();
            Title = Strings.Resources.PrivacyVoiceMessages;
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
