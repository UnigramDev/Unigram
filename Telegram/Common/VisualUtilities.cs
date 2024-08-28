//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Navigation;

namespace Telegram.Common
{
    public partial class VisualUtilities
    {
        public static SpriteVisual DropShadow(UIElement element, float radius = 20, float opacity = 0.25f, UIElement target = null)
        {
            var compositor = BootStrapper.Current.Compositor;

            var shadow = compositor.CreateDropShadow();
            shadow.BlurRadius = radius;
            shadow.Opacity = opacity;
            shadow.Color = Colors.Black;

            var visual = compositor.CreateSpriteVisual();
            visual.Shadow = shadow;
            visual.Size = new Vector2(0, 0);
            visual.Offset = new Vector3(0, 0, 0);
            visual.RelativeSizeAdjustment = Vector2.One;

            switch (element)
            {
                case Image image:
                    shadow.Mask = image.GetAlphaMask();
                    break;
                case Shape shape:
                    shadow.Mask = shape.GetAlphaMask();
                    break;
                case TextBlock textBlock:
                    shadow.Mask = textBlock.GetAlphaMask();
                    break;
            }

            ElementCompositionPreview.SetElementChildVisual(target ?? element, visual);
            return visual;
        }

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

            var compositor = visual.Compositor;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Opacity = newValue ? 1 : 0;
                visual.Scale = new Vector3(scale ? newValue ? 1 : 0 : 1);

                sender.Visibility = newValue ? Visibility.Visible : Visibility.Collapsed;
            };

            var anim1 = compositor.CreateScalarKeyFrameAnimation();
            anim1.InsertKeyFrame(0, newValue ? 0 : 1);
            anim1.InsertKeyFrame(1, newValue ? 1 : 0);
            visual.StartAnimation("Opacity", anim1);

            if (scale)
            {
                var anim2 = compositor.CreateVector3KeyFrameAnimation();
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
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= handler;

                if (weak.Target is Action callback)
                {
                    callback();
                }
            }

            try
            {
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += handler;
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
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering -= handler;
                tsc.SetResult(true);
            }

            try
            {
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendering += handler;
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
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendered -= handler;

                if (weak.Target is Action callback)
                {
                    callback();
                }
            }

            try
            {
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendered += handler;
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
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendered -= handler;
                tsc.SetResult(true);
            }

            try
            {
                Microsoft.UI.Xaml.Media.CompositionTarget.Rendered += handler;
            }
            catch
            {
                // Bla bla
            }

            return tsc.Task;
        }
    }
}
