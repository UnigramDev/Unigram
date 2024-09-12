using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class MenuFlyoutReadDateItem : MenuFlyoutItem
    {
        public MenuFlyoutReadDateItem()
        {
            DefaultStyleKey = typeof(MenuFlyoutReadDateItem);
        }

        #region ShowWhenVisibility

        public Visibility ShowWhenVisibility
        {
            get { return (Visibility)GetValue(ShowWhenVisibilityProperty); }
            set { SetValue(ShowWhenVisibilityProperty, value); }
        }

        public static readonly DependencyProperty ShowWhenVisibilityProperty =
            DependencyProperty.Register("ShowWhenVisibility", typeof(Visibility), typeof(MenuFlyoutReadDateItem), new PropertyMetadata(Visibility.Collapsed));

        #endregion
    }
}
