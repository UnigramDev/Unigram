//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Unigram.Common;
using Unigram.ViewModels.Settings;
using Windows.Foundation.Metadata;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsAdvancedPage : HostedPage
    {
        public SettingsAdvancedViewModel ViewModel => DataContext as SettingsAdvancedViewModel;

        public SettingsAdvancedPage()
        {
            InitializeComponent();
            Title = Strings.Resources.PrivacyAdvanced;

            if (ApiInfo.IsPackagedRelease)
            {
                FindName(nameof(UpdatePanel));
            }

#if !DESKTOP_BRIDGE
            if (ApiInformation.IsTypePresent("Windows.ApplicationModel.FullTrustProcessLauncher"))
            {
                FindName(nameof(TraySwitch));
            }
#endif
        }

        private void Shortcuts_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsShortcutsPage));
        }
    }
}
