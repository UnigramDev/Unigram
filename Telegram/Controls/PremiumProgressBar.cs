//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Native.Composition;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    public partial class PremiumProgressBar : RangeBase
    {
        private AnimatedTextBlock ValueText;
        private TextBlock MaximumLabel;
        private Grid ValueRoot;
        private Canvas ThumbRoot;
        private Grid Thumb;
        private Path Arrow;

        private DirectRectangleClip2 _thumbClip;
        private DirectRectangleClip2 _valueClip;

        private Visual _thumbRoot;
        private Visual _thumb;
        private Visual _arrow;

        private HorizontalAlignment _arrowAlignment;
        private double _prevValue = 0;

        public PremiumProgressBar()
        {
            DefaultStyleKey = typeof(PremiumProgressBar);
        }

        private void UpdateText()
        {
            ValueText.Text = Formatter.ShortRating(Value, false);
        }

        protected override void OnApplyTemplate()
        {
            ValueText = GetTemplateChild(nameof(ValueText)) as AnimatedTextBlock;
            MaximumLabel = GetTemplateChild(nameof(MaximumLabel)) as TextBlock;
            ValueRoot = GetTemplateChild(nameof(ValueRoot)) as Grid;
            ThumbRoot = GetTemplateChild(nameof(ThumbRoot)) as Canvas;
            Thumb = GetTemplateChild(nameof(Thumb)) as Grid;
            Arrow = GetTemplateChild(nameof(Arrow)) as Path;

            ElementCompositionPreview.SetIsTranslationEnabled(Arrow, true);

            _thumbRoot = ElementComposition.GetElementVisual(ThumbRoot);
            _thumb = ElementComposition.GetElementVisual(Thumb);
            _arrow = ElementComposition.GetElementVisual(Arrow);

            var radius1 = new Vector2(20);
            var radius2 = new Vector2(4);

            _thumbRoot.CenterPoint = new Vector3(0, 46, 0);

            _thumbClip = CompositionDevice.CreateRectangleClip2(Thumb.Children[0]);
            _thumbClip.Set(radius1);

            _valueClip = CompositionDevice.CreateRectangleClip2(ValueRoot);
            _valueClip.Set(radius2);

            Thumb.SizeChanged += OnSizeChanged;

            UpdateText();

            MaximumLabel.Text = "/" + Formatter.ShortRating(Maximum, false);

            OnValueChanged(_prevValue, Value);
            base.OnApplyTemplate();
        }

        enum PointerState
        {
            Released = 0,
            Moved = 1,
            Pressed = 2
        }

        enum TransitionState
        {
            None = 0,
            Entrance = 1,
            Exit = 2
        }

        private PointerState _animateState;

        private double _animateTo;
        private TransitionState _transition;

        private bool _animating;

        private double _oldValue;
        private double _newValue;
        private float _angle;

        private EventHandler<object> _rendering;

        public void Animate(long to, TimeSpan? delay = null)
        {
            _animateTo = (double)to;

            var anim = new DoubleAnimation();
            anim.From = Value;
            anim.To = to;
            anim.Duration = TimeSpan.FromMilliseconds(333);
            anim.EnableDependentAnimation = true;
            anim.EasingFunction = new Windows.UI.Xaml.Media.Animation.CubicEase
            {
                EasingMode = EasingMode.EaseInOut
            };

            Storyboard.SetTarget(anim, this);
            Storyboard.SetTargetProperty(anim, "Value");

            _animating = true;
            _transition = delay == null ? TransitionState.None : delay == TimeSpan.Zero ? TransitionState.Exit : TransitionState.Entrance;

            _oldValue = Value;
            _newValue = Value;

            var storyboard = new Storyboard();
            storyboard.BeginTime = delay;
            storyboard.Children.Add(anim);
            storyboard.Begin();
        }

        private void OnRendering(object sender, object e)
        {
            var progress = Math.Max((float)Math.Abs(_oldValue - _newValue), 1);
            progress = MathF.Log(progress, (float)Math.Abs(Maximum - Minimum) / 16);
            progress = progress * MathF.Pow(progress, 2);

            var bend = 24 * Math.Clamp(progress, 0, 10);
            var angle = _oldValue < _newValue ? bend : -bend;

            if (Math.Abs(_angle - angle) > .01f)
            {
                angle = MathFEx.Lerp(_angle, angle, .1f);
            }

            _thumbRoot.RotationAngleInDegrees = angle;

            _oldValue = _newValue;
            _angle = angle;

            if (angle.AlmostEqualsToZero(1e-2f))
            {
                _thumbRoot.RotationAngleInDegrees = 0;
                _rendering = null;

                Windows.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;
            }
        }

        protected override void OnMaximumChanged(double oldMaximum, double newMaximum)
        {
            MaximumLabel?.Text = "/" + Formatter.ShortRating(Maximum, false);

            base.OnMaximumChanged(oldMaximum, newMaximum);
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            _newValue = newValue;
            _animateState = PointerState.Released;

            base.OnValueChanged(oldValue, newValue);

            if (ValueText == null)
            {
                return;
            }

            UpdateText();
            UpdateClip();

            if (_rendering == null && _animating)
            {
                Windows.UI.Xaml.Media.CompositionTarget.Rendering += _rendering = new EventHandler<object>(OnRendering);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateClip();
        }

        private void UpdateClip()
        {
            if (ValueRoot == null || Thumb == null)
            {
                return;
            }

            var thumbWidth = 0; // ValueRoot.ActualSize.Y;

            var value = (float)((Value - Minimum) / (Maximum - Minimum));
            var clipWidth = (ValueRoot.ActualSize.X - (thumbWidth)) * (float.IsNaN(value) ? 0 : value);

            var radius = new Vector2(thumbWidth / 2);

            if (Minimum < 0)
            {
                _valueClip.SetInset(clipWidth + thumbWidth, 0, ValueRoot.ActualSize.X, ValueRoot.ActualSize.Y);
            }
            else
            {
                _valueClip.SetInset(0, 0, clipWidth + thumbWidth, ValueRoot.ActualSize.Y);
            }

            _thumbRoot.Offset = new Vector3(clipWidth + radius.X, 0, 0);

            var center = Thumb.ActualSize.X / 2;

            var width = (ValueRoot.ActualSize.X - (thumbWidth)) * (float)((Value - Minimum) / (Maximum - Minimum));
            width += thumbWidth / 2;

            var toWidth = (ValueRoot.ActualSize.X - (thumbWidth)) * (float)((_animateTo - Minimum) / (Maximum - Minimum));
            toWidth += thumbWidth / 2;

            var radiusLeft = 20f;
            var radiusRight = 20f;

            bool shouldClampLeft = false;
            bool shouldClampRight = false;

            if (_transition == TransitionState.Entrance)
            {
                shouldClampLeft = width < center - 12 && toWidth < center - 12;
                shouldClampRight = width > ValueRoot.ActualSize.X - center + 12 && toWidth > ValueRoot.ActualSize.X - center + 20;
            }
            else if (_transition == TransitionState.None)
            {
                shouldClampLeft = width < center - 12 || Value == Minimum;
                shouldClampRight = width > ValueRoot.ActualSize.X - center + 12 || Value == Maximum;
            }

            if (shouldClampLeft)
            {
                radiusLeft = width - center + 12;

                _thumb.Offset = new Vector3(-width - 12, 0, 0);
                _arrow.Properties.InsertVector3("Translation", new Vector3(radiusLeft, 0, 0));
            }
            else if (shouldClampRight)
            {
                radiusRight = ValueRoot.ActualSize.X - width - Thumb.ActualSize.X + center + 12;

                _thumb.Offset = new Vector3((ValueRoot.ActualSize.X - width - Thumb.ActualSize.X + 12), 0, 0);
                _arrow.Properties.InsertVector3("Translation", new Vector3(-radiusRight, 0, 0));
            }
            else
            {
                _thumb.Offset = new Vector3(-Thumb.ActualSize.X / 2, 0, 0);
                _arrow.Properties.InsertVector3("Translation", new Vector3());
            }

            Arrow.Fill = GetArrowFill(value);

            Vector2 CalculateRadius(float diff)
            {
                diff = center + diff - Arrow.ActualSize.X / 2;
                diff = Math.Min(diff + 2, 20);
                diff = Math.Max(diff, 8);
                return new Vector2(diff, 20);
            }

            _thumbClip.SetInset(0, 0, Thumb.ActualSize.X, 40);

            _thumbClip.BottomLeft = CalculateRadius(radiusLeft);
            _thumbClip.BottomRight = CalculateRadius(radiusRight);

            float CalculateRadius2(float diff)
            {
                diff = center + diff - Arrow.ActualSize.X / 2;
                diff = Math.Min(diff + 2, 20);
                diff = Math.Max(diff, 2);
                return diff;
            }

            if ((center + radiusLeft - Arrow.ActualSize.X / 2) <= 6)
            {
                _arrowCentered = false;
                CreatePathGeometry(CalculateRadius2(radiusLeft), 0);
            }
            else if ((center + radiusRight - Arrow.ActualSize.X / 2) <= 6)
            {
                _arrowCentered = false;
                CreatePathGeometry(0, CalculateRadius2(radiusRight));
            }
            else if (!_arrowCentered)
            {
                _arrowCentered = true;
                CreatePathGeometry(0, 0);
            }
        }

        private SolidColorBrush GetArrowFill(double amount)
        {
            return Background switch
            {
                SolidColorBrush solid => solid,
                LinearGradientBrush linear => new SolidColorBrush(ColorsHelper.Mix(linear.GradientStops[0].Color, linear.GradientStops[^1].Color, amount)),
                _ => null
            };
        }

        private bool _arrowCentered = true;

        public void CreatePathGeometry(double radiusLeft, double radiusRight)
        {
            if (Arrow.Data is not PathGeometry pathGeometry)
            {
                return;
            }

            var pathFigure = new PathFigure();
            pathFigure.IsClosed = true;

            if (radiusLeft != 0)
            {
                pathFigure.StartPoint = new Point(6 - radiusLeft, -1);
            }
            else
            {
                pathFigure.StartPoint = new Point(0, 0);
                pathFigure.Segments.Add(new LineSegment { Point = new Point(0, 2) });

                pathFigure.Segments.Add(new BezierSegment
                {
                    Point1 = new Point(0.923074, 2),
                    Point2 = new Point(1.80816, 2.36679),
                    Point3 = new Point(2.46094, 3.01953)
                });
            }

            pathFigure.Segments.Add(new LineSegment { Point = new Point(6.76172, 7.32031) });

            pathFigure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(7.23956, 7.79815),
                Point2 = new Point(8, 8),
                Point3 = new Point(8.5, 7.99512)
            });

            pathFigure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(9, 8),
                Point2 = new Point(9.76044, 7.79815),
                Point3 = new Point(10.2383, 7.32031)
            });

            if (radiusRight != 0)
            {
                pathFigure.Segments.Add(new LineSegment { Point = new Point(17 - (6 - radiusRight), -1) });
            }
            else
            {
                pathFigure.Segments.Add(new LineSegment { Point = new Point(14.5391, 3.01953) });

                pathFigure.Segments.Add(new BezierSegment
                {
                    Point1 = new Point(15.1918, 2.36679),
                    Point2 = new Point(16.0769, 2),
                    Point3 = new Point(17, 2)
                });

                pathFigure.Segments.Add(new LineSegment { Point = new Point(17, 0) });
                pathFigure.Segments.Add(new LineSegment { Point = new Point(17, 0) });
            }

            var measure = new PathFigure();
            measure.StartPoint = new Point(0, 0);
            measure.Segments.Add(new LineSegment { Point = new Point(17, 8) });

            pathGeometry.Figures.Clear();
            pathGeometry.Figures.Add(pathFigure);
            pathGeometry.Figures.Add(measure);
        }

        #region MinimumText

        public string MinimumText
        {
            get { return (string)GetValue(MinimumTextProperty); }
            set { SetValue(MinimumTextProperty, value); }
        }

        public static readonly DependencyProperty MinimumTextProperty =
            DependencyProperty.Register("MinimumText", typeof(string), typeof(PremiumProgressBar), new PropertyMetadata(string.Empty));

        #endregion

        #region MaximumText

        public string MaximumText
        {
            get { return (string)GetValue(MaximumTextProperty); }
            set { SetValue(MaximumTextProperty, value); }
        }

        public static readonly DependencyProperty MaximumTextProperty =
            DependencyProperty.Register("MaximumText", typeof(string), typeof(PremiumProgressBar), new PropertyMetadata(string.Empty));

        #endregion

        #region MaximumVisibility

        public Visibility MaximumVisibility
        {
            get { return (Visibility)GetValue(MaximumVisibilityProperty); }
            set { SetValue(MaximumVisibilityProperty, value); }
        }

        public static readonly DependencyProperty MaximumVisibilityProperty =
            DependencyProperty.Register("MaximumVisibility", typeof(Visibility), typeof(PremiumProgressBar), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region ValueVisibility

        public Visibility ValueVisibility
        {
            get { return (Visibility)GetValue(ValueVisibilityProperty); }
            set { SetValue(ValueVisibilityProperty, value); }
        }

        public static readonly DependencyProperty ValueVisibilityProperty =
            DependencyProperty.Register("ValueVisibility", typeof(Visibility), typeof(PremiumProgressBar), new PropertyMetadata(Visibility.Visible));

        #endregion

        #region Glyph

        public string Glyph
        {
            get { return (string)GetValue(GlyphProperty); }
            set { SetValue(GlyphProperty, value); }
        }

        public static readonly DependencyProperty GlyphProperty =
            DependencyProperty.Register("Glyph", typeof(string), typeof(PremiumProgressBar), new PropertyMetadata(string.Empty));

        #endregion
    }
}
