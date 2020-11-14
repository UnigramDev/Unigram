using System;
using System.Numerics;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class VisualUtilities
    {
        public static void ShakeView(FrameworkElement view, float x = 2)
        {
            // We use first child inside the control (usually a Grid)
            // so we don't have to worry about absolute offset
            var inner = VisualTreeHelper.GetChild(view, 0) as FrameworkElement;
            var visual = ElementCompositionPreview.GetElementVisual(inner);

            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            animation.Duration = TimeSpan.FromMilliseconds(50 * 6);

            for (int i = 1; i < 6; i++)
            {
                x = -x;
                animation.InsertKeyFrame(i * (1f / 5f), i == 5 ? 0 : x);
            }

            animation.InsertKeyFrame(0, 0);
            animation.InsertKeyFrame(1, 0);

            visual.StartAnimation("Offset.X", animation);
        }

        #region IsVisible

        public static bool GetIsVisible(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsVisibleProperty);
        }

        public static void SetIsVisible(DependencyObject obj, bool value)
        {
            obj.SetValue(IsVisibleProperty, value);
        }

        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.RegisterAttached("IsVisible", typeof(bool), typeof(UIElement), new PropertyMetadata(true, OnVisibleChanged));

        private static void OnVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as UIElement;
            var newValue = (bool)e.NewValue;
            var oldValue = (bool)e.OldValue;

            if (newValue == oldValue || (sender.Visibility == Visibility.Collapsed && !newValue))
            {
                return;
            }

            var scale = GetIsScaleEnabled(d);
            var visual = ElementCompositionPreview.GetElementVisual(sender);

            sender.Visibility = Visibility.Visible;

            var batch = Window.Current.Compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Opacity = newValue ? 1 : 0;
                visual.Scale = new Vector3(scale ? newValue ? 1 : 0 : 1);

                sender.Visibility = newValue ? Visibility.Visible : Visibility.Collapsed;
            };

            var anim1 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim1.InsertKeyFrame(0, newValue ? 0 : 1);
            anim1.InsertKeyFrame(1, newValue ? 1 : 0);
            visual.StartAnimation("Opacity", anim1);

            if (scale)
            {
                var anim2 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                anim2.InsertKeyFrame(0, new Vector3(newValue ? 0 : 1));
                anim2.InsertKeyFrame(1, new Vector3(newValue ? 1 : 0));
                visual.StartAnimation("Scale", anim2);
            }

            batch.End();
        }

        #endregion

        #region IsScaleEnabled

        public static bool GetIsScaleEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsScaleEnabledProperty);
        }

        public static void SetIsScaleEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsScaleEnabledProperty, value);
        }

        public static readonly DependencyProperty IsScaleEnabledProperty =
            DependencyProperty.RegisterAttached("IsScaleEnabled", typeof(bool), typeof(UIElement), new PropertyMetadata(true));

        #endregion
    }
}
