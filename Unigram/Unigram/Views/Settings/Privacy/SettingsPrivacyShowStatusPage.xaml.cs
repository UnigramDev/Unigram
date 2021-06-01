using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Windows.UI.Xaml;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyShowStatusPage : HostedPage
    {
        public SettingsPrivacyShowStatusViewModel ViewModel => DataContext as SettingsPrivacyShowStatusViewModel;

        public SettingsPrivacyShowStatusPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPrivacyShowStatusViewModel>();
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
