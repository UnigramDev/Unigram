//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Native;
using Telegram.Native.Controls;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
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
        private Color _renderedColor;
        private bool _rendered;

        private long _foregroundChanged;

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
            // The colour the formula is drawn in is the colour of the text around it, and this
            // is every way it moves: the property is set to another brush - which is what the
            // block hosting this does - or a theme switch re-evaluates the theme resource it
            // holds, which is a property change as well. A colour swapped in place on the
            // brush itself is not watched, the same call DirectTextBlock makes for its text.
            this.RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged, ref _foregroundChanged);
        }

        protected override void OnUnloaded()
        {
            this.UnregisterPropertyChangedCallback(ForegroundProperty, ref _foregroundChanged);
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            InvalidateTexture();
        }

        // The bitmap is out of date. Drawn again where it is drawn - the arrange pass - rather
        // than here, so a colour change and a source change on the way to the same frame
        // rasterize once.
        private void InvalidateTexture()
        {
            _rendered = false;
            InvalidateArrange();
        }

        // The colour of the text around it: a formula is text, and it belongs in whatever the
        // rest of the line is drawn in.
        private Color TextColor => Foreground is SolidColorBrush brush ? brush.Color : Colors.Black;

        private void Render()
        {
            if (_surface == null || LayoutRoot == null)
            {
                return;
            }

            var scale = XamlRoot?.RasterizationScale ?? 1;
            var color = TextColor;

            if (_rendered && _renderedScale == scale && _renderedColor == color)
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
            _renderedColor = color;

            var bitmap = new WriteableBitmap(width, height);

            // Drawn by the device rather than by the formula: the factories are there, and one
            // of them makes the bitmap this ends up in.
            Direct2D.Current.RenderMath(_surface, bitmap.PixelBuffer, scale, color);

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
            // at which the formula is about to be seen, it runs once for however many of the
            // three inputs moved, and a recycled element that comes back unchanged draws
            // nothing again.
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
            // The parsed formula is a box tree the size of the expression, and this element is
            // recycled by setting the source to nothing - so the previous one goes now rather
            // than whenever the projection is collected.
            _surface?.Dispose();
            _surface = null;

            _rendered = false;
            LayoutRoot?.ClearValue(Image.SourceProperty);

            if (string.IsNullOrEmpty(newValue))
            {
                InvalidateMeasure();
                return;
            }

            try
            {
                _surface = new RichMathSurface(newValue);
            }
            catch
            {
                // An expression this cannot parse: IsValid says so, and the caller renders what
                // was written as text instead.
            }

            InvalidateMeasure();
        }

        #endregion
    }
}
