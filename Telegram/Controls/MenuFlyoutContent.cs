//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using LinqToVisualTree;
using System.Linq;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public partial class MenuFlyoutContent : MenuFlyoutItem
    {
        public MenuFlyoutContent()
        {
            DefaultStyleKey = typeof(MenuFlyoutContent);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MenuFlyoutContentAutomationPeer(this);
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

    public partial class MenuFlyoutContentAutomationPeer : MenuFlyoutItemAutomationPeer
    {
        private readonly MenuFlyoutContent _owner;

        public MenuFlyoutContentAutomationPeer(MenuFlyoutContent owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            var builder = new StringBuilder();
            var descendants = _owner.Descendants();

            foreach (UIElement child in descendants.Where(x => x is TextBlock or RichTextBlock))
            {
                var view = AutomationProperties.GetAccessibilityView(child);
                if (view == AccessibilityView.Raw)
                {
                    continue;
                }

                var peer = FrameworkElementAutomationPeer.FromElement(child);
                if (peer == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(peer.GetName());
            }

            return builder.ToString();
        }
    }
}
