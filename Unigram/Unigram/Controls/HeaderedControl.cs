using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Unigram.Controls
{
    public class HeaderedControl : ItemsControl
    {
        public HeaderedControl()
        {
            DefaultStyleKey = typeof(HeaderedControl);
        }

        #region Header

        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(HeaderedControl), new PropertyMetadata(null));

        #endregion

        #region Footer

        public string Footer
        {
            get { return (string)GetValue(FooterProperty); }
            set { SetValue(FooterProperty, value); }
        }

        public static readonly DependencyProperty FooterProperty =
            DependencyProperty.Register("Footer", typeof(string), typeof(HeaderedControl), new PropertyMetadata(null));

        #endregion
    }
}
