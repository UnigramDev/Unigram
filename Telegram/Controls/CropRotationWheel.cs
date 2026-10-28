//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    /// <summary>
    /// A dial of ticks under the crop that turns the image by up to
    /// 45 degrees either way.
    /// </summary>
    public partial class CropRotationWheel : Control
    {
        private const double MaximumAngle = 45;

        // About 5.2 pixels a degree.
        private const double DegreesPerPixel = 1 / (Math.PI * 1.65);

        // Crossing zero stops there, and this much travel is swallowed before the dial moves on.
        private const double HoldPixels = 12;

        // The dial turns twice as far as the image, a tick every five degrees of dial.
        private const int TickAngle = 5;
        private const int TickCount = 16;

        private readonly Rectangle[] _ticks = new Rectangle[TickCount * 2 - 1];

        private Canvas TicksPanel;
        private TextBlock Label;

        private double _angle;

        private uint? _pointerId;
        private double _previousX;

        // Travel since the dial stopped on zero, in pixels, or null when it is not holding there.
        private double? _hold;

        public CropRotationWheel()
        {
            DefaultStyleKey = typeof(CropRotationWheel);
        }

        public event EventHandler RotationStarted;
        public event EventHandler RotationChanged;
        public event EventHandler RotationCompleted;

        #region ActiveForeground

        public Brush ActiveForeground
        {
            get { return (Brush)GetValue(ActiveForegroundProperty); }
            set { SetValue(ActiveForegroundProperty, value); }
        }

        public static readonly DependencyProperty ActiveForegroundProperty =
            DependencyProperty.Register("ActiveForeground", typeof(Brush), typeof(CropRotationWheel), new PropertyMetadata(null));

        #endregion

        /// <summary>
        /// Degrees, clockwise. Setting it does not raise <see cref="RotationChanged"/>.
        /// </summary>
        public double Angle
        {
            get => _angle;
            set
            {
                _angle = Math.Clamp(value, -MaximumAngle, MaximumAngle);
                Invalidate();
            }
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            TicksPanel = GetTemplateChild(nameof(TicksPanel)) as Canvas;
            Label = GetTemplateChild(nameof(Label)) as TextBlock;

            for (int i = 0; i < _ticks.Length; i++)
            {
                TicksPanel.Children.Add(_ticks[i] = new Rectangle());
            }

            TicksPanel.SizeChanged += OnSizeChanged;
            Invalidate();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Invalidate();
        }

        #region Input

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (_pointerId == null && CapturePointer(e.Pointer))
            {
                _pointerId = e.Pointer.PointerId;
                _previousX = e.GetCurrentPoint(this).Position.X;
                _hold = null;

                RotationStarted?.Invoke(this, EventArgs.Empty);
            }

            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_pointerId != e.Pointer.PointerId)
            {
                return;
            }

            var x = e.GetCurrentPoint(this).Position.X;

            // Dragging left turns clockwise.
            var delta = _previousX - x;
            _previousX = x;

            double angle;

            if (_hold is double hold)
            {
                angle = Release(hold + delta);
            }
            else
            {
                angle = _angle + delta * DegreesPerPixel;

                // Crossing or landing on zero stops there; whatever travel went past it counts
                // towards leaving again.
                if (_angle != 0 && (angle == 0 || Math.Sign(angle) != Math.Sign(_angle)))
                {
                    angle = Release(angle / DegreesPerPixel);
                }
            }

            angle = Math.Clamp(angle, -MaximumAngle, MaximumAngle);

            if (Math.Abs(angle - _angle) > 0.0001)
            {
                _angle = angle;

                Invalidate();
                RotationChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Holds on zero until the travel since stopping there exceeds HoldPixels, in either direction.
        private double Release(double travel)
        {
            if (Math.Abs(travel) < HoldPixels)
            {
                _hold = travel;
                return 0;
            }

            _hold = null;
            return (travel - Math.Sign(travel) * HoldPixels) * DegreesPerPixel;
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            base.OnPointerReleased(e);
            Complete(e);
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            base.OnPointerCanceled(e);
            Complete(e);
        }

        protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            Complete(e);
        }

        private void Complete(PointerRoutedEventArgs e)
        {
            if (_pointerId == e.Pointer.PointerId)
            {
                _pointerId = null;
                ReleasePointerCapture(e.Pointer);

                RotationCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            base.OnKeyDown(e);

            var angle = e.Key switch
            {
                VirtualKey.Left => _angle - 1,
                VirtualKey.Right => _angle + 1,
                VirtualKey.Home => 0,
                _ => double.NaN
            };

            if (double.IsNaN(angle) || _pointerId != null)
            {
                return;
            }

            RotationStarted?.Invoke(this, EventArgs.Empty);
            Angle = Math.Round(angle);
            RotationChanged?.Invoke(this, EventArgs.Empty);
            RotationCompleted?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        #endregion

        #region Drawing

        private void Invalidate()
        {
            if (TicksPanel == null)
            {
                return;
            }

            Label.Text = string.Format("{0:0.0}°", _angle);

            var width = TicksPanel.ActualWidth;
            var height = TicksPanel.ActualHeight;

            if (width == 0 || height == 0)
            {
                return;
            }

            // The ticks between zero and the current angle take the active colour, and so does
            // the one the dial has come to rest on.
            var angle = -_angle * 2;
            var delta = angle % TickAngle;
            var segments = (int)Math.Floor(angle / TickAngle);

            for (int i = 0; i < TickCount; i++)
            {
                var a = i;
                var active = a < segments || (a == 0 && delta < 0);

                UpdateTick(_ticks[i], a, delta, width, height, a == segments || (a == 0 && segments == -1), active);

                if (i != 0)
                {
                    a = -i;
                    UpdateTick(_ticks[TickCount - 1 + i], a, delta, width, height, a == segments + 1, a > segments);
                }
            }
        }

        private void UpdateTick(Rectangle tick, int index, double delta, double width, double height, bool center, bool active)
        {
            var radius = width / 2;

            var angle = (90 - (index * TickAngle + delta)) * Math.PI / 180;
            var offset = radius * Math.Cos(angle);
            var f = Math.Abs(offset) / radius;

            var tickWidth = center ? 2d : 1d;
            var tickHeight = center ? 16d : 12d;

            tick.Fill = center || active ? ActiveForeground : Foreground;
            tick.Opacity = Math.Clamp(1 - f * f, 0, 1);
            tick.Width = tickWidth;
            tick.Height = tickHeight;

            Canvas.SetLeft(tick, width / 2 + offset - tickWidth / 2);
            Canvas.SetTop(tick, (height - tickHeight) / 2);
        }

        #endregion
    }
}
