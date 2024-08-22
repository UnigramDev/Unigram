//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Telegram.Common
{
    public class VisualUtilities
    {
        public static void ShakeView(FrameworkElement view, float x = 2)
        {
            // We use first child inside the control (usually a Grid)
            // so we don't have to worry about absolute offset
            var inner = VisualTreeHelper.GetChild(view, 0) as FrameworkElement;
            if (inner == null)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(inner);

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
            var visual = ElementComposition.GetElementVisual(sender);

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


        static class DelegateKeeper
        {
            private static ConditionalWeakTable<object, List<Delegate>> cwt = new();
            public static void KeepAlive(Delegate d) => cwt.GetOrCreateValue(d?.Target ?? throw new ArgumentNullException(nameof(d))).Add(d);
        }

        public static void QueueCallbackForCompositionRendering(Action callback)
        {
            DelegateKeeper.KeepAlive(callback);

            var weak = new WeakReference(callback);
            void handler(object sender, object e)
            {
                CompositionTarget.Rendering -= handler;

                if (weak.Target is Action callback)
                {
                    callback();
                }
            }

            try
            {
                CompositionTarget.Rendering += handler;
            }
            catch
            {
                // Bla bla
            }
        }

        public static Task WaitForCompositionRenderingAsync()
        {
            var tsc = new TaskCompletionSource<bool>();
            void handler(object sender, object e)
            {
                CompositionTarget.Rendering -= handler;
                tsc.SetResult(true);
            }

            try
            {
                CompositionTarget.Rendering += handler;
            }
            catch
            {
                // Bla bla
            }

            return tsc.Task;
        }

        public static void QueueCallbackForCompositionRendered(Action callback)
        {
            DelegateKeeper.KeepAlive(callback);

            var weak = new WeakReference(callback);
            void handler(object sender, object e)
            {
                CompositionTarget.Rendered -= handler;

                if (weak.Target is Action callback)
                {
                    callback();
                }
            }

            try
            {
                CompositionTarget.Rendered += handler;
            }
            catch
            {
                // Bla bla
            }
        }

        public static Task WaitForCompositionRenderedAsync()
        {
            var tsc = new TaskCompletionSource<bool>();
            void handler(object sender, object e)
            {
                CompositionTarget.Rendered -= handler;
                tsc.SetResult(true);
            }

            try
            {
                CompositionTarget.Rendered += handler;
            }
            catch
            {
                // Bla bla
            }

            return tsc.Task;
        }
    }
}
