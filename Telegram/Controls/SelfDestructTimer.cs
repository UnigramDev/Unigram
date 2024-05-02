//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public class SelfDestructTimer : Control
    {
        private readonly ShapeVisual _visual;
        private readonly CompositionSpriteShape _shape;
        private readonly CompositionEllipseGeometry _ellipse;

        public double Radius { get; set; } = 21;
        public double Center { get; set; } = 24;

        public SelfDestructTimer()
        {
            DefaultStyleKey = typeof(SelfDestructTimer);
            Visibility = Visibility.Collapsed;

            var ellipse = Window.Current.Compositor.CreateEllipseGeometry();
            ellipse.Radius = new Vector2((float)Radius);
            ellipse.Center = new Vector2((float)Center);

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

            _visual = visual;
            _shape = shape;
            _ellipse = ellipse;

            ElementCompositionPreview.SetElementChildVisual(this, visual);
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
        }

        protected override void OnApplyTemplate()
        {
            _ellipse.Radius = new Vector2((float)Radius);
            _ellipse.Center = new Vector2((float)Center);

            _shape.CenterPoint = new Vector2((float)Center);

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

        public void Fill()
        {
            _ellipse.StopAnimation("TrimStart");
            _ellipse.TrimStart = 0;

            Visibility = Visibility.Visible;
        }

        #region Value

        public DateTime? Value
        {
            get => (DateTime?)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(DateTime?), typeof(SelfDestructTimer), new PropertyMetadata(null, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SelfDestructTimer)d).OnValueChanged(e.NewValue as DateTime?);
        }

        #endregion

        #region Maximum

        public int? Maximum
        {
            get => (int?)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(int?), typeof(SelfDestructTimer), new PropertyMetadata(null));

        #endregion

        private void OnValueChanged(DateTime? newValue)
        {
            if (_ellipse == null)
            {
                return;
            }

            if (newValue == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }
            else
            {
                Visibility = Visibility.Visible;
            }

            var value = newValue.Value;
            var difference = value - DateTime.Now;

            var seconds = (float)difference.TotalSeconds;

            var easing = Window.Current.Compositor.CreateLinearEasingFunction();
            var angleAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            angleAnimation.InsertKeyFrame(0, 1f - (seconds / (Maximum ?? 0)));
            angleAnimation.InsertKeyFrame(1, 1f, easing);
            angleAnimation.Duration = difference;

            _ellipse.StartAnimation("TrimStart", angleAnimation);

            //double value;
            ////if (oldValue > 0.0 && oldValue < 1.0 && newValue == 0.0)
            ////{
            ////    value = 359.0;
            ////}
            ////else
            //{
            //    value = newValue * 359.0;
            //    if (value < 0.0)
            //    {
            //        value = 0.0;
            //    }
            //    else if (value > 359.0)
            //    {
            //        value = 359.0;
            //    }
            //}
            //if (value != Indicator.StartAngle)
            //{
            //    if (value > 0.0 && value < 359.0)
            //    {
            //        Visibility = Windows.UI.Xaml.Visibility.Visible;
            //    }

            //    if (value > Indicator.StartAngle)
            //    {
            //        _angleStoryboard.SkipToFill();

            //        var angleAnimation = (DoubleAnimation)_angleStoryboard.Children[0];
            //        angleAnimation.To = value;
            //        angleAnimation.Duration = DueTime - DateTime.Now;
            //        _angleStoryboard.Begin();
            //    }
            //}
        }
    }
}
