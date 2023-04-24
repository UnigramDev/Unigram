//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.ViewModels.Settings;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsAutoDeletePage : HostedPage
    {
        public SettingsAutoDeleteViewModel ViewModel => DataContext as SettingsAutoDeleteViewModel;

        public SettingsAutoDeletePage()
        {
            InitializeComponent();
            Title = Strings.AutoDeleteMessages;
        }

        private void OnChecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is RadioButton button && button.DataContext is SettingsOptionItem<int> option)
            {
                ViewModel.UpdateSelection(option.Value, true);
            }
        }
    }
}
