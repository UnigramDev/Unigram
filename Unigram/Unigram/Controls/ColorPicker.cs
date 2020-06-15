using System;
using Unigram.Common;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Spectrum", Type = typeof(Border))]
    [TemplatePart(Name = "SpectrumThumb", Type = typeof(UIElement))]
    [TemplatePart(Name = "ValueSlider", Type = typeof(Slider))]
    public class ColorPicker : Control
    {
        private Border Spectrum;
        private UIElement SpectrumThumb;

        private Slider ValueSlider;

        private bool _loaded;
        private bool _pressed;

        private HSV _color = new HSV(0, 1, 1);
        private double _value = 1;
        private Color _current;

        public ColorPicker()
        {
            DefaultStyleKey = typeof(ColorPicker);
        }

        protected override void OnApplyTemplate()
        {
            _current = _color.ToRGB();

            Spectrum = GetTemplateChild("Spectrum") as Border;
            SpectrumThumb = GetTemplateChild("SpectrumThumb") as UIElement;

            ValueSlider = GetTemplateChild("ValueSlider") as Slider;

            if (Spectrum != null)
            {
                var colors = new Color[] { Colors.Red, Colors.Yellow, Colors.Lime, Colors.Cyan, Colors.Blue, Colors.Magenta, Colors.Red };
                var gradient = new LinearGradientBrush();
                gradient.StartPoint = new Point(0, 0);
                gradient.EndPoint = new Point(1, 0);

                for (int i = 0; i < colors.Length; i++)
                {
                    gradient.GradientStops.Add(new GradientStop { Color = colors[i], Offset = 1d / (colors.Length - 1) * i });
                }

                Spectrum.Background = gradient;
                Spectrum.SizeChanged += OnSizeChanged;
                Spectrum.PointerMoved += OnPointerMoved;
                Spectrum.PointerPressed += OnPointerPressed;
                Spectrum.PointerReleased += OnPointerReleased;
                Spectrum.PointerCanceled += OnPointerReleased;
                Spectrum.PointerCaptureLost += OnPointerReleased;
            }

            if (ValueSlider != null)
            {
                ValueSlider.ValueChanged += OnValueChanged;
            }

            OnColorChanged(Color, Color);

            base.OnApplyTemplate();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSpectrumPointer(new Point(_color.H / 360 * e.NewSize.Width, (1 - _color.S) * e.NewSize.Height), false);
        }

        private void OnValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            _value = e.NewValue / 100;
            _current = new HSV(_color.H, _color.S, _value).ToRGB();

            Color = _current;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_pressed)
            {
                UpdateSpectrumPointer(e.GetCurrentPoint(Spectrum));
            }
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _pressed = true;
            Spectrum.CapturePointer(e.Pointer);
            UpdateSpectrumPointer(e.GetCurrentPoint(Spectrum));
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _pressed = false;
            Spectrum.ReleasePointerCapture(e.Pointer);
            UpdateSpectrumPointer(e.GetCurrentPoint(Spectrum));
        }

        private void UpdateSpectrumPointer(PointerPoint pointer)
        {
            UpdateSpectrumPointer(pointer.Position, true);
        }

        private void UpdateSpectrumPointer(Point point, bool values)
        {
            point.X = Math.Max(0, Math.Min(Spectrum.ActualWidth, point.X));
            point.Y = Math.Max(0, Math.Min(Spectrum.ActualHeight, point.Y));

            var h = point.X / Spectrum.ActualWidth * 360;
            var s = 1 - point.Y / Spectrum.ActualHeight;

            if (values)
            {
                _color = new HSV(h, s, 1);
                _current = new HSV(h, s, _value).ToRGB();

                Color = _current;
            }

            if (SpectrumThumb != null)
            {
                Canvas.SetLeft(SpectrumThumb, Math.Max(0, Math.Min(Spectrum.ActualWidth - 20, point.X - 10)));
                Canvas.SetTop(SpectrumThumb, Math.Max(0, Math.Min(Spectrum.ActualHeight - 20, point.Y - 10)));
            }

            if (ValueSlider != null)
            {
                var gradient = new LinearGradientBrush();
                gradient.StartPoint = new Point(0, 0);
                gradient.EndPoint = new Point(1, 0);
                gradient.GradientStops.Add(new GradientStop { Color = new HSV(h, s, 1).ToRGB() });
                gradient.GradientStops.Add(new GradientStop { Color = new HSV(h, s, 0).ToRGB(), Offset = 1 });

                ValueSlider.Foreground = gradient;
            }
        }

        public event TypedEventHandler<ColorPicker, ColorChangedEventArgs> ColorChanged;

        #region Color

        public Color Color
        {
            get { return (Color)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }

        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register("Color", typeof(Color), typeof(ColorPicker), new PropertyMetadata(Colors.Red, OnColorChanged));

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ColorPicker)d).OnColorChanged((Color)e.NewValue, (Color)e.OldValue);
        }

        private void OnColorChanged(Color newValue, Color oldValue)
        {
            if (newValue == oldValue && _loaded)
            {
                return;
            }

            ColorChanged?.Invoke(this, new ColorChangedEventArgs(newValue, oldValue));

            if (newValue == _current && _loaded)
            {
                return;
            }

            var rgb = (RGB)newValue;
            var hsv = rgb.ToHSV();


            _current = newValue;
            _color = new HSV(hsv.H, hsv.S, 1);
            _value = hsv.V;

            if (Spectrum != null && ValueSlider != null)
            {
                _loaded = true;

                UpdateSpectrumPointer(new Point(hsv.H / 360 * Spectrum.ActualWidth, (1 - hsv.S) * Spectrum.ActualHeight), false);
                ValueSlider.Value = hsv.V * 100;
            }
        }

        #endregion

    }

    public sealed class ColorChangedEventArgs : EventArgs
    {
        public ColorChangedEventArgs(Color newColor, Color oldColor)
        {
            NewColor = newColor;
            OldColor = oldColor;
        }

        public Color NewColor { get; }

        public Color OldColor { get; }
    }
}
