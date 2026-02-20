//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Navigation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Composition
{
    public partial class CompositionColorSource
    {
        private readonly CompositionColorBrush _brush;

        private SolidColorBrush _newValue;
        private long _token;

        public static implicit operator CompositionColorBrush(CompositionColorSource d) => d._brush;

        public CompositionColorSource(Brush value, bool connected)
        {
            if (value is SolidColorBrush newValue)
            {
                if (connected && _token == 0)
                {
                    newValue.RegisterColorChangedCallback(OnColorChanged, ref _token);
                }

                _newValue = newValue;
                _brush = BootStrapper.Current.Compositor.CreateColorBrush(newValue.Color);
            }
            else
            {
                _brush = BootStrapper.Current.Compositor.CreateColorBrush(Colors.Black);
            }
        }

        public void PropertyChanged(SolidColorBrush newValue, bool connected)
        {
            if (_newValue != null && _token != 0)
            {
                _newValue.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _token);
                _token = 0;
            }

            if (newValue == null || _brush == null)
            {
                return;
            }

            _newValue = newValue;
            _brush.Color = newValue.Color;

            if (connected)
            {
                _newValue.RegisterColorChangedCallback(OnColorChanged, ref _token);
            }
        }

        private void OnColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            var newValue = sender as SolidColorBrush;
            if (newValue == null || _brush == null)
            {
                return;
            }

            _brush.Color = newValue.Color;
        }

        public void Register()
        {
            if (_token == 0 && _brush != null)
            {
                _newValue?.RegisterColorChangedCallback(OnColorChanged, ref _token);
                OnColorChanged(_newValue, SolidColorBrush.ColorProperty);
            }
        }

        public void Unregister()
        {
            _newValue?.UnregisterColorChangedCallback(ref _token);
        }
    }
}
