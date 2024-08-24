//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Telegram.Common;
using Telegram.ViewModels.Settings;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsAdvancedPage : HostedPage
    {
        public SettingsAdvancedViewModel ViewModel => DataContext as SettingsAdvancedViewModel;

        public SettingsAdvancedPage()
        {
            InitializeComponent();
            Title = Strings.PrivacyAdvanced;

            if (ApiInfo.IsPackagedRelease)
            {
                FindName(nameof(UpdatePanel));
            }
        }

        private void Shortcuts_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsShortcutsPage));
        }
    }
}
