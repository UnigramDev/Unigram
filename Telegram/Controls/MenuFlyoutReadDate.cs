//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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
