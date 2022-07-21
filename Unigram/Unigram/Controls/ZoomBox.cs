using Unigram.Common;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [TemplatePart(Name = ZoomBox.ContentHolderPartName, Type = typeof(UIElement))]
    public class ZoomBox : ContentControl
    {
        public const string ContentHolderPartName = "contentHolder";

        private readonly ScaleTransform _transform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        private UIElement _presenter;

        private double _currentDpi;
        private double _currentScale;

        public ScaleTransform Transform => _transform;

        public ZoomBox()
        {
            DefaultStyleKey = typeof(ZoomBox);

            var info = DisplayInformation.GetForCurrentView();
            info.DpiChanged += OnDpiChanged;

            _currentDpi = (double)info.ResolutionScale / 100d;
            _currentScale = _currentDpi / (RequestedScale ?? _currentDpi);
        }

        private void OnDpiChanged(DisplayInformation sender, object args)
        {
            InvalidateScale();
        }

        private void InvalidateScale()
        {
            var newDpi = (double)DisplayInformation.GetForCurrentView().ResolutionScale / 100d;
            var newScale = newDpi / (RequestedScale ?? newDpi);

            var needsCreate = newDpi != _currentDpi;
            needsCreate |= newScale != _currentScale;

            _currentDpi = newDpi;
            _currentScale = newScale;

            if (needsCreate)
            {
                InvalidateMeasure();
            }
        }

        protected override void OnApplyTemplate()
        {
            if (_presenter != null)
                _presenter.RenderTransform = null;
            _presenter = null;

            base.OnApplyTemplate();

            var temp = GetTemplateChild(ContentHolderPartName) as UIElement;
            if (temp == null)
                return;

            _presenter = temp;

            if (ActualScale != 1)
                _presenter.RenderTransform = _transform;
        }

        #region RequestedScale

        public double? RequestedScale
        {
            get { return (double?)GetValue(RequestedScaleProperty); }
            set { SetValue(RequestedScaleProperty, value); }
        }

        public static readonly DependencyProperty RequestedScaleProperty =
            DependencyProperty.Register("RequestedScale", typeof(double?), typeof(ZoomBox), new PropertyMetadata(null, OnRequestedScaleChanged));

        private static void OnRequestedScaleChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var control = (ZoomBox)source;
            control.InvalidateScale();
        }

        #endregion

        public double ActualScale => _currentScale;

        protected override Size ArrangeOverride(Size finalSizeInHostCoordinates)
        {
            var effectiveZoomFactor = ActualScale;
            var finalSizeInViewCoordinates = finalSizeInHostCoordinates.Scale(effectiveZoomFactor);
            var requiredSizeInViewCoordinates = base.ArrangeOverride(finalSizeInViewCoordinates);
            var requiredSizeInHostCoordinates = requiredSizeInViewCoordinates.Scale(1 / effectiveZoomFactor);

            if (effectiveZoomFactor != 1)
            {
                _transform.ScaleX = _transform.ScaleY = 1 / effectiveZoomFactor;
                _presenter.RenderTransform = _transform;
            }
            else
            {
                _presenter.RenderTransform = null;
            }

#if LOG_MEASURE_ARRANGE_OVERRIDE
      Debug.WriteLine("*** ArrangeOverride ***");
      Debug.WriteLine("  input size (host coordinates):    {0:#.} x {1:#.}", finalSizeInHostCoordinates.Width, finalSizeInHostCoordinates.Height);
      Debug.WriteLine("  converted (view coordinates):     {0:#.} x {1:#.}", finalSizeInViewCoordinates.Width, finalSizeInViewCoordinates.Height);
      Debug.WriteLine("  required size (view coordinates): {0:#} x {1:#.}", requiredSizeInViewCoordinates.Width, requiredSizeInViewCoordinates.Height);
      Debug.WriteLine("  scaling factor:                   {0:#.##}", ZoomFactor);
      Debug.WriteLine("  returning (host coordinates):     {0:#.} x {1:#.}", requiredSizeInHostCoordinates.Width, requiredSizeInHostCoordinates.Height);
#endif

            return requiredSizeInHostCoordinates;
        }

        protected override Size MeasureOverride(Size availableSizeInHostCoordinates)
        {
            var effectiveZoomFactor = ActualScale;
            var availableSizeInViewCoordinates = availableSizeInHostCoordinates.Scale(effectiveZoomFactor);
            var desiredSizeInViewCoordinates = base.MeasureOverride(availableSizeInViewCoordinates);
            var desiredSizeInHostCoordinates = desiredSizeInViewCoordinates.Scale(1 / effectiveZoomFactor);

#if LOG_MEASURE_ARRANGE_OVERRIDE
      Debug.WriteLine("*** MeasureOverride ***");
      Debug.WriteLine("  input size (host coordinates):    {0:#.} x {1:#.}", availableSizeInHostCoordinates.Width, availableSizeInHostCoordinates.Height);
      Debug.WriteLine("  converted (view coordinates):     {0:#.} x {1:#.}", availableSizeInViewCoordinates.Width, availableSizeInViewCoordinates.Height);
      Debug.WriteLine("  required size (view coordinates): {0:#} x {1:#.}", desiredSizeInViewCoordinates.Width, desiredSizeInViewCoordinates.Height);
      Debug.WriteLine("  scaling factor:                   {0:#.##}", ZoomFactor);
      Debug.WriteLine("  returning (host coordinates):     {0:#.} x {1:#.}", desiredSizeInHostCoordinates.Width, desiredSizeInHostCoordinates.Height);
#endif

            return desiredSizeInHostCoordinates;
        }
    }
}
