//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Controls;
using Windows.UI;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseColorPopup : ContentPopup
    {
        public ChooseColorPopup()
        {
            InitializeComponent();

            Title = Strings.ColorPickerMainColor;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;
            CloseButtonText = Strings.UseSystemAccentColor;
            CloseButtonResult = ContentDialogResult.Primary;
            CloseButtonClick += ContentDialog_CloseButtonClick;
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
            get => CloseButtonText.Length > 0;
            set => CloseButtonText = value ? Strings.UseSystemAccentColor : string.Empty;
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

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _color = default;
        }
    }
}
