//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Unigram.Controls;
using Windows.UI;

namespace Unigram.Views.Popups
{
    public sealed partial class ChooseColorPopup : ContentPopup
    {
        public ChooseColorPopup()
        {
            InitializeComponent();

            Title = Strings.Resources.ColorPickerMainColor;
            PrimaryButtonText = Strings.Resources.OK;
            SecondaryButtonText = Strings.Resources.Cancel;
        }

        private Color _color;
        public Color Color
        {
            get => _color;
            set => Picker.Color = _color = value;
        }

        public bool IsTransparencyEnabled
        {
            get => TextField.IsTransparencyEnabled;
            set => TextField.IsTransparencyEnabled = value;
        }

        public bool IsAccentColorVisible
        {
            get => Accent.Visibility == Visibility.Visible;
            set => Accent.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void TextField_ColorChanged(ColorTextBox sender, Controls.ColorChangedEventArgs args)
        {
            _color = args.NewColor;
            Picker.Color = args.NewColor;
        }

        private void Picker_ColorChanged(Controls.ColorPicker sender, Controls.ColorChangedEventArgs args)
        {
            _color = args.NewColor;
            TextField.Color = args.NewColor;
        }

        private void System_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            _color = default;
            Hide(ContentDialogResult.Primary);
        }
    }
}
