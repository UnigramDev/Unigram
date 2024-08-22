//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Telegram.Controls
{
    public class GlyphToggleButton : ToggleButton
    {
        public GlyphToggleButton()
        {
            DefaultStyleKey = typeof(GlyphToggleButton);
        }

        #region CheckedGlyph

        public string CheckedGlyph
        {
            get => (string)GetValue(CheckedGlyphProperty);
            set => SetValue(CheckedGlyphProperty, value);
        }

        public static readonly DependencyProperty CheckedGlyphProperty =
            DependencyProperty.Register("CheckedGlyph", typeof(string), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region Glyph

        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region IndeterminateGlyph

        public string IndeterminateGlyph
        {
            get => (string)GetValue(IndeterminateGlyphProperty);
            set => SetValue(IndeterminateGlyphProperty, value);
        }

        public static readonly DependencyProperty IndeterminateGlyphProperty =
            DependencyProperty.Register("IndeterminateGlyph", typeof(string), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region CheckedForeground

        public Brush CheckedForeground
        {
            get => (Brush)GetValue(CheckedForegroundProperty);
            set => SetValue(CheckedForegroundProperty, value);
        }

        public static readonly DependencyProperty CheckedForegroundProperty =
            DependencyProperty.Register("CheckedForeground", typeof(Brush), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region UncheckedForeground

        public Brush UncheckedForeground
        {
            get => (Brush)GetValue(UncheckedForegroundProperty);
            set => SetValue(UncheckedForegroundProperty, value);
        }

        public static readonly DependencyProperty UncheckedForegroundProperty =
            DependencyProperty.Register("UncheckedForeground", typeof(Brush), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region IsOneWay

        public bool IsOneWay
        {
            get => (bool)GetValue(IsOneWayProperty);
            set => SetValue(IsOneWayProperty, value);
        }

        public static readonly DependencyProperty IsOneWayProperty =
            DependencyProperty.Register("IsOneWay", typeof(bool), typeof(GlyphToggleButton), new PropertyMetadata(true));

        #endregion

        protected override void OnToggle()
        {
            if (IsOneWay)
            {
                var binding = GetBindingExpression(IsCheckedProperty);
                if (binding != null && binding.ParentBinding.Mode == BindingMode.TwoWay)
                {
                    base.OnToggle();
                }
            }
            else
            {
                base.OnToggle();
            }
        }
    }
}
