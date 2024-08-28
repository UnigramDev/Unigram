//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class AnimatedIconButton : AnimatedGlyphButton
    {
        public AnimatedIconButton()
        {
            DefaultStyleKey = typeof(AnimatedIconButton);
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
        }

        protected override bool IsRuntimeCompatible()
        {
            return Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 11);
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (Source != null && Foreground is SolidColorBrush foreground)
            {
                Source.SetColorProperty("Color_000000", foreground.Color);
            }
        }

        #region Source

        public IAnimatedVisualSource2 Source
        {
            get { return (IAnimatedVisualSource2)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(IAnimatedVisualSource2), typeof(AnimatedIconToggleButton), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as AnimatedIconToggleButton;
            var newValue = e.NewValue as IAnimatedVisualSource2;

            if (newValue != null && sender?.Foreground is SolidColorBrush foreground)
            {
                newValue.SetColorProperty("Color_000000", foreground.Color);
            }
        }

        #endregion
    }
}
