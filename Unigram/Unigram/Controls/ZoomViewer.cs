using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Point = Windows.Foundation.Point;

namespace Unigram.Controls
{
    public class ZoomViewer : ContentControl
    {
        private ScrollViewer ScrollingHost;

        private bool _pointerPressed;
        private Point _pointerPosition;

        public ZoomViewer()
        {
            DefaultStyleKey = typeof(ZoomViewer);
        }

        public event EventHandler<ScrollViewerViewChangedEventArgs> ViewChanged;

        protected override void OnApplyTemplate()
        {
            ScrollingHost = GetTemplateChild(nameof(ScrollingHost)) as ScrollViewer;
            ScrollingHost.ViewChanged += ScrollingHost_ViewChanged;
            ScrollingHost.PointerPressed += ScrollingHost_PointerPressed;
            ScrollingHost.PointerMoved += ScrollingHost_PointerMoved;
            ScrollingHost.PointerReleased += ScrollingHost_PointerReleased;
        }

        public bool CanZoomIn => ScrollingHost?.ZoomFactor < MaxZoomFactor;
        public bool CanZoomOut => ScrollingHost?.ZoomFactor > MinZoomFactor;

        public double ZoomFactor => ScrollingHost?.ZoomFactor ?? 1;

        #region MaxZoomFactor

        public double MaxZoomFactor
        {
            get { return (double)GetValue(MaxZoomFactorProperty); }
            set { SetValue(MaxZoomFactorProperty, value); }
        }

        public static readonly DependencyProperty MaxZoomFactorProperty =
            DependencyProperty.Register("MaxZoomFactor", typeof(double), typeof(ZoomViewer), new PropertyMetadata(1d));

        #endregion

        #region MinZoomFactor

        public double MinZoomFactor
        {
            get { return (double)GetValue(MinZoomFactorProperty); }
            set { SetValue(MinZoomFactorProperty, value); }
        }

        public static readonly DependencyProperty MinZoomFactorProperty =
            DependencyProperty.Register("MinZoomFactor", typeof(double), typeof(ZoomViewer), new PropertyMetadata(1d));

        #endregion

        public void Zoom(bool zoomIn, bool disableAnimation = false)
        {
            Zoom(ScrollingHost.ZoomFactor + (zoomIn ? 0.25f : -0.25f), disableAnimation);
        }

        public void Zoom(float factor, bool disableAnimation = false)
        {
            if (ScrollingHost == null)
            {
                return;
            }

            if (factor <= ScrollingHost.MaxZoomFactor)
            {
                var horizontal = ScrollingHost.ViewportWidth * factor - ScrollingHost.ActualWidth;
                var vertical = ScrollingHost.ViewportHeight * factor - ScrollingHost.ActualHeight;

                if (ScrollingHost.ScrollableWidth > 0)
                {
                    horizontal *= ScrollingHost.HorizontalOffset / ScrollingHost.ScrollableWidth;
                }
                else
                {
                    horizontal /= 2;
                }

                if (ScrollingHost.ScrollableHeight > 0)
                {
                    vertical *= ScrollingHost.VerticalOffset / ScrollingHost.ScrollableHeight;
                }
                else
                {
                    vertical /= 2;
                }

                ScrollingHost.ChangeView(horizontal, vertical, factor, disableAnimation);
            }
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            ViewChanged?.Invoke(this, e);
        }

        private void ScrollingHost_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ScrollingHost.CapturePointer(e.Pointer);

            _pointerPressed = true;
            _pointerPosition = e.GetCurrentPoint(ScrollingHost).Position;

            _pointerPosition.X += ScrollingHost.HorizontalOffset;
            _pointerPosition.Y += ScrollingHost.VerticalOffset;
        }

        private void ScrollingHost_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_pointerPressed)
            {
                var point = e.GetCurrentPoint(ScrollingHost);

                var diffX = _pointerPosition.X - point.Position.X;
                var diffY = _pointerPosition.Y - point.Position.Y;

                ScrollingHost.ChangeView(diffX, diffY, null, true);
            }
        }

        private void ScrollingHost_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (ScrollingHost.PointerCaptures?.Count > 0)
            {
                ScrollingHost.ReleasePointerCapture(e.Pointer);
            }

            _pointerPressed = false;
            _pointerPosition = default;
        }
    }
}
