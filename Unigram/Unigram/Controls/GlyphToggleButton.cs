using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;

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

        #region UncheckedGlyph

        public string UncheckedGlyph
        {
            get { return (string)GetValue(UncheckedGlyphProperty); }
            set { SetValue(UncheckedGlyphProperty, value); }
        }

        public static readonly DependencyProperty UncheckedGlyphProperty =
            DependencyProperty.Register("UncheckedGlyph", typeof(string), typeof(GlyphToggleButton), new PropertyMetadata(null));

        #endregion

        protected override void OnToggle()
        {
            var binding = GetBindingExpression(IsCheckedProperty);
            if (binding != null && binding.ParentBinding.Mode == BindingMode.TwoWay)
            {
                base.OnToggle();
            }
        }
    }
}
