using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class GlyphMenuFlyoutItem : MenuFlyoutItem
    {
        public GlyphMenuFlyoutItem()
        {
            //DefaultStyleKey = typeof(GlyphMenuFlyoutItem);
        }

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(GlyphMenuFlyoutItem), new PropertyMetadata(null, OnGlyphChanged));

        private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlyphMenuFlyoutItem)d).OnGlyphChanged((string)e.NewValue);
        }

        private void OnGlyphChanged(string newValue)
        {
            if (ApiInformation.IsPropertyPresent("Windows.UI.Xaml.Controls.MenuFlyoutItem", "Icon"))
            {
                Icon = new FontIcon { Glyph = newValue };
            }
        }
    }
}
