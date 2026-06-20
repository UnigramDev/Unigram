//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Native;
using Telegram.Native.Controls;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public class RichMathImage : AnimatedImageBase
    {
        private Image LayoutRoot;

        private RichMathSurface _surface;

        public RichMathImage()
        {
            DefaultStyleKey = typeof(RichMathImage);
        }

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Image;
        }

        protected override void OnLoaded()
        {
            OnRasterizationScaleChanged(XamlRoot.RasterizationScale);
        }

        protected override void OnRasterizationScaleChanged(double rasterizationScale)
        {
            if (_surface == null || LayoutRoot == null)
            {
                return;
            }

            var width = (int)(_surface.PixelWidth * rasterizationScale);
            var height = (int)(_surface.PixelHeight * rasterizationScale);

            var bitmap = new WriteableBitmap(width, height);

            _surface.RenderSync(bitmap.PixelBuffer, XamlRoot.RasterizationScale, ActualTheme == ElementTheme.Light ? Colors.Black : Colors.White);

            bitmap.Invalidate();
            LayoutRoot.Source = bitmap;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_surface == null)
            {
                return base.MeasureOverride(availableSize);
            }

            availableSize = new Size(_surface.PixelWidth, _surface.PixelHeight);

            LayoutRoot.Measure(availableSize);
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            LayoutRoot.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

        public bool IsValid => _surface != null;

        public int PixelWidth => _surface?.PixelWidth ?? 0;

        public int PixelHeight => _surface?.PixelHeight ?? 0;

        public float Baseline => _surface?.Baseline ?? 0;

        #region Source

        public string Source
        {
            get { return (string)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(string), typeof(RichMathImage), new PropertyMetadata(string.Empty, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((RichMathImage)d).OnSourceChanged((string)e.NewValue);
        }

        private void OnSourceChanged(string newValue)
        {
            try
            {
                _surface = new RichMathSurface(newValue);
                InvalidateMeasure();

                if (IsConnected)
                {
                    OnLoaded();
                }
            }
            catch
            {
                // TODO
                _surface = null;
            }
        }

        #endregion
    }
}
