//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;

namespace Telegram.Controls
{
    public partial class NavigationViewItem : NavigationViewItemBase
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

        #region Text

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(NavigationViewItem), new PropertyMetadata(null));

        #endregion

        #region Glyph

        public string Glyph
        {
            get => (string)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(NavigationViewItem), new PropertyMetadata(null));

        #endregion

        #region IsChecked

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register("IsChecked", typeof(bool), typeof(NavigationViewItem), new PropertyMetadata(false, OnIsCheckedChanged));

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            VisualStateManager.GoToState((NavigationViewItem)d, (bool)e.NewValue ? "Checked" : "Unchecked", false);
        }

        #endregion

        #region BadgeVisibility

        public Visibility BadgeVisibility
        {
            get { return (Visibility)GetValue(BadgeVisibilityProperty); }
            set { SetValue(BadgeVisibilityProperty, value); }
        }

        public static readonly DependencyProperty BadgeVisibilityProperty =
            DependencyProperty.Register("BadgeVisibility", typeof(Visibility), typeof(NavigationViewItem), new PropertyMetadata(Visibility.Collapsed));

        #endregion
    }
}
