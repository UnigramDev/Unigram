using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Services;
using Telegram.Api.Services.Updates;
using Unigram.Core.Dependency;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class DebugToolbar : UserControl, INotifyPropertyChanged
    {
        private CoreApplicationViewTitleBar coreTitleBar = CoreApplication.GetCurrentView().TitleBar;

        public DebugToolbar()
        {
            this.InitializeComponent();
        }

        void CustomTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            coreTitleBar.LayoutMetricsChanged += OnLayoutMetricsChanged;
            coreTitleBar.IsVisibleChanged += OnIsVisibleChanged;

            // The SizeChanged event is raised when the view enters or exits full screen mode.
            Window.Current.SizeChanged += OnWindowSizeChanged;

            EnableControlsInTitleBar(true);
            UpdateLayoutMetrics();
            UpdatePositionAndVisibility();
        }

        void CustomTitleBar_Unloaded(object sender, RoutedEventArgs e)
        {
            coreTitleBar.LayoutMetricsChanged -= OnLayoutMetricsChanged;
            coreTitleBar.IsVisibleChanged -= OnIsVisibleChanged;
            Window.Current.SizeChanged -= OnWindowSizeChanged;
        }

        void OnLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object e)
        {
            UpdateLayoutMetrics();
        }

        void UpdateLayoutMetrics()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs("CoreTitleBarHeight"));
                PropertyChanged(this, new PropertyChangedEventArgs("CoreTitleBarPadding"));
            }
        }

        void OnIsVisibleChanged(CoreApplicationViewTitleBar sender, object e)
        {
            UpdatePositionAndVisibility();
        }

        void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            UpdatePositionAndVisibility();
        }

        // We wrap the normal contents of the MainPage inside a grid
        // so that we can place a title bar on top of it.
        //
        // When not in full screen mode, the grid looks like this:
        //
        //      Row 0: Custom-rendered title bar
        //      Row 1: Rest of content
        //
        // In full screen mode, the the grid looks like this:
        //
        //      Row 0: (empty)
        //      Row 1: Custom-rendered title bar
        //      Row 1: Rest of content
        //
        // The title bar is either Visible or Collapsed, depending on the value of
        // the IsVisible property.
        void UpdatePositionAndVisibility()
        {
            if (ApplicationView.GetForCurrentView().IsFullScreenMode)
            {
                // In full screen mode, the title bar overlays the content.
                // and might or might not be visible.
                TitleBar.Visibility = coreTitleBar.IsVisible ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;
                Grid.SetRow(TitleBar, 1);
            }
            else
            {
                // When not in full screen mode, the title bar is visible and does not overlay content.
                TitleBar.Visibility = Windows.UI.Xaml.Visibility.Visible;
                Grid.SetRow(TitleBar, 0);
            }
        }

        UIElement pageContent = null;

        public UIElement SetPageContent(UIElement newContent)
        {
            UIElement oldContent = pageContent;
            if (oldContent != null)
            {
                pageContent = null;
                RootGrid.Children.Remove(oldContent);
            }
            pageContent = newContent;
            if (newContent != null)
            {
                RootGrid.Children.Add(newContent);
                // The page content is row 1 in our grid. (See diagram above.)
                Grid.SetRow((FrameworkElement)pageContent, 1);
            }
            return oldContent;
        }

        #region Data binding
        public event PropertyChangedEventHandler PropertyChanged;

        public Thickness CoreTitleBarPadding
        {
            get
            {
                // The SystemOverlayLeftInset and SystemOverlayRightInset values are
                // in terms of physical left and right. Therefore, we need to flip
                // then when our flow direction is RTL.
                if (FlowDirection == FlowDirection.LeftToRight)
                {
                    return new Thickness() { Left = coreTitleBar.SystemOverlayLeftInset, Right = coreTitleBar.SystemOverlayRightInset };
                }
                else
                {
                    return new Thickness() { Left = coreTitleBar.SystemOverlayRightInset, Right = coreTitleBar.SystemOverlayLeftInset };
                }
            }
        }

        public double CoreTitleBarHeight
        {
            get
            {
                return coreTitleBar.Height;
            }
        }
        #endregion

        public void EnableControlsInTitleBar(bool enable)
        {
            if (enable)
            {
                Window.Current.SetTitleBar(BackgroundElement);
            }
            else
            {
                Window.Current.SetTitleBar(null);
            }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            var updatesService = UnigramContainer.Current.ResolveType<IUpdatesService>();
            updatesService.LoadStateAndUpdate(() => { });

            Play.Visibility = Visibility.Collapsed;
            Pause.Visibility = Visibility.Visible;
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            var updatesService = UnigramContainer.Current.ResolveType<IUpdatesService>();
            updatesService.SaveState();
            updatesService.CancelUpdating();

            Play.Visibility = Visibility.Visible;
            Pause.Visibility = Visibility.Collapsed;
        }
    }
}