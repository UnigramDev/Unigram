//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if WINUI3
using Microsoft.UI.Windowing;
using Telegram.Navigation;
#else
using Windows.ApplicationModel.Core;
#endif
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    public partial class SystemOverlayMetrics
    {
#if WINUI3
        /// <summary>
        /// The same four numbers off the AppWindow's title bar. Height and the two insets are
        /// named the same there; IsVisible is not, because AppWindowTitleBar has no such flag -
        /// what the UWP one meant by it is "the system reserves a caption area at all", which is
        /// false in full screen and is the presenter's business here.
        /// </summary>
        public SystemOverlayMetrics(AppWindow sender)
        {
            var titleBar = sender.TitleBar;

            Height = titleBar.Height;
            LeftInset = titleBar.LeftInset;
            RightInset = titleBar.RightInset;
            IsVisible = sender.Presenter?.Kind != AppWindowPresenterKind.FullScreen;
        }
#else
        public SystemOverlayMetrics(CoreApplicationViewTitleBar sender)
        {
            Height = sender.Height;
            LeftInset = sender.SystemOverlayLeftInset;
            RightInset = sender.SystemOverlayRightInset;
            IsVisible = sender.IsVisible;
        }
#endif

        public double Height { get; }

        public double LeftInset { get; }

        public double RightInset { get; }

        public bool IsVisible { get; }
    }

    public partial class CorePage : PageEx
    {
        private bool _registered;

        public CorePage()
        {
            Connected += OnConnected;
            Disconnected += OnDisconnected;
        }

#if WINUI3
        // Held rather than re-fetched on teardown: the page can be disconnected after its window
        // has gone, and asking for the AppWindow then would answer null and leak the handler.
        private AppWindow _appWindow;

        private void OnConnected(object sender, RoutedEventArgs e)
        {
            if (_registered)
            {
                return;
            }

            // The window this page is actually in, off its own XamlRoot. Never WindowContext.Current:
            // it is thread-static, and this host puts every window on one thread, so it would answer
            // whichever window happened to be created last.
            _appWindow = WindowContext.GetAppWindow(this);
            if (_appWindow == null)
            {
                return;
            }

            _registered = true;
            _appWindow.Changed += OnAppWindowChanged;

            OnLayoutMetricsChanged(new SystemOverlayMetrics(_appWindow));
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            if (_registered)
            {
                _registered = false;

                _appWindow.Changed -= OnAppWindowChanged;
                _appWindow = null;
            }
        }

        /// <summary>
        /// One event where UWP had two: AppWindow.Changed covers the presenter, size and position
        /// changes that move the caption insets - going full screen, or crossing to a monitor at a
        /// different scale - which is what IsVisibleChanged and LayoutMetricsChanged were between
        /// them reporting.
        /// </summary>
        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidPresenterChange || args.DidSizeChange || args.DidPositionChange)
            {
                try
                {
                    OnLayoutMetricsChanged(new SystemOverlayMetrics(sender));
                }
                catch
                {
                    // Most likely InvalidComObjectException
                }
            }
        }
#else
        private void OnConnected(object sender, RoutedEventArgs e)
        {
            if (!_registered)
            {
                _registered = true;

                var application = CoreApplication.GetCurrentView().TitleBar;
                application.IsVisibleChanged += OnLayoutMetricsChanged;
                application.LayoutMetricsChanged += OnLayoutMetricsChanged;

                OnLayoutMetricsChanged(application, null);
            }
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            if (_registered)
            {
                _registered = false;

                var application = CoreApplication.GetCurrentView().TitleBar;
                application.IsVisibleChanged -= OnLayoutMetricsChanged;
                application.LayoutMetricsChanged -= OnLayoutMetricsChanged;
            }
        }

        private void OnLayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            try
            {
                OnLayoutMetricsChanged(new SystemOverlayMetrics(sender));
            }
            catch
            {
                // Most likely InvalidComObjectException
            }
        }
#endif

        protected virtual void OnLayoutMetricsChanged(SystemOverlayMetrics metrics)
        {

        }
    }
}
