//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class BlockQuote : Control
    {
        public BlockQuote()
        {
            DefaultStyleKey = typeof(BlockQuote);
        }

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(BlockQuote), new PropertyMetadata(null));

        #endregion
    }

    public class BlockCode : Control
    {
        public BlockCode()
        {
            DefaultStyleKey = typeof(BlockCode);
        }

        #region LanguageName

        public string LanguageName
        {
            get { return (string)GetValue(LanguageNameProperty); }
            set { SetValue(LanguageNameProperty, value); }
        }

        public static readonly DependencyProperty LanguageNameProperty =
            DependencyProperty.Register("LanguageName", typeof(string), typeof(BlockCode), new PropertyMetadata(null));

        #endregion
    }
}
