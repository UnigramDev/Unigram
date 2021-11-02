using Microsoft.UI.Xaml.Controls;
using Unigram.Common;
using Unigram.ViewModels.Settings;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsAdvancedPage : HostedPage
    {
        public SettingsAdvancedViewModel ViewModel => DataContext as SettingsAdvancedViewModel;

        public SettingsAdvancedPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsAdvancedViewModel>();

            if (ApiInfo.IsPackagedRelease)
            {
                FindName(nameof(UpdatePanel));
            }

#if DESKTOP_BRIDGE
            FindName(nameof(TraySwitch));

            if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                TraySwitch.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else
            {
                TraySwitch.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            }
#endif
        }

        private void Shortcuts_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsShortcutsPage));
        }
    }
}
