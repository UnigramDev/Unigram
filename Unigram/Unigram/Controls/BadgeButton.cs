using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class BadgeButton : GlyphButton
    {
        public BadgeButton()
        {
            DefaultStyleKey = typeof(BadgeButton);
        }

        #region Badge

        public object Badge
        {
            get { return (object)GetValue(BadgeProperty); }
            set { SetValue(BadgeProperty, value); }
        }

        public static readonly DependencyProperty BadgeProperty =
            DependencyProperty.Register("Badge", typeof(object), typeof(BadgeButton), new PropertyMetadata(null));

        #endregion

        #region BadgeVisibility

        public Visibility BadgeVisibility
        {
            get { return (Visibility)GetValue(BadgeVisibilityProperty); }
            set { SetValue(BadgeVisibilityProperty, value); }
        }

        public static readonly DependencyProperty BadgeVisibilityProperty =
            DependencyProperty.Register("BadgeVisibility", typeof(Visibility), typeof(BadgeButton), new PropertyMetadata(Visibility.Visible));

        #endregion
    }
}
