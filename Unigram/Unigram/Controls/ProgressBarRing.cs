using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
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

        public ProgressBarRing()
        {
            DefaultStyleKey = typeof(ProgressBarRing);
        }

        protected override void OnApplyTemplate()
        {
            Visibility = Windows.UI.Xaml.Visibility.Collapsed;

            Indicator = (ProgressBarRingSlice)GetTemplateChild("Indicator");
            Rotation = (RotateTransform)GetTemplateChild("Rotation");

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

            Loaded += (s, args) =>
            {
                _foreverStoryboard.RepeatBehavior = RepeatBehavior.Forever;
                _foreverStoryboard.Begin();
            };
            Unloaded += (s, args) =>
            {
                _foreverStoryboard.RepeatBehavior = new RepeatBehavior(1);
                _foreverStoryboard.Stop();
            };

            OnValueChanged(0, Value);
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
            if (_foreverStoryboard == null)
                return;

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
            if (value != Indicator.EndAngle)
            {
                if (value > 0.0 && value < 359.0)
                {
                    Visibility = Windows.UI.Xaml.Visibility.Visible;
                }

                _angleStoryboard.SkipToFill();

                if (value > Indicator.EndAngle)
                {
                    var angleAnimation = (DoubleAnimation)_angleStoryboard.Children[0];
                    angleAnimation.To = value;
                    _angleStoryboard.Begin();
                    return;
                }

                Indicator.EndAngle = value;

                if (Value >= 1.0 || Value <= 0.0)
                {
                    Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
            }
        }
    }
}
