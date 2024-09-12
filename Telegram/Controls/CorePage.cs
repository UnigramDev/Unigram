using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    public partial class SystemOverlayMetrics
    {
        public SystemOverlayMetrics(CoreApplicationViewTitleBar sender)
        {
            Height = sender.Height;
            LeftInset = sender.SystemOverlayLeftInset;
            RightInset = sender.SystemOverlayRightInset;
            IsVisible = sender.IsVisible;
        }

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

        protected virtual void OnLayoutMetricsChanged(SystemOverlayMetrics metrics)
        {

        }
    }
}
