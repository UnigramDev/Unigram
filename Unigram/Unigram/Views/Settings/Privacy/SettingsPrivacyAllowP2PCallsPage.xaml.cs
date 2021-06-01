using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Windows.UI.Xaml;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyAllowP2PCallsPage : HostedPage
    {
        public SettingsPrivacyAllowP2PCallsViewModel ViewModel => DataContext as SettingsPrivacyAllowP2PCallsViewModel;

        public SettingsPrivacyAllowP2PCallsPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsPrivacyAllowP2PCallsViewModel>();
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
