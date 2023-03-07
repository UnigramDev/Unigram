//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Numerics;
using Unigram.Navigation;

namespace Unigram.Controls
{
    public class ProgressBarRing : ProgressBar
    {
        private readonly ShapeVisual _visual;
        private readonly CompositionSpriteShape _shape;
        private readonly CompositionEllipseGeometry _ellipse;

        private bool _spinning;
        private readonly ScalarKeyFrameAnimation _foreverAnimation;

        public ProgressBarRing()
        {
            DefaultStyleKey = typeof(ProgressBarRing);

            var compositor = BootStrapper.Current.Compositor;

            var ellipse = compositor.CreateEllipseGeometry();
            ellipse.Radius = new Vector2((float)Radius);
            ellipse.Center = new Vector2((float)Center);
            ellipse.TrimEnd = 0f;

            var shape = compositor.CreateSpriteShape(ellipse);
            shape.CenterPoint = new Vector2((float)Center);
            shape.StrokeThickness = 2;
            shape.StrokeStartCap = CompositionStrokeCap.Round;
            shape.StrokeEndCap = CompositionStrokeCap.Round;

            if (Foreground is SolidColorBrush brush)
            {
                shape.StrokeBrush = compositor.CreateColorBrush(brush.Color);
            }

            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape);
            visual.Size = new Vector2((float)Center * 2);
            visual.CenterPoint = new Vector3((float)Center);

            var trimStart = compositor.CreateScalarKeyFrameAnimation();
            trimStart.Target = nameof(CompositionGeometry.TrimStart);
            trimStart.InsertExpressionKeyFrame(1.0f, "this.FinalValue", compositor.CreateLinearEasingFunction());

            var trimEnd = compositor.CreateScalarKeyFrameAnimation();
            trimEnd.Target = nameof(CompositionGeometry.TrimEnd);
            trimEnd.InsertExpressionKeyFrame(1.0f, "this.FinalValue", compositor.CreateLinearEasingFunction());

            var visibility = compositor.CreateExpressionAnimation("target.TrimEnd > 0 && target.TrimEnd < 1");
            visibility.SetReferenceParameter("target", ellipse);

            var animations = compositor.CreateImplicitAnimationCollection();
            animations[nameof(CompositionGeometry.TrimStart)] = trimStart;
            animations[nameof(CompositionGeometry.TrimEnd)] = trimEnd;

            //ellipse.ImplicitAnimations = animations;
            //visual.StartAnimation("IsVisible", visibility);
            //visual.StartAnimation("RotationAngleInDegrees", forever);

            _visual = visual;
            _shape = shape;
            _ellipse = ellipse;

            var easing = compositor.CreateLinearEasingFunction();
            var forever = compositor.CreateScalarKeyFrameAnimation();
            forever.InsertKeyFrame(0, 240, easing);
            forever.InsertKeyFrame(1, 599, easing);
            forever.IterationBehavior = AnimationIterationBehavior.Forever;
            forever.Duration = TimeSpan.FromSeconds(3);

            _foreverAnimation = forever;

            ElementCompositionPreview.SetElementChildVisual(this, visual);
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
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
                _shape.StrokeBrush = BootStrapper.Current.Compositor.CreateColorBrush(brush.Color);
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
                    var linear = BootStrapper.Current.Compositor.CreateLinearEasingFunction();
                    var trimStart = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
                    var trimEnd = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();

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

                        var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
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
