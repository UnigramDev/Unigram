using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Unigram.Controls
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
