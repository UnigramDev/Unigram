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

        #region CornerRadius

        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(SimpleButton), new PropertyMetadata(default(CornerRadius)));

        #endregion
    }

    public class SimpleHyperlinkButton : HyperlinkButton
    {
        public SimpleHyperlinkButton()
        {
            DefaultStyleKey = typeof(SimpleHyperlinkButton);
        }

        #region CornerRadius

        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(SimpleHyperlinkButton), new PropertyMetadata(default(CornerRadius)));

        #endregion
    }
}
