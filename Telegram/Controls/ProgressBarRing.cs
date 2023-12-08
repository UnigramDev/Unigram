//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public class ProgressBarRing : ProgressBar
    {
        private readonly FrameworkElementState _manager;

        private readonly ShapeVisual _visual;
        private readonly CompositionSpriteShape _shape;
        private readonly CompositionEllipseGeometry _ellipse;

        private bool _spinning;
        private readonly ScalarKeyFrameAnimation _foreverAnimation;

        public ProgressBarRing()
        {
            DefaultStyleKey = typeof(ProgressBarRing);

            var ellipse = Window.Current.Compositor.CreateEllipseGeometry();
            ellipse.Radius = new Vector2((float)Radius);
            ellipse.Center = new Vector2((float)Center);
            ellipse.TrimEnd = 0f;

            var shape = Window.Current.Compositor.CreateSpriteShape(ellipse);
            shape.CenterPoint = new Vector2((float)Center);
            shape.StrokeThickness = 2;
            shape.StrokeStartCap = CompositionStrokeCap.Round;
            shape.StrokeEndCap = CompositionStrokeCap.Round;

            if (Foreground is SolidColorBrush brush)
            {
                shape.StrokeBrush = Window.Current.Compositor.CreateColorBrush(brush.Color);
            }

            var visual = Window.Current.Compositor.CreateShapeVisual();
            visual.Shapes.Add(shape);
            visual.Size = new Vector2((float)Center * 2);
            visual.CenterPoint = new Vector3((float)Center);

            var trimStart = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            trimStart.Target = nameof(CompositionGeometry.TrimStart);
            trimStart.InsertExpressionKeyFrame(1.0f, "this.FinalValue", Window.Current.Compositor.CreateLinearEasingFunction());

            var trimEnd = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            trimEnd.Target = nameof(CompositionGeometry.TrimEnd);
            trimEnd.InsertExpressionKeyFrame(1.0f, "this.FinalValue", Window.Current.Compositor.CreateLinearEasingFunction());

            var visibility = Window.Current.Compositor.CreateExpressionAnimation("target.TrimEnd > 0 && target.TrimEnd < 1");
            visibility.SetReferenceParameter("target", ellipse);

            var animations = Window.Current.Compositor.CreateImplicitAnimationCollection();
            animations[nameof(CompositionGeometry.TrimStart)] = trimStart;
            animations[nameof(CompositionGeometry.TrimEnd)] = trimEnd;

            //ellipse.ImplicitAnimations = animations;
            //visual.StartAnimation("IsVisible", visibility);
            //visual.StartAnimation("RotationAngleInDegrees", forever);

            _visual = visual;
            _shape = shape;
            _ellipse = ellipse;

            var easing = Window.Current.Compositor.CreateLinearEasingFunction();
            var forever = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            forever.InsertKeyFrame(0, 240, easing);
            forever.InsertKeyFrame(1, 599, easing);
            forever.IterationBehavior = AnimationIterationBehavior.Forever;
            forever.Duration = TimeSpan.FromSeconds(3);

            _foreverAnimation = forever;

            ElementCompositionPreview.SetElementChildVisual(this, visual);
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);

            _manager = new FrameworkElementState(this);
            _manager.Loaded += OnLoaded;
            _manager.Unloaded += OnUnloaded;
        }

        public double Radius { get; set; } = 21;
        public double Center { get; set; } = 24;

        public double Thickness { get; set; } = 2;

        public bool Spin { get; set; } = true;

        public bool ShrinkOut { get; set; } = true;

        protected override void OnApplyTemplate()
        {
            _ellipse.Radius = new Vector2((float)Radius);
            _ellipse.Center = new Vector2((float)Center);

            _shape.CenterPoint = new Vector2((float)Center);
            _shape.StrokeThickness = (float)Thickness;

            _visual.Size = new Vector2((float)Center * 2);
            _visual.CenterPoint = new Vector3((float)Center);

            base.OnApplyTemplate();
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_shape != null && Foreground is SolidColorBrush brush)
            {
                _shape.StrokeBrush = Window.Current.Compositor.CreateColorBrush(brush.Color);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_foreverAnimation != null)
            {
                _visual.StartAnimation("RotationAngleInDegrees", _foreverAnimation);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_foreverAnimation != null)
            {
                _visual.StopAnimation("RotationAngleInDegrees");
            }
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            //if (newValue > 0)
            //{
            //    newValue = Math.Max(newValue, 0.0001);
            //}
            if (double.IsNaN(newValue))
            {
                newValue = 0;
            }

            if (newValue > 0 && newValue < 0.05)
            {
                newValue = 0.05;
            }

            if (_foreverAnimation != null)
            {
                if (newValue > 0 && newValue < 1)
                {
                    if (!_spinning)
                    {
                        _spinning = true;
                        _visual.RotationAngleInDegrees = 230; // 202
                        _visual.StartAnimation("RotationAngleInDegrees", _foreverAnimation);
                    }
                }
                //else if (_spinning)
                //{
                //    _spinning = false;
                //    _visual.StopAnimation("RotationAngleInDegrees");
                //}
            }

            if (_ellipse != null)
            {
                var diff = Math.Abs(oldValue - newValue);
                if (diff < 0.10 && newValue < 1)
                {
                    _ellipse.TrimStart = 0;
                    _ellipse.TrimEnd = MathF.Max(0, MathF.Min(1, (float)newValue));
                }
                else
                {
                    var linear = Window.Current.Compositor.CreateLinearEasingFunction();
                    var trimStart = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                    var trimEnd = Window.Current.Compositor.CreateScalarKeyFrameAnimation();

                    if (newValue < 1)
                    {
                        //_ellipse.TrimStart = 0;
                        //_ellipse.TrimEnd = MathF.Max(0, MathF.Min(1, (float)newValue));

                        trimStart.InsertKeyFrame(1, 0, linear);
                        trimEnd.InsertKeyFrame(1, MathF.Max(0, MathF.Min(1, (float)newValue)), linear);

                        _ellipse.StartAnimation("TrimStart", trimStart);
                        _ellipse.StartAnimation("TrimEnd", trimEnd);
                    }
                    else
                    {
                        //_ellipse.TrimStart = 1;
                        //_ellipse.TrimEnd = 1;

                        trimStart.InsertKeyFrame(1, ShrinkOut ? 1 : 0, linear);
                        trimEnd.InsertKeyFrame(1, 1, linear);

                        var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                        batch.Completed += (s, args) =>
                        {
                            if (_foreverAnimation != null && _spinning)
                            {
                                _spinning = false;
                                _visual.StopAnimation("RotationAngleInDegrees");
                            }

                            Completed?.Invoke(this, EventArgs.Empty);
                        };

                        _ellipse.StartAnimation("TrimStart", trimStart);
                        _ellipse.StartAnimation("TrimEnd", trimEnd);

                        batch.End();
                    }
                }
            }

            //if (newValue is >= 1.0 or <= 0.0)
            //{
            //    Visibility = Visibility.Collapsed;
            //}
            //else
            //{
            //    Visibility = Visibility.Visible;
            //}
        }

        public event EventHandler Completed;
    }
}
