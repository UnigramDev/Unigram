using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class NavigationView : ContentControl
    {
        private Button TogglePaneButton;
        private SplitView RootSplitView;

        private Thickness? _previousTopPadding;

        public NavigationView()
        {
            DefaultStyleKey = typeof(NavigationView);
        }

        protected override void OnApplyTemplate()
        {
            TogglePaneButton = GetTemplateChild("TogglePaneButton") as Button;
            RootSplitView = GetTemplateChild("RootSplitView") as SplitView;

            TogglePaneButton.Click += Toggle_Click;

            RootSplitView.PaneOpening += OnPaneOpening;
            RootSplitView.PaneClosing += OnPaneClosing;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            IsPaneOpen = !IsPaneOpen;
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

        public event TypedEventHandler<SplitView, object> PaneOpening;
        public event TypedEventHandler<SplitView, SplitViewPaneClosingEventArgs> PaneClosing;

        #region IsPaneOpen

        public bool IsPaneOpen
        {
            get { return (bool)GetValue(IsPaneOpenProperty); }
            set { SetValue(IsPaneOpenProperty, value); }
        }

        public static readonly DependencyProperty IsPaneOpenProperty =
            DependencyProperty.Register("IsPaneOpen", typeof(bool), typeof(NavigationView), new PropertyMetadata(false));

        #endregion

        #region PaneHeader

        public object PaneHeader
        {
            get { return (object)GetValue(PaneHeaderProperty); }
            set { SetValue(PaneHeaderProperty, value); }
        }

        public static readonly DependencyProperty PaneHeaderProperty =
            DependencyProperty.Register("PaneHeader", typeof(object), typeof(NavigationView), new PropertyMetadata(null));

        #endregion

        #region PaneFooter

        public object PaneFooter
        {
            get { return (object)GetValue(PaneFooterProperty); }
            set { SetValue(PaneFooterProperty, value); }
        }

        public static readonly DependencyProperty PaneFooterProperty =
            DependencyProperty.Register("PaneFooter", typeof(object), typeof(NavigationView), new PropertyMetadata(null));

        #endregion

        #region PaneToggleButtonVisibility

        public Visibility PaneToggleButtonVisibility
        {
            get { return (Visibility)GetValue(PaneToggleButtonVisibilityProperty); }
            set { SetValue(PaneToggleButtonVisibilityProperty, value); }
        }

        public static readonly DependencyProperty PaneToggleButtonVisibilityProperty =
            DependencyProperty.Register("PaneToggleButtonVisibility", typeof(Visibility), typeof(NavigationView), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region TopPadding

        public Thickness TopPadding
        {
            get { return (Thickness)GetValue(TopPaddingProperty); }
            set { SetValue(TopPaddingProperty, value); }
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
