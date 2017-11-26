using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class ZoomableGridViewPopup : Grid
    {
        private ApplicationView _applicationView;

        public ZoomableGridViewPopup()
        {
            InitializeComponent();

            _applicationView = ApplicationView.GetForCurrentView();
            _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
            OnVisibleBoundsChanged(_applicationView, null);
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (sender == null)
            {
                return;
            }

            if (/*BackgroundElement != null &&*/ Window.Current?.Bounds is Rect bounds && sender.VisibleBounds != bounds)
            {
                Margin = new Thickness(sender.VisibleBounds.X - bounds.Left, sender.VisibleBounds.Y - bounds.Top, bounds.Width - (sender.VisibleBounds.Right - bounds.Left), bounds.Height - (sender.VisibleBounds.Bottom - bounds.Top));
            }
            else
            {
                Margin = new Thickness();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Padding = new Thickness(0, 0, 0, InputPane.GetForCurrentView().OccludedRect.Height);

            InputPane.GetForCurrentView().Showing += InputPane_Showing;
            InputPane.GetForCurrentView().Hiding += InputPane_Hiding;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            InputPane.GetForCurrentView().Showing -= InputPane_Showing;
            InputPane.GetForCurrentView().Hiding -= InputPane_Hiding;
        }

        private void InputPane_Showing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            Padding = new Thickness(0, 0, 0, args.OccludedRect.Height);
        }

        private void InputPane_Hiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            Padding = new Thickness();
        }
    }
}
