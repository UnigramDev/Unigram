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
    public partial class RichMathImage : ControlEx
    {
        private Image LayoutRoot;

        private RichMathSurface _surface;

        // What the bitmap was rasterized for. Drawing a formula is a pass over the parsed
        // expression and a bitmap the size of it, so an arrange that changed none of this is
        // not a reason to do it again.
        //
        // The scale is read here rather than subscribed to: a rasterization scale change makes
        // the core call RecursiveInvalidateMeasure on the visual root - every element is
        // measured and arranged again - so the layout pass is where it arrives anyway.
        private double _renderedScale;
        private ElementTheme _renderedTheme;
        private bool _rendered;

        public RichMathImage()
        {
            DefaultStyleKey = typeof(RichMathImage);
        }

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Image;
        }

        private void Render()
        {
            if (_surface == null || LayoutRoot == null)
            {
                return;
            }

            var scale = XamlRoot?.RasterizationScale ?? 1;
            var theme = ActualTheme;

            if (_rendered && _renderedScale == scale && _renderedTheme == theme)
            {
                return;
            }

            var width = (int)(_surface.PixelWidth * scale);
            var height = (int)(_surface.PixelHeight * scale);

            if (width <= 0 || height <= 0)
            {
                return;
            }

            _rendered = true;
            _renderedScale = scale;
            _renderedTheme = theme;

            var bitmap = new WriteableBitmap(width, height);

            _surface.RenderSync(bitmap.PixelBuffer, scale, theme == ElementTheme.Light ? Colors.Black : Colors.White);

            bitmap.Invalidate();
            LayoutRoot.Source = bitmap;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_surface == null || LayoutRoot == null)
            {
                return base.MeasureOverride(availableSize);
            }

            availableSize = new Size(_surface.PixelWidth, _surface.PixelHeight);

            LayoutRoot.Measure(availableSize);
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (LayoutRoot == null)
            {
                return base.ArrangeOverride(finalSize);
            }

            // Here rather than when the source is set or the element loads: this is the point
            // at which the formula is about to be seen, and a recycled element that comes back
            // unchanged draws nothing again.
            Render();

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
            }
            catch
            {
                // An expression this cannot parse: IsValid says so, and the caller renders what
                // was written as text instead.
                _surface = null;
            }

            _rendered = false;
            InvalidateMeasure();
        }

        #endregion
    }
}
