//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Navigation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    /// <summary>
    /// How a media editor's rows of tools change with the tab: the old row slides away from the
    /// side the new tab is on and fades, while the new row slides in from it. A direction is the
    /// sign of the move between tabs, 1 to the right; 0 only fades.
    /// </summary>
    public static class MediaEditorTransitions
    {
        private const float Distance = 24;

        private static readonly TimeSpan OutDuration = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(160);
        private static readonly TimeSpan InDuration = TimeSpan.FromMilliseconds(250);

        // Bumped by each transition an element starts, so that one cut short does not finish after
        // the next one began, collapsing a row that is coming back.
        private static readonly DependencyProperty VersionProperty =
            DependencyProperty.RegisterAttached("Version", typeof(int), typeof(MediaEditorTransitions), new PropertyMetadata(0));

        public static void SlideIn(UIElement element, int direction)
        {
            Bump(element);

            element.Visibility = Visibility.Visible;

            var visual = Prepare(element);
            var compositor = visual.Compositor;
            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1));

            var translation = compositor.CreateVector3KeyFrameAnimation();
            translation.InsertKeyFrame(0, new Vector3(Distance * direction, 0, 0));
            translation.InsertKeyFrame(1, Vector3.Zero, easing);
            translation.Duration = InDuration;

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 1, easing);
            opacity.Duration = InDuration;

            visual.StartAnimation("Translation", translation);
            visual.StartAnimation("Opacity", opacity);
        }

        /// <summary>
        /// Slides the element away, then collapses it.
        /// </summary>
        public static void SlideOut(UIElement element, int direction)
        {
            if (element.Visibility == Visibility.Collapsed)
            {
                return;
            }

            var version = Bump(element);
            var visual = Prepare(element);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += new Collapse(element, version).OnCompleted;

            Leave(visual, "Translation", Vector3.Zero, direction);

            batch.End();
        }

        /// <summary>
        /// Changes what one element shows as two rows passing each other, as when two elements trade
        /// places: a frozen picture of it slides out over <paramref name="host"/>, whose child visual
        /// it takes for the while, as the element, changed by <paramref name="change"/>, slides in.
        /// </summary>
        public static async void Swap(UIElement element, UIElement host, int direction, Action change)
        {
            var version = (int)element.GetValue(VersionProperty);

            SpriteVisual snapshot = null;

            if (element.Visibility == Visibility.Visible && element.ActualSize.X > 0)
            {
                var offset = element.TransformToVisual(host).TransformVector2();

                snapshot = BootStrapper.Current.Compositor.CreateRedirectVisual(element, Vector2.Zero, element.ActualSize, true);
                snapshot.Offset = new Vector3(offset, 0);

                // The picture is only taken on the next frame: changed before then, it would be of
                // the new content.
                await VisualUtilities.WaitForCompositionRenderedAsync();

                ElementComposition.SetElementChildVisual(host, snapshot);
            }

            change();

            // Not if something else moved the element meanwhile, the crop tab sliding it away.
            if (element.Visibility == Visibility.Visible && (int)element.GetValue(VersionProperty) == version)
            {
                SlideIn(element, direction);
            }

            if (snapshot != null)
            {
                var batch = snapshot.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += new Release(host, snapshot).OnCompleted;

                Leave(snapshot, "Offset", snapshot.Offset, direction);

                batch.End();
            }
        }

        // Accelerating away, faded linearly and before the slide ends: an accelerating fade keeps
        // the row opaque until the last frames, which reads as it vanishing at the end of its slide.
        private static void Leave(Visual visual, string property, Vector3 from, int direction)
        {
            var compositor = visual.Compositor;

            var translation = compositor.CreateVector3KeyFrameAnimation();
            translation.InsertKeyFrame(1, from + new Vector3(-Distance * direction, 0, 0), compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(1, 1)));
            translation.Duration = OutDuration;

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(1, 0, compositor.CreateLinearEasingFunction());
            opacity.Duration = FadeOutDuration;

            visual.StartAnimation(property, translation);
            visual.StartAnimation("Opacity", opacity);
        }

        private static Visual Prepare(UIElement element)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(element, true);
            return ElementComposition.GetElementVisual(element);
        }

        private static int Bump(UIElement element)
        {
            var version = (int)element.GetValue(VersionProperty) + 1;
            element.SetValue(VersionProperty, version);

            return version;
        }

        private sealed class Collapse
        {
            private readonly UIElement _element;
            private readonly int _version;

            public Collapse(UIElement element, int version)
            {
                _element = element;
                _version = version;
            }

            public void OnCompleted(object sender, CompositionBatchCompletedEventArgs args)
            {
                ((CompositionScopedBatch)sender).Completed -= OnCompleted;

                if ((int)_element.GetValue(VersionProperty) == _version)
                {
                    _element.Visibility = Visibility.Collapsed;
                }
            }
        }

        private sealed class Release
        {
            private readonly UIElement _host;
            private readonly SpriteVisual _snapshot;

            public Release(UIElement host, SpriteVisual snapshot)
            {
                _host = host;
                _snapshot = snapshot;
            }

            public void OnCompleted(object sender, CompositionBatchCompletedEventArgs args)
            {
                ((CompositionScopedBatch)sender).Completed -= OnCompleted;

                // A swap that started meanwhile has put its own picture there.
                if (ElementCompositionPreview.GetElementChildVisual(_host) == _snapshot)
                {
                    ElementComposition.SetElementChildVisual(_host, null);
                }

                _snapshot.Brush?.Dispose();
                _snapshot.Dispose();
            }
        }
    }
}
