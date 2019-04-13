using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class GlyphMenuFlyoutItem : MenuFlyoutItem
    {
        public GlyphMenuFlyoutItem()
        {
            //DefaultStyleKey = typeof(GlyphMenuFlyoutItem);
        }

        public IconElement Glyph
        {
            get { return (IconElement)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(IconElement), typeof(GlyphMenuFlyoutItem), new PropertyMetadata(null, OnGlyphChanged));

        private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlyphMenuFlyoutItem)d).OnGlyphChanged((IconElement)e.NewValue);
        }

        private void OnGlyphChanged(IconElement newValue)
        {
            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.MenuFlyoutItem", "Icon"))
            {
                Icon = newValue;
            }
        }
    }
}
