using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class MenuFlyoutLabel : MenuFlyoutSeparator
    {
        public MenuFlyoutLabel()
        {
            DefaultStyleKey = typeof(MenuFlyoutLabel);
        }

        #region Text

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(MenuFlyoutLabel), new PropertyMetadata(null));

        #endregion

    }
}
