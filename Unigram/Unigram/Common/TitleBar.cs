using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Common
{
    public static class TitleBar
    {
        public static bool GetIsAttached(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsAttachedProperty);
        }

        public static void SetIsAttached(DependencyObject obj, bool value)
        {
            obj.SetValue(IsAttachedProperty, value);
        }

        public static readonly DependencyProperty IsAttachedProperty =
            DependencyProperty.RegisterAttached("IsAttached", typeof(bool), typeof(RowDefinition), new PropertyMetadata(false, OnAttachedChanged));

        private static void OnAttachedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var row = d as RowDefinition;
            var newValue = (bool)e.NewValue;

            if (row == null || !newValue)
            {
                return;
            }

            //var row = panel.RowDefinitions.FirstOrDefault();
            //if (row == null)
            //{
            //    return;
            //}

            var sender = CoreApplication.GetCurrentView().TitleBar;

            if (string.Equals(AnalyticsInfo.VersionInfo.DeviceFamily, "Windows.Desktop") && UIViewSettings.GetForCurrentView().UserInteractionMode == UserInteractionMode.Mouse)
            {
                // If running on PC and tablet mode is disabled, then titlebar is most likely visible
                // So we're going to force it
                row.Height = new GridLength(32, GridUnitType.Pixel);
            }
            else
            {
                row.Height = new GridLength(sender.IsVisible ? sender.Height : 0, GridUnitType.Pixel);
            }

            TypedEventHandler<CoreApplicationViewTitleBar, object> handler = null;
            handler = (s, args) =>
            {
                row.Height = new GridLength(sender.IsVisible ? sender.Height : 0, GridUnitType.Pixel);
            };

            sender.ExtendViewIntoTitleBar = true;
            sender.IsVisibleChanged += handler;
            sender.LayoutMetricsChanged += handler;

            //row.Unloaded += (s, args) =>
            //{
            //    sender.IsVisibleChanged -= handler;
            //    sender.LayoutMetricsChanged -= handler;
            //};
        }
    }
}
