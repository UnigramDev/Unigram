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
            DataContext = TLContainer.Current.Resolve<SettingsPrivacyAllowCallsViewModel>();
        }

        #region Binding

        private Visibility ConvertNever(PrivacyValue value)
        {
            return value == PrivacyValue.AllowAll || value == PrivacyValue.AllowContacts ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility ConvertAlways(PrivacyValue value)
        {
            return value == PrivacyValue.AllowContacts || value == PrivacyValue.DisallowAll ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        private void P2PCall_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPrivacyAllowP2PCallsPage));
        }
    }
}
