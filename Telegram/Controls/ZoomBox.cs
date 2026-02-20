//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    [TemplatePart(Name = "ContentPresenter", Type = typeof(UIElement))]
    public class ZoomBox : ContentControl
    {
        private readonly ScaleTransform _transform = new()
        {
            ScaleX = 1.0,
            ScaleY = 1.0
        };

        private UIElement ContentPresenter;

        #region ZoomFactor


        public double ZoomFactor
        {
            get { return (double)GetValue(ZoomFactorProperty); }
            set { SetValue(ZoomFactorProperty, value); }
        }

        public static readonly DependencyProperty ZoomFactorProperty =
            DependencyProperty.Register(nameof(ZoomFactor), typeof(double), typeof(ZoomBox), new PropertyMetadata(1.0, OnZoomFactorChanged));

        private static void OnZoomFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ZoomBox)d).InvalidateMeasure();
        }

        #endregion

        public ZoomBox()
        {
            DefaultStyleKey = typeof(ZoomBox);
        }

        protected override void OnApplyTemplate()
        {
            if (ContentPresenter != null)
            {
                ContentPresenter.RenderTransform = null;
            }

            ContentPresenter = null;
            base.OnApplyTemplate();

            ContentPresenter = GetTemplateChild(nameof(ContentPresenter)) as ContentPresenter;

            if (ContentPresenter != null && ZoomFactor != 1.0)
            {
                ContentPresenter.RenderTransform = _transform;
            }
        }

        protected override Size ArrangeOverride(Size finalSizeInHostCoordinates)
        {
            double zoomFactor = ZoomFactor;
            Size val = finalSizeInHostCoordinates.Scale(zoomFactor);
            Size size = base.ArrangeOverride(val);
            Size result = size.Scale(1.0 / zoomFactor);
            if (zoomFactor != 1.0)
            {
                _transform.ScaleX = _transform.ScaleY = 1.0 / zoomFactor;
                ContentPresenter.RenderTransform = _transform;
            }
            else
            {
                ContentPresenter.RenderTransform = null;
            }
            return result;
        }

        protected override Size MeasureOverride(Size availableSizeInHostCoordinates)
        {
            double zoomFactor = ZoomFactor;
            Size val = availableSizeInHostCoordinates.Scale(ZoomFactor);
            Size size = base.MeasureOverride(val);
            return size.Scale(1.0 / zoomFactor);
        }
    }

    public static class SizeEx
    {
        public static Size Scale(this Size size, double scaleFactor)
        {
            return new Size(
                double.IsInfinity(size.Width) ? size.Width : (size.Width * scaleFactor),
                double.IsInfinity(size.Height) ? size.Height : (size.Height * scaleFactor));
        }
    }
}
