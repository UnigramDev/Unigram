using Unigram.ViewModels.Settings;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsAdvancedPage : HostedPage
    {
        public SettingsAdvancedViewModel ViewModel => DataContext as SettingsAdvancedViewModel;

        public SettingsAdvancedPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsAdvancedViewModel>();

            if (Package.Current.SignatureKind != PackageSignatureKind.Store)
            {
                FindName(nameof(UpdatePanel));
            }

#if DESKTOP_BRIDGE
            FindName(nameof(TraySwitch));

            if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                TraySwitch.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else
            {
                TraySwitch.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
#endif
        }

        private void Shortcuts_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsShortcutsPage));
        }

        private void VoIP_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsVoIPPage));
        }
    }
}
