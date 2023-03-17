//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class SettingsComboBox : ComboBox
    {
        private TextBlock PlaceholderTextBlock;

        public SettingsComboBox()
        {
            DefaultStyleKey = typeof(SettingsComboBox);
            SelectionChanged += OnSelectionChanged;
        }

        protected override void OnApplyTemplate()
        {
            PlaceholderTextBlock = GetTemplateChild(nameof(PlaceholderTextBlock)) as TextBlock;
            PlaceholderTextBlock.Padding = new Thickness(0, 0, 6, 0);

            base.OnApplyTemplate();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedItem is SettingsOptionItem item)
            {
                PlaceholderText = item.Text;
            }
        }
    }
}
