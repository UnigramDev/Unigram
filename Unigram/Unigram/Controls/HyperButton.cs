using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class HyperButton : Button
    {
        public HyperButton()
        {
            DefaultStyleKey = typeof(HyperButton);
        }

        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(HyperButton), new PropertyMetadata(default(CornerRadius)));
    }

    public class HyperCheckButton : HyperButton
    {
        public HyperCheckButton()
        {
            DefaultStyleKey = typeof(HyperCheckButton);
        }

        public bool IsOn
        {
            get { return (bool)GetValue(IsOnProperty); }
            set { SetValue(IsOnProperty, value); }
        }

        public static readonly DependencyProperty IsOnProperty =
            DependencyProperty.Register("IsOn", typeof(bool), typeof(HyperCheckButton), new PropertyMetadata(false));
    }
}
