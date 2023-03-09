//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class GlyphButton : Button
    {
        public GlyphButton()
        {
            DefaultStyleKey = typeof(GlyphButton);
        }

        #region Glyph
        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(GlyphButton), new PropertyMetadata(null));
        #endregion
    }

    public class GlyphHyperlinkButton : HyperlinkButton
    {
        public GlyphHyperlinkButton()
        {
            DefaultStyleKey = typeof(GlyphHyperlinkButton);
        }

        #region Glyph
        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(GlyphHyperlinkButton), new PropertyMetadata(null));
        #endregion
    }

}
