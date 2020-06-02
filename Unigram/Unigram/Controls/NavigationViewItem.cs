using Windows.UI.Xaml;

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

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(NavigationViewItem), new PropertyMetadata(null));

        #endregion

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
