using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class GlyphRadioButton : RadioButton
    {
        public GlyphRadioButton()
        {
            DefaultStyleKey = typeof(GlyphRadioButton);
        }

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(GlyphRadioButton), new PropertyMetadata(null));

        #endregion

        #region CheckedGlyph

        public string CheckedGlyph
        {
            get { return (string)GetValue(CheckedGlyphProperty); }
            set { SetValue(CheckedGlyphProperty, value); }
        }

        public static readonly DependencyProperty CheckedGlyphProperty =
            DependencyProperty.Register("CheckedGlyph", typeof(string), typeof(GlyphRadioButton), new PropertyMetadata(null));

        #endregion

        #region Radius

        public CornerRadius Radius
        {
            get { return (CornerRadius)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register("Radius", typeof(CornerRadius), typeof(GlyphRadioButton), new PropertyMetadata(default(CornerRadius)));

        #endregion

    }
}
