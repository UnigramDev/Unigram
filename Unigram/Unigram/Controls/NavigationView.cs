using Unigram.Common;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Unigram.Controls
{
    public enum PaneToggleButtonVisibility
    {
        Visible,
        Collapsed,
        Back
    }

    public class NavigationView : ContentControl
    {
        private Grid TogglePaneButtonRoot;
        private ToggleButton TogglePaneButton;
        private SplitView RootSplitView;

        private Thickness? _previousTopPadding;

        public NavigationView()
        {
            DefaultStyleKey = typeof(NavigationView);
        }

        public void ShowTeachingTip(string text)
        {
            Window.Current.ShowTeachingTip(TogglePaneButton, text, Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.BottomLeft);
        }

        protected override void OnApplyTemplate()
        {
            TogglePaneButtonRoot = GetTemplateChild("TogglePaneButtonRoot") as Grid;
            TogglePaneButton = GetTemplateChild("TogglePaneButton") as ToggleButton;
            RootSplitView = GetTemplateChild("RootSplitView") as SplitView;

            TogglePaneButton.Click += Toggle_Click;

            RootSplitView.PaneOpening += OnPaneOpening;
            RootSplitView.PaneClosing += OnPaneClosing;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (TogglePaneButton.IsChecked == true)
            {
                BackRequested?.Invoke(this, null);
            }
            else
            {
                IsPaneOpen = !IsPaneOpen;
            }
        }

        private void OnPaneOpening(SplitView sender, object args)
        {
            PaneOpening?.Invoke(sender, args);

            _previousTopPadding = TopPadding;
            TopPadding = new Thickness();

            TogglePaneButton.RequestedTheme = ElementTheme.Dark;
        }

        private void OnPaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args)
        {
            PaneClosing?.Invoke(sender, args);

            if (_previousTopPadding != null)
            {
                TopPadding = _previousTopPadding.Value;
                _previousTopPadding = null;
            }

            TogglePaneButton.RequestedTheme = ElementTheme.Default;
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

        public PaneToggleButtonVisibility PaneToggleButtonVisibility
        {
            get => (PaneToggleButtonVisibility)GetValue(PaneToggleButtonVisibilityProperty);
            set => SetValue(PaneToggleButtonVisibilityProperty, value);
        }

        public static readonly DependencyProperty PaneToggleButtonVisibilityProperty =
            DependencyProperty.Register("PaneToggleButtonVisibility", typeof(PaneToggleButtonVisibility), typeof(NavigationView), new PropertyMetadata(PaneToggleButtonVisibility.Visible, OnPaneToggleButtonVisibilityChanged));

        private static void OnPaneToggleButtonVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((NavigationView)d).OnPaneToggleButtonVisibilityChanged((PaneToggleButtonVisibility)e.NewValue, (PaneToggleButtonVisibility)e.OldValue);
        }

        private void OnPaneToggleButtonVisibilityChanged(PaneToggleButtonVisibility newValue, PaneToggleButtonVisibility oldValue)
        {
            if (TogglePaneButtonRoot == null)
            {
                return;
            }

            if (newValue == PaneToggleButtonVisibility.Collapsed)
            {
                TogglePaneButtonRoot.Visibility = Visibility.Collapsed;
            }
            else
            {
                TogglePaneButtonRoot.Visibility = Visibility.Visible;
                TogglePaneButton.IsChecked = newValue == PaneToggleButtonVisibility.Back;

                Automation.SetToolTip(TogglePaneButton, newValue == PaneToggleButtonVisibility.Back ? Strings.Resources.AccDescrGoBack : Strings.Resources.AccDescrOpenMenu);
            }
        }

        #endregion

        #region TopPadding

        public Thickness TopPadding
        {
            get => (Thickness)GetValue(TopPaddingProperty);
            set => SetValue(TopPaddingProperty, value);
        }

        public static readonly DependencyProperty TopPaddingProperty =
            DependencyProperty.Register("TopPadding", typeof(Thickness), typeof(NavigationView), new PropertyMetadata(null));

        public void SetTopPadding(Thickness thickness)
        {
            _previousTopPadding = thickness;
            TopPadding = thickness;
        }

        #endregion
    }
}
