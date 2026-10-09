//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Controls.Media;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class ColorsChangedEventArgs : EventArgs
    {
        public ColorsChangedEventArgs(IList<Color> newColors/*, Color[] oldColors*/)
        {
            NewColors = newColors;
            //OldColors = oldColors;
        }

        public IList<Color> NewColors { get; }

        //public Color[] OldColors { get; }
    }

    public sealed partial class ColorsPicker : Grid
    {
        public ColorsPicker()
        {
            InitializeComponent();

            RadioColor1.IsChecked = true;
        }

        public event TypedEventHandler<ColorsPicker, ColorsChangedEventArgs> ColorsChanged;

        private int _maximum = 4;
        public int Maximum
        {
            get => _maximum;
            set => _maximum = value;
        }

        private IList<Color> _colors = Array.Empty<Color>();
        public IList<Color> Colors
        {
            get => _colors;
            set => SetColors(value);
        }

        private IList<Color> GetColorsAndUpdateLayout()
        {
            var colors = GetColors();

            RemoveColorButton.Visibility = colors.Count > 1 && colors.Count < _maximum
                ? Visibility.Visible
                : Visibility.Collapsed;

            AddRemoveColorButton.Glyph = colors.Count < _maximum ? Icons.Add : Icons.Dismiss;

            return colors;
        }

        private IList<Color> GetColors()
        {
            if (!_color1.IsEmpty && !_color2.IsEmpty)
            {
                if (!_color3.IsEmpty && !_color4.IsEmpty)
                {
                    return [_color1, _color2, _color3, _color4];
                }
                else if (!_color3.IsEmpty)
                {
                    return [_color1, _color2, _color3];
                }

                return [_color1, _color2];
            }
            else if (!_color1.IsEmpty)
            {
                return [_color1];
            }
            else if (!_color2.IsEmpty)
            {
                return [_color2];
            }

            return Array.Empty<Color>();
        }

        private void SetColors(IList<Color> value)
        {
            var color1 = BackgroundColor.Empty;
            var color2 = BackgroundColor.Empty;
            var color3 = BackgroundColor.Empty;
            var color4 = BackgroundColor.Empty;

            if (value?.Count > 0)
            {
                color1 = value[0];
            }
            if (value?.Count > 1)
            {
                color2 = value[1];
            }
            if (value?.Count > 2)
            {
                color3 = value[2];
            }
            if (value?.Count > 3)
            {
                color4 = value[3];
            }

            _colorsChanging = true;
            SetColor1(color1);
            SetColor2(color2);
            SetColor3(color3);
            SetColor4(color4);

            _colors = GetColorsAndUpdateLayout();
            _colorsChanging = false;
        }

        private bool _colorsChanging;

        private BackgroundColor _color1;
        private BackgroundColor _color2;
        private BackgroundColor _color3;
        private BackgroundColor _color4;

        private void SetColor1(BackgroundColor color)
        {
            if (_color1.Value != color.Value || _color1.IsEmpty != color.IsEmpty)
            {
                _color1 = color;
                BrushColor1.Color = color;

                RaiseColorsChanged();
            }
        }

        private void SetColor2(BackgroundColor color)
        {
            if (_color2.Value != color.Value || _color2.IsEmpty != color.IsEmpty)
            {
                _color2 = color;
                BrushColor2.Color = color;
                RadioColor2.Visibility = color.IsEmpty ? Visibility.Collapsed : Visibility.Visible;

                RaiseColorsChanged();
            }
        }

        private void SetColor3(BackgroundColor color)
        {
            if (_color3.Value != color.Value || _color3.IsEmpty != color.IsEmpty)
            {
                _color3 = color;
                BrushColor3.Color = color;
                RadioColor3.Visibility = color.IsEmpty ? Visibility.Collapsed : Visibility.Visible;

                RaiseColorsChanged();
            }
        }

        private void SetColor4(BackgroundColor color)
        {
            if (_color4.Value != color.Value || _color4.IsEmpty != color.IsEmpty)
            {
                _color4 = color;
                BrushColor4.Color = color;
                RadioColor4.Visibility = color.IsEmpty ? Visibility.Collapsed : Visibility.Visible;

                RaiseColorsChanged();
            }
        }

        private void RaiseColorsChanged()
        {
            _colors = GetColorsAndUpdateLayout();

            if (!_colorsChanging)
            {
                ColorsChanged?.Invoke(this, new ColorsChangedEventArgs(_colors));
            }
        }

        private void TextColor_ColorChanged(ColorTextBox sender, Controls.ColorChangedEventArgs args)
        {
            if (sender.FocusState == FocusState.Unfocused)
            {
                return;
            }

            PickerColor.Color = args.NewColor;
        }

        private void RadioColor_Toggled(object sender, RoutedEventArgs e)
        {
            var row = Grid.GetRow(this);
            if (row != 2)
            {
                return;
            }

            if (RadioColor1.IsChecked == true)
            {
                PickerColor.Color = _color1;
            }
            else if (RadioColor2.IsChecked == true)
            {
                PickerColor.Color = _color2;
            }
            else if (RadioColor3.IsChecked == true)
            {
                PickerColor.Color = _color3;
            }
            else if (RadioColor4.IsChecked == true)
            {
                PickerColor.Color = _color4;
            }

            TextColor1.SelectAll();
        }

        private void PickerColor_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            var row = Grid.GetRow(this);
            if (row != 2)
            {
                return;
            }

            TextColor1.Color = args.NewColor;

            if (RadioColor1.IsChecked == true)
            {
                SetColor1(args.NewColor);
            }
            else if (RadioColor2.IsChecked == true)
            {
                SetColor2(args.NewColor);
            }
            else if (RadioColor3.IsChecked == true)
            {
                SetColor3(args.NewColor);
            }
            else if (RadioColor4.IsChecked == true)
            {
                SetColor4(args.NewColor);
            }
        }

        private void RemoveColor_Click(object sender, RoutedEventArgs e)
        {
            if (RadioColor1.IsChecked == true)
            {
                RemoveColor(0);
            }
            else if (RadioColor2.IsChecked == true)
            {
                RemoveColor(1);
            }
            else if (RadioColor3.IsChecked == true)
            {
                RemoveColor(2);
            }
            else if (RadioColor4.IsChecked == true)
            {
                RemoveColor(3);
            }
        }

        private void AddRemoveColor_Click(object sender, RoutedEventArgs e)
        {
            if (_colors.Count < _maximum)
            {
                AddColor();
            }
            else if (RadioColor1.IsChecked == true)
            {
                RemoveColor(0);
            }
            else if (RadioColor2.IsChecked == true)
            {
                RemoveColor(1);
            }
            else if (RadioColor3.IsChecked == true)
            {
                RemoveColor(2);
            }
            else if (RadioColor4.IsChecked == true)
            {
                RemoveColor(3);
            }
        }

        private void RemoveColor(int index)
        {
            if (index <= 0)
            {
                SetColor1(_color2);
            }

            if (index <= 1)
            {
                SetColor2(_color3);
            }

            if (index <= 2)
            {
                SetColor3(_color4);
            }

            if (index <= 3)
            {
                SetColor4(BackgroundColor.Empty);
            }

            RadioColor1.IsChecked = true;
        }

        private void AddColor()
        {
            if (_color2.IsEmpty)
            {
                SetColor2(_color1);
                RadioColor2.IsChecked = true;
            }
            else if (_color3.IsEmpty)
            {
                SetColor3(_color2);
                RadioColor3.IsChecked = true;
            }
            else if (_color4.IsEmpty)
            {
                SetColor4(_color3);
                RadioColor4.IsChecked = true;
            }
        }

        public void SelectAll()
        {
            RadioColor_Toggled(null, null);
        }
    }
}
