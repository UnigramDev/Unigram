using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class MenuFlyoutProfile : MenuFlyoutItem
    {
        #region Info

        public string Info
        {
            get { return (string)GetValue(InfoProperty); }
            set { SetValue(InfoProperty, value); }
        }

        public static readonly DependencyProperty InfoProperty =
            DependencyProperty.Register("Info", typeof(string), typeof(MenuFlyoutProfile), new PropertyMetadata(null));

        #endregion
    }
}
