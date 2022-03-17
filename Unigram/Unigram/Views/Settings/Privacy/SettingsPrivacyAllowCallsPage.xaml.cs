using Unigram.ViewModels.Settings;
using Unigram.ViewModels.Settings.Privacy;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings.Privacy
{
    public sealed partial class SettingsPrivacyAllowCallsPage : HostedPage
    {
        public SettingsPrivacyAllowCallsViewModel ViewModel => DataContext as SettingsPrivacyAllowCallsViewModel;

        public SettingsPrivacyAllowCallsPage()
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

        #endregion

        private void P2PCall_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyAllowP2PCallsPage));
        }
    }
}
