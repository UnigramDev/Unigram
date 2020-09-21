using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
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
            get { return (string)GetValue(CheckedGlyphProperty); }
            set { SetValue(CheckedGlyphProperty, value); }
        }

        public static readonly DependencyProperty CheckedGlyphProperty =
            DependencyProperty.Register("CheckedGlyph", typeof(string), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region IndeterminateGlyph

        public string IndeterminateGlyph
        {
            get { return (string)GetValue(IndeterminateGlyphProperty); }
            set { SetValue(IndeterminateGlyphProperty, value); }
        }

        public static readonly DependencyProperty IndeterminateGlyphProperty =
            DependencyProperty.Register("IndeterminateGlyph", typeof(string), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region CheckedForeground

        public Brush CheckedForeground
        {
            get { return (Brush)GetValue(CheckedForegroundProperty); }
            set { SetValue(CheckedForegroundProperty, value); }
        }

        public static readonly DependencyProperty CheckedForegroundProperty =
            DependencyProperty.Register("CheckedForeground", typeof(Brush), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region UncheckedForeground

        public Brush UncheckedForeground
        {
            get { return (Brush)GetValue(UncheckedForegroundProperty); }
            set { SetValue(UncheckedForegroundProperty, value); }
        }

        public static readonly DependencyProperty UncheckedForegroundProperty =
            DependencyProperty.Register("UncheckedForeground", typeof(Brush), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        #region IsOneWay

        public bool IsOneWay
        {
            get { return (bool)GetValue(IsOneWayProperty); }
            set { SetValue(IsOneWayProperty, value); }
        }

        public static readonly DependencyProperty IsOneWayProperty =
            DependencyProperty.Register("IsOneWay", typeof(bool), typeof(GlyphToggleButton), new PropertyMetadata(true));

        #endregion

        #region Radius

        public CornerRadius Radius
        {
            get { return (CornerRadius)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register("Radius", typeof(CornerRadius), typeof(GlyphToggleButton), new PropertyMetadata(default(CornerRadius)));

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
