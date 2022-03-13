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
            Title = Strings.Resources.PrivacyPhone;
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
                return Strings.Resources.PrivacyPhoneInfo2;
            }

            return Strings.Resources.PrivacyPhoneInfo3;
        }

        #endregion

    }
}
