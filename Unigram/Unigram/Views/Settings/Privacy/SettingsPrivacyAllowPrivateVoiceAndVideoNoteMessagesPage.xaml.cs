using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Windows.UI.Xaml;

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
