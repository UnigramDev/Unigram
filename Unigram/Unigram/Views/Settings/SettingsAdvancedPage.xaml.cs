using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels.Settings;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsAdvancedPage : Page
    {
        public SettingsAdvancedViewModel ViewModel => DataContext as SettingsAdvancedViewModel;

        public SettingsAdvancedPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SettingsAdvancedViewModel>();

            if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                TraySwitch.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else
            {
                TraySwitch.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        private void OnSizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            AdaptiveWide.Visibility = e.NewSize.Width >= 880 ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void VoIP_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsVoIPPage));
        }
    }
}
