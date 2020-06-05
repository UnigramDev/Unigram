using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    public class ColorSlider : Control
    {
        private Dictionary<uint, Point> m_pointerPositions;

        private FrameworkElement _layoutRoot;
        private FrameworkElement _container;

        private FrameworkElement _thumb;
        private Ellipse _thumbDrop;

        private Visual _thumbVisual;

        private Vector2 _value;
        private Vector2 _current;

        public ColorSlider()
        {
            DefaultStyleKey = typeof(ColorSlider);
            SizeChanged += OnSizeChanged;

            m_pointerPositions = new Dictionary<uint, Point>();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetDefault(_value);
        }

        #region Properties

        public event EventHandler StrokeChanged;

        public Color Stroke => ColorForValue(_value.X);

        public float StrokeThickness => 4 + _value.Y * 16;

        public void SetDefault(Vector2 value)
        {
            _value = new Vector2(value.X, value.Y);
            _current = _value;

            if (_layoutRoot == null || _layoutRoot.ActualWidth == 0)
            {
                return;
            }

            var w = (float)_layoutRoot.ActualWidth;
            var h = 180f;

            var weight = 4 + (_current.Y * 16);

            _thumbVisual.Offset = new Vector3(_current.X * w, 0, 0);

            _thumbDrop.StrokeThickness = 20 - (weight * 2) / 2;
            _thumbDrop.Fill = new SolidColorBrush(ColorForValue(_current.X));
        }

        public Vector2 GetDefault()
        {
            return new Vector2(_value.X, _value.Y);
        }

        #endregion

        protected override void OnApplyTemplate()
        {
            var gradient = new LinearGradientBrush();
            gradient.StartPoint = new Point(0, 0);
            gradient.EndPoint = new Point(1, 0);

            for (int i = 0; i < _colors.Length; i++)
            {
                gradient.GradientStops.Add(new GradientStop
                {
                    Color = _colors[i],
                    Offset = _offsets[i]
                });
            }

            _layoutRoot = (FrameworkElement)GetTemplateChild("LayoutRoot");

            _container = (FrameworkElement)GetTemplateChild("Container");
            _container.PointerPressed += Thumb_PointerPressed;
            _container.PointerMoved += Thumb_PointerMoved;
            _container.PointerReleased += Thumb_PointerReleased;

            _thumb = (FrameworkElement)GetTemplateChild("Thumb");

            _thumbVisual = ElementCompositionPreview.GetElementVisual(_thumb);
            _thumbVisual.CenterPoint = new Vector3(24);
            _thumbVisual.Scale = new Vector3(0.5f);

            _thumbDrop = (Ellipse)GetTemplateChild("ThumbDrop");

            Background = gradient;
            SetDefault(_value);

            base.OnApplyTemplate();
        }

        private void Thumb_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ((UIElement)sender).CapturePointer(e.Pointer);

            var pointer = e.GetCurrentPoint(_container);
            m_pointerPositions[pointer.PointerId] = pointer.Position;

            _current = new Vector2(_value.X, 0);

            UpdateThumb(pointer.Position, true, true);
            e.Handled = true;
        }

        private void Thumb_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var pointer = e.GetCurrentPoint(_container);
            m_pointerPositions.Remove(pointer.PointerId);

            ((UIElement)sender).ReleasePointerCapture(e.Pointer);

            _value = _current;
            StrokeChanged?.Invoke(this, EventArgs.Empty);

            UpdateThumb(pointer.Position, true, false);
            e.Handled = true;
        }

        private void Thumb_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.Pointer.IsInContact && m_pointerPositions.TryGetValue(e.Pointer.PointerId, out _))
            {
                UpdateThumb(e.GetCurrentPoint(_container).Position.ToVector2());
                e.Handled = true;
            }
        }

        private void UpdateThumb(Point position, bool animate = false, bool zoom = true)
        {
            UpdateThumb(position.ToVector2(), animate, zoom);
        }

        private void UpdateThumb(Vector2 position, bool animate = false, bool zoom = true)
        {
            if (_layoutRoot == null)
            {
                return;
            }

            var w = (float)_layoutRoot.ActualWidth;
            var h = 180f;

            var offsetX = position.X / w;
            var left = Math.Clamp(offsetX, 0, 1);
            var bottom = _current.Y;

            _current.X = left;

            if (position.Y < h)
            {
                var offsetY = position.Y / h;
                bottom = 1 - Math.Clamp(offsetY, 0, 1);

                _current.Y = bottom;
            }
            else
            {
                bottom = 0;
            }

            var weight = 4 + (_current.Y * 16);

            _thumbDrop.StrokeThickness = 20 - (weight * 2) / 2;
            _thumbDrop.Fill = new SolidColorBrush(ColorForValue(_current.X));

            if (animate)
            {
                _thumbVisual.StopAnimation("Scale");
                _thumbVisual.StopAnimation("Offset");

                //if (ApiInformation.IsMethodPresent(".Compositor", "CreateSpringVector3Animation"))
                if (false)
                {
                    var scale = Window.Current.Compositor.CreateSpringVector3Animation();
                    scale.InitialValue = new Vector3(zoom ? 0.5f : 1);
                    scale.FinalValue = new Vector3(zoom ? 1 : 0.5f);

                    var offset = Window.Current.Compositor.CreateSpringVector3Animation();
                    //offset.InitialValue = new Vector3(_current.X * w, -60, 0);
                    offset.InitialValue = _thumbVisual.Offset;
                    offset.FinalValue = new Vector3(_current.X * w, zoom ? -60 : 0, 0);

                    _thumbVisual.StartAnimation("Scale", scale);
                    _thumbVisual.StartAnimation("Offset", offset);
                }
                else
                {
                    _container.Height = 200;
                    _container.Margin = new Thickness(0, -180, 0, 0);

                    var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                    batch.Completed += (s, args) =>
                    {
                        _container.Height = zoom ? 200 : 20;
                        _container.Margin = new Thickness(0, zoom ? -180 : 0, 0, 0);
                    };

                    var scale = Window.Current.Compositor.CreateSpringVector3Animation();
                    scale.InitialValue = new Vector3(zoom ? 0.5f : 1);
                    scale.FinalValue = new Vector3(zoom ? 1 : 0.5f);

                    var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                    offset.InsertKeyFrame(0, _thumbVisual.Offset);
                    offset.InsertKeyFrame(1, new Vector3(_current.X * w, zoom ? -60 : 0, 0));

                    _thumbVisual.StartAnimation("Scale", scale);
                    _thumbVisual.StartAnimation("Offset", offset);

                    batch.End();
                }
            }
            else
            {
                //_thumbVisual.StopAnimation("Offset");
                _thumbVisual.Offset = new Vector3(_current.X * w, -(bottom * h) - 60, 0);
            }
        }

        private Color ColorForValue(double location)
        {
            if (location >= 1)
            {
                return _colors[_colors.Length - 1];
            }

            if (location < double.Epsilon)
            {
                return _colors[0];
            }
            else if (location > 1 - double.Epsilon)
            {
                return _colors[_colors.Length - 1];
            }

            var leftIndex = -1;
            var rightIndex = -1;


            for (int i = 1; i < _offsets.Length; i++)
            {
                if (_offsets[i] > location)
                {
                    leftIndex = i - 1;
                    rightIndex = i;
                    break;
                }
            }

            var leftLocation = _offsets[leftIndex];
            var leftColor = _colors[leftIndex];

            var rightLocation = _offsets[rightIndex];
            var rightColor = _colors[rightIndex];

            var factor = (location - leftLocation) / (rightLocation - leftLocation);

            return interpolateColor(leftColor, rightColor, factor);
        }

        private Color interpolateColor(Color color1, Color color2, double factor)
        {
            factor = Math.Clamp(factor, 0, 1);


            var r1 = color1.R / 255f;
            var r2 = color2.R / 255f;
            var g1 = color1.G / 255f;
            var g2 = color2.G / 255f;
            var b1 = color1.B / 255f;
            var b2 = color2.B / 255f;

            var r = r1 + (r2 - r1) * factor;
            var g = g1 + (g2 - g1) * factor;
            var b = b1 + (b2 - b1) * factor;

            return Color.FromArgb(0xFF, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private Color[] _colors = new[]
        {
            Color.FromArgb(0xff, 0xea, 0x27, 0x39),
            Color.FromArgb(0xff, 0xdb, 0x3a, 0xd2),
            Color.FromArgb(0xff, 0x30, 0x51, 0xe3),
            Color.FromArgb(0xff, 0x49, 0xc5, 0xed),
            Color.FromArgb(0xff, 0x80, 0xc8, 0x64),
            Color.FromArgb(0xff, 0xfc, 0xde, 0x65),
            Color.FromArgb(0xff, 0xfc, 0x96, 0x4d),
            Color.FromArgb(0xff, 0x00, 0x00, 0x00),
            Color.FromArgb(0xff, 0xff, 0xff, 0xff)
        };

        private double[] _offsets = new[]
        {
            0.0,  //red
            0.14, //pink
            0.24, //blue
            0.39, //cyan
            0.49, //green
            0.62, //yellow
            0.73, //orange
            0.85, //black
            1.0
        };
    }
}
