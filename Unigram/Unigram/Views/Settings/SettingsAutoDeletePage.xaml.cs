//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using Unigram.ViewModels.Settings;
using Unigram.Views.Popups;

namespace Unigram.Views.Settings
{
    public sealed partial class SettingsAutoDeletePage : HostedPage
    {
        public SettingsAutoDeleteViewModel ViewModel => DataContext as SettingsAutoDeleteViewModel;

        public SettingsAutoDeletePage()
        {
            InitializeComponent();
            Title = Strings.Resources.AutoDeleteMessages;
        }

        private void OnChecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (sender is RadioButton button && button.DataContext is SettingsOptionItem<int> option)
            {
                ViewModel.UpdateSelection(option.Value, true);
            }
        }
    }
}
