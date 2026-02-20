//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Composition
{
    public partial class CompositionPropertySetColorSource
    {
        private readonly CompositionPropertySet _visual;
        private readonly string _propertyName;

        private SolidColorBrush _newValue;
        private long _token;

        public CompositionPropertySetColorSource(Brush value, CompositionPropertySet visual, string propertyName, bool connected)
        {
            _visual = visual;
            _propertyName = propertyName;

            if (value is SolidColorBrush newValue)
            {
                if (connected && _token == 0)
                {
                    newValue.RegisterColorChangedCallback(OnColorChanged, ref _token);
                }

                _newValue = newValue;
                _visual.InsertVector4(_propertyName, ColorAsVector4(newValue.Color));
            }
            //else
            //{
            //    _visual.InsertVector4(_propertyName, ColorAsVector4(Colors.Black));
            //}
        }

        public void PropertyChanged(SolidColorBrush newValue, bool connected)
        {
            if (_newValue != null && _token != 0)
            {
                _newValue.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _token);
                _token = 0;
            }

            if (newValue == null)
            {
                return;
            }

            _newValue = newValue;
            _visual.InsertVector4(_propertyName, ColorAsVector4(newValue.Color));

            if (connected)
            {
                _newValue.RegisterColorChangedCallback(OnColorChanged, ref _token);
            }
        }

        private void OnColorChanged(DependencyObject sender, DependencyProperty dp)
        {
            var newValue = sender as SolidColorBrush;
            if (newValue == null)
            {
                return;
            }

            _visual.InsertVector4(_propertyName, ColorAsVector4(newValue.Color));
        }

        public void Register()
        {
            if (_token == 0)
            {
                _newValue?.RegisterColorChangedCallback(OnColorChanged, ref _token);
                OnColorChanged(_newValue, SolidColorBrush.ColorProperty);
            }
        }

        public void Unregister()
        {
            _newValue?.UnregisterColorChangedCallback(ref _token);
        }

        public static Vector4 ColorAsVector4(Color color)
        {
            return new Vector4(color.R, color.G, color.B, color.A);
        }
    }
}
