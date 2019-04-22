using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class SimpleButton : Button
    {
        public SimpleButton()
        {
            DefaultStyleKey = typeof(SimpleButton);
        }

        #region Radius

        public CornerRadius Radius
        {
            get { return (CornerRadius)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register("Radius", typeof(CornerRadius), typeof(SimpleButton), new PropertyMetadata(default(CornerRadius)));

        #endregion
    }

    public class SimpleHyperlinkButton : HyperlinkButton
    {
        public SimpleHyperlinkButton()
        {
            DefaultStyleKey = typeof(SimpleHyperlinkButton);
        }

        #region Radius

        public CornerRadius Radius
        {
            get { return (CornerRadius)GetValue(RadiusProperty); }
            set { SetValue(RadiusProperty, value); }
        }

        public static readonly DependencyProperty RadiusProperty =
            DependencyProperty.Register("Radius", typeof(CornerRadius), typeof(SimpleHyperlinkButton), new PropertyMetadata(default(CornerRadius)));

        #endregion
    }
}
