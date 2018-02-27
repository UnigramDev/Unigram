using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class TitleBarPlaceholder : Grid
    {
        public TitleBarPlaceholder()
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;
            UpdateTitleBarLayout(coreTitleBar);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            UpdateTitleBarLayout(coreTitleBar);

            coreTitleBar.IsVisibleChanged += TitleBar_IsVisibleChanged;
            coreTitleBar.LayoutMetricsChanged += TitleBar_LayoutMetricsChanged;
        }

        private void OnUnloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            UpdateTitleBarLayout(coreTitleBar);

            coreTitleBar.IsVisibleChanged -= TitleBar_IsVisibleChanged;
            coreTitleBar.LayoutMetricsChanged -= TitleBar_LayoutMetricsChanged;
        }

        private void TitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            Visibility = sender.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            UpdateTitleBarLayout(sender);
        }

        private void UpdateTitleBarLayout(CoreApplicationViewTitleBar coreTitleBar)
        {
            Padding = new Thickness(coreTitleBar.SystemOverlayLeftInset, 0, coreTitleBar.SystemOverlayRightInset, 0);
            Height = coreTitleBar.Height;
        }
    }
}
