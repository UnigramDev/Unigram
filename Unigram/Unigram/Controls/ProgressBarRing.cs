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

        private CompositionGeometry _ellipse;

        public ProgressBarRing()
        {
            DefaultStyleKey = typeof(ProgressBarRing);
        }

        protected override void OnApplyTemplate()
        {
            Indicator = (ProgressBarRingSlice)GetTemplateChild("Indicator");
            Rotation = (RotateTransform)GetTemplateChild("Rotation");

            if (Indicator != null)
            {
                OnApplyLegacyTemplate();
            }
            else if (ApiInfo.CanUseDirectComposition)
            {
                var ellipse = Window.Current.Compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(21);
                ellipse.Center = new Vector2(24);
                ellipse.TrimEnd = 0f;

                var shape = Window.Current.Compositor.CreateSpriteShape(ellipse);
                shape.CenterPoint = new Vector2(24);
                shape.StrokeThickness = 2;
                shape.StrokeBrush = Window.Current.Compositor.CreateColorBrush(Windows.UI.Colors.Red);
                shape.StrokeStartCap = CompositionStrokeCap.Round;
                shape.StrokeEndCap = CompositionStrokeCap.Round;

                var visual = Window.Current.Compositor.CreateShapeVisual();
                visual.Shapes.Add(shape);
                visual.Size = new Vector2(48);
                visual.CenterPoint = new Vector3(24);

                var easing = Window.Current.Compositor.CreateLinearEasingFunction();
                var forever = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                forever.InsertKeyFrame(1, 360, easing);
                forever.IterationBehavior = AnimationIterationBehavior.Forever;
                forever.Duration = TimeSpan.FromSeconds(3);

                var trimEnd = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                trimEnd.Target = nameof(CompositionGeometry.TrimEnd);
                trimEnd.InsertExpressionKeyFrame(1.0f, "this.FinalValue", Window.Current.Compositor.CreateLinearEasingFunction());

                var visibility = Window.Current.Compositor.CreateExpressionAnimation("target.TrimEnd < 1");
                visibility.SetReferenceParameter("target", ellipse);

                var animations = Window.Current.Compositor.CreateImplicitAnimationCollection();
                animations[nameof(CompositionGeometry.TrimEnd)] = trimEnd;

                ellipse.ImplicitAnimations = animations;
                visual.StartAnimation("IsVisible", visibility);
                visual.StartAnimation("RotationAngleInDegrees", forever);

                _ellipse = ellipse;

                ElementCompositionPreview.SetElementChildVisual(this, visual);
            }
        }

        private void OnApplyLegacyTemplate()
        {
            Visibility = Windows.UI.Xaml.Visibility.Collapsed;

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
                _foreverStoryboard.Completed += OnForeverStoryboardCompleted;

                _angleStoryboard = new Storyboard();
                var angleAnimation = new DoubleAnimation();
                angleAnimation.Duration = TimeSpan.FromSeconds(0.25);
                angleAnimation.EnableDependentAnimation = true;

                Storyboard.SetTarget(angleAnimation, Indicator);
                Storyboard.SetTargetProperty(angleAnimation, "EndAngle");

                _angleStoryboard.Children.Add(angleAnimation);
                _angleStoryboard.Completed += OnAngleStoryboardCompleted;

                Loaded += OnLoaded;
                Unloaded += OnUnloaded;
            }
            else
            {
                Indicator.EndAngle = 359;
            }

            OnValueChanged(0, Value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _foreverStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            _foreverStoryboard.Begin();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _foreverStoryboard.RepeatBehavior = new RepeatBehavior(1);
            _foreverStoryboard.Stop();
        }

        private void OnForeverStoryboardCompleted(object sender, object e)
        {
        }

        private void OnAngleStoryboardCompleted(object sender, object e)
        {
            if (Value >= 1.0 || Value <= 0)
            {
                Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        protected override void OnValueChanged(double oldValue, double newValue)
        {
            if (_ellipse != null)
            {
                _ellipse.TrimEnd = (float)newValue;
                return;
            }

            //if (_foreverStoryboard == null)
            //    return;

            if (Indicator == null)
            {
                return;
            }

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
