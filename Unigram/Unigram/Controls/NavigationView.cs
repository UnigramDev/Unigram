using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class NavigationView : ContentControl
    {
        private Button TogglePaneButton;
        private SplitView RootSplitView;

        public NavigationView()
        {
            DefaultStyleKey = typeof(NavigationView);
        }

        protected override void OnApplyTemplate()
        {
            TogglePaneButton = GetTemplateChild("TogglePaneButton") as Button;
            RootSplitView = GetTemplateChild("RootSplitView") as SplitView;

            TogglePaneButton.Click += Toggle_Click;

            if (ApiInformation.IsEventPresent("Windows.UI.Xaml.Controls.SplitView", "PaneOpening"))
            {
                RootSplitView.PaneOpening += OnPaneOpening;
                RootSplitView.PaneClosing += OnPaneClosing;
            }
            else
            {
                RootSplitView.RegisterPropertyChangedCallback(SplitView.IsPaneOpenProperty, OnPaneOpenChanged);
            }
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            IsPaneOpen = !IsPaneOpen;
        }

        private void OnPaneOpening(SplitView sender, object args)
        {
            TogglePaneButton.RequestedTheme = ElementTheme.Dark;
        }

        private void OnPaneClosing(SplitView sender, SplitViewPaneClosingEventArgs args)
        {
            TogglePaneButton.RequestedTheme = ElementTheme.Default;
        }

        private void OnPaneOpenChanged(DependencyObject sender, DependencyProperty dp)
        {
            TogglePaneButton.RequestedTheme = RootSplitView.IsPaneOpen ? ElementTheme.Dark : ElementTheme.Default;
        }

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
    }
}
