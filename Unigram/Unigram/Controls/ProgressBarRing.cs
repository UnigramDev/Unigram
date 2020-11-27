using System;
using System.Numerics;
using Unigram.Common;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Unigram.Controls
{
    public class ProgressBarRing : ProgressBar
    {
        private ProgressBarRingSlice Indicator;
        private RotateTransform Rotation;

        private Storyboard _foreverStoryboard;
        private Storyboard _angleStoryboard;

        private ShapeVisual _visual;
        private CompositionSpriteShape _shape;
        private CompositionGeometry _ellipse;

        private bool _spinning;
        private ScalarKeyFrameAnimation _foreverAnimation;

        public ProgressBarRing()
        {
            DefaultStyleKey = typeof(ProgressBarRing);
        }

        public double Radius { get; set; } = 21;
        public double Center { get; set; } = 24;

        public bool Spin { get; set; } = true;

        protected override void OnApplyTemplate()
        {
            if (ApiInfo.CanUseDirectComposition)
            {
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

                var trimEnd = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                trimEnd.Target = nameof(CompositionGeometry.TrimEnd);
                trimEnd.InsertExpressionKeyFrame(1.0f, "this.FinalValue", Window.Current.Compositor.CreateLinearEasingFunction());

                var visibility = Window.Current.Compositor.CreateExpressionAnimation("target.TrimEnd > 0 && target.TrimEnd < 1");
                visibility.SetReferenceParameter("target", ellipse);

                var animations = Window.Current.Compositor.CreateImplicitAnimationCollection();
                animations[nameof(CompositionGeometry.TrimEnd)] = trimEnd;

                ellipse.ImplicitAnimations = animations;
                visual.StartAnimation("IsVisible", visibility);
                //visual.StartAnimation("RotationAngleInDegrees", forever);

                _visual = visual;
                _shape = shape;
                _ellipse = ellipse;

                var easing = Window.Current.Compositor.CreateLinearEasingFunction();
                var forever = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                forever.InsertKeyFrame(1, 360, easing);
                forever.IterationBehavior = AnimationIterationBehavior.Forever;
                forever.Duration = TimeSpan.FromSeconds(3);

                _foreverAnimation = forever;

                ElementCompositionPreview.SetElementChildVisual(this, visual);
                RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
            }
            else
            {
                Indicator = (ProgressBarRingSlice)GetTemplateChild("Indicator");
                Rotation = (RotateTransform)GetTemplateChild("Rotation");

                Visibility = Visibility.Collapsed;

                if (Rotation != null)
                {
                    _foreverStoryboard = new Storyboard();
                    _foreverStoryboard.RepeatBehavior = RepeatBehavior.Forever;
                    var rotationAnimation = new DoubleAnimation();
                    rotationAnimation.From = 0;
                    rotationAnimation.To = 360;
                    rotationAnimation.Duration = TimeSpan.FromSeconds(3.0);

                    Storyboard.SetTarget(rotationAnimation, Rotation);
                    Storyboard.SetTargetProperty(rotationAnimation, "(RotateTransform.Angle)");

                    _foreverStoryboard.Children.Add(rotationAnimation);

                    _angleStoryboard = new Storyboard();
                    var angleAnimation = new DoubleAnimation();
                    angleAnimation.Duration = TimeSpan.FromSeconds(0.25);
                    angleAnimation.EnableDependentAnimation = true;

                    Storyboard.SetTarget(angleAnimation, Indicator);
                    Storyboard.SetTargetProperty(angleAnimation, "EndAngle");

                    _angleStoryboard.Children.Add(angleAnimation);
                }
                else
                {
                    Indicator.EndAngle = 359;
                }

                OnValueChanged(0, Value);
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
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

            if (_foreverStoryboard != null)
            {
                _foreverStoryboard.RepeatBehavior = RepeatBehavior.Forever;
                _foreverStoryboard.Begin();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_foreverAnimation != null)
            {
                _visual.StopAnimation("RotationAngleInDegrees");
            }

            if (_foreverStoryboard != null)
            {
                _foreverStoryboard.RepeatBehavior = new RepeatBehavior(1);
                _foreverStoryboard.Stop();
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

            if (_ellipse != null)
            {
                if (_foreverAnimation != null)
                {
                    if (/*newValue > 0 &&*/ newValue < 1)
                    {
                        if (!_spinning)
                        {
                            _spinning = true;
                            _visual.RotationAngleInDegrees = 0;
                            _visual.StartAnimation("RotationAngleInDegrees", _foreverAnimation);
                        }
                    }
                    else if (_spinning)
                    {
                        _spinning = false;
                        _visual.StopAnimation("RotationAngleInDegrees");
                    }
                }

                _ellipse.TrimEnd = MathF.Max(0, MathF.Min(1, (float)newValue));

                if (newValue >= 1.0 || newValue <= 0.0)
                {
                    Visibility = Visibility.Collapsed;
                }
                else
                {
                    Visibility = Visibility.Visible;
                }
            }
            else if (Indicator != null)
            {
                double value;
                //if (oldValue > 0.0 && oldValue < 1.0 && newValue == 0.0)
                //{
                //    value = 359.0;
                //}
                //else
                {
                    value = newValue * 359.0;
                    if (value < 0.0)
                    {
                        value = 0.0;
                    }
                    else if (value > 359.0)
                    {
                        value = 359.0;
                    }
                }
                if (value != (Rotation == null ? Indicator.StartAngle : Indicator.EndAngle))
                {
                    if (value > 0.0 && value < 359.0)
                    {
                        Visibility = Windows.UI.Xaml.Visibility.Visible;
                    }

                    //_angleStoryboard.SkipToFill();

                    //if (value > Indicator.EndAngle)
                    //{
                    //    var angleAnimation = (DoubleAnimation)_angleStoryboard.Children[0];
                    //    angleAnimation.To = value;
                    //    _angleStoryboard.Begin();
                    //    return;
                    //}

                    if (Rotation == null)
                    {
                        Indicator.StartAngle = 360 - value;
                    }
                    else
                    {
                        Indicator.EndAngle = value;
                    }

                    if (Value >= 1.0 || Value <= 0.0)
                    {
                        Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    }
                }
            }
        }
    }
}
