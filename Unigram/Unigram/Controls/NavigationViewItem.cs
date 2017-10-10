using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class NavigationViewItem : NavigationViewItemBase
    {
        public NavigationViewItem()
        {
            DefaultStyleKey = typeof(NavigationViewItem);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            VisualStateManager.GoToState(this, IsChecked ? "Checked" : "Unchecked", false);
        }

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(NavigationViewItem), new PropertyMetadata(null));

        #endregion

        #region IsChecked

        public bool IsChecked
        {
            get { return (bool)GetValue(IsCheckedProperty); }
            set { SetValue(IsCheckedProperty, value); }
        }

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool), typeof(NavigationViewItem), new PropertyMetadata(false, OnIsCheckedChanged));

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            VisualStateManager.GoToState((NavigationViewItem)d, (bool)e.NewValue ? "Checked" : "Unchecked", false);
        }

        #endregion
    }
}
