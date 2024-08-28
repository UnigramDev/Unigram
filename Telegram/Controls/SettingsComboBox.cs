//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Telegram.Views.Popups;

namespace Telegram.Controls
{
    public partial class SettingsComboBox : ComboBox
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
