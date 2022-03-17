using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Windows.UI.Xaml;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyPhonePage : HostedPage
    {
        public SettingsPrivacyPhoneViewModel ViewModel => DataContext as SettingsPrivacyPhoneViewModel;

        public SettingsPrivacyPhonePage()
        {
            InitializeComponent();
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

        private string ConvertFooter(PrivacyValue value)
        {
            if (value == PrivacyValue.AllowAll)
            {
                return Strings.Resources.PrivacyPhoneInfo2;
            }

            return Strings.Resources.PrivacyPhoneInfo3;
        }

        #endregion

    }
}
