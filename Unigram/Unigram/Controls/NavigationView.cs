//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class NavigationView : ContentControl
    {
        private GlyphButton TogglePaneButton;
        private SplitView RootSplitView;

        public NavigationView()
        {
            DefaultStyleKey = typeof(NavigationView);
        }

        protected override void OnApplyTemplate()
        {
            TogglePaneButton = GetTemplateChild("TogglePaneButton") as GlyphButton;
            RootSplitView = GetTemplateChild("RootSplitView") as SplitView;

            TogglePaneButton.Click += Toggle_Click;

            RootSplitView.PaneOpening += OnPaneOpening;
            RootSplitView.PaneClosing += OnPaneClosing;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, null);
        }

        private void OnPaneOpening(SplitView sender, object args)
        {
            PaneOpening?.Invoke(sender, args);
        }

        private void OnPaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args)
        {
            PaneClosing?.Invoke(sender, args);
        }

        public event TypedEventHandler<NavigationView, object> BackRequested;

        public event TypedEventHandler<SplitView, object> PaneOpening;
        public event TypedEventHandler<SplitView, SplitViewPaneClosingEventArgs> PaneClosing;

        #region IsPaneOpen

        public bool IsPaneOpen
        {
            get => (bool)GetValue(IsPaneOpenProperty);
            set => SetValue(IsPaneOpenProperty, value);
        }

        public static readonly DependencyProperty IsPaneOpenProperty =
            DependencyProperty.Register("IsPaneOpen", typeof(bool), typeof(NavigationView), new PropertyMetadata(false));

        #endregion

        #region PaneHeader

        public object PaneHeader
        {
            get => GetValue(PaneHeaderProperty);
            set => SetValue(PaneHeaderProperty, value);
        }

        public static readonly DependencyProperty PaneHeaderProperty =
            DependencyProperty.Register("PaneHeader", typeof(object), typeof(NavigationView), new PropertyMetadata(null));

        #endregion

        #region PaneFooter

        public object PaneFooter
        {
            get => GetValue(PaneFooterProperty);
            set => SetValue(PaneFooterProperty, value);
        }

        public static readonly DependencyProperty PaneFooterProperty =
            DependencyProperty.Register("PaneFooter", typeof(object), typeof(NavigationView), new PropertyMetadata(null));

        #endregion

        #region PaneToggleButtonVisibility

        public Visibility PaneToggleButtonVisibility
        {
            get => (Visibility)GetValue(PaneToggleButtonVisibilityProperty);
            set => SetValue(PaneToggleButtonVisibilityProperty, value);
        }

        public static readonly DependencyProperty PaneToggleButtonVisibilityProperty =
            DependencyProperty.Register("PaneToggleButtonVisibility", typeof(Visibility), typeof(NavigationView), new PropertyMetadata(Visibility.Collapsed));

        #endregion
    }
}
