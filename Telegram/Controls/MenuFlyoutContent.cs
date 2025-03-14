//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class MenuFlyoutContent : MenuFlyoutItem
    {
        public MenuFlyoutContent()
        {
            DefaultStyleKey = typeof(MenuFlyoutContent);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (Content is Control control && FocusState != FocusState.Unfocused)
            {
                control.Focus(FocusState);
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            if (Content is Control control && FocusState != FocusState.Unfocused)
            {
                control.Focus(FocusState);
            }
        }

        #region Content

        public object Content
        {
            get { return (object)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register("Content", typeof(object), typeof(MenuFlyoutContent), new PropertyMetadata(null));

        #endregion

        #region ContentTemplate

        public DataTemplate ContentTemplate
        {
            get { return (DataTemplate)GetValue(ContentTemplateProperty); }
            set { SetValue(ContentTemplateProperty, value); }
        }

        public static readonly DependencyProperty ContentTemplateProperty =
            DependencyProperty.Register("ContentTemplate", typeof(DataTemplate), typeof(MenuFlyoutContent), new PropertyMetadata(null));

        #endregion
    }
}
