//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public partial class SlidePanel : Panel
    {
        public class SlideState
        {
            private readonly UIElement _element;
            private readonly float _expectedHeight;

            private bool _collapsed;
            private int _pending;

            public SlideState(UIElement element, bool visible, float expectedHeight)
            {
                _element = element;
                _expectedHeight = expectedHeight;

                _collapsed = !visible;

                element.Visibility = visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                ElementCompositionPreview.SetIsTranslationEnabled(element, true);
            }

            public static implicit operator bool(SlideState d) => d._collapsed;

            public bool IsVisible
            {
                get => !_collapsed;
                set => ShowHide(_element, value);
            }

            public void Show()
            {
                _collapsed = false;
                _element.Visibility = Visibility.Visible;
            }

            public void Collapse()
            {
                _collapsed = true;
                _element.Visibility = Visibility.Collapsed;
            }

            public async void ShowHide(UIElement element, bool show)
            {
                if (_collapsed != show)
                {
                    return;
                }

                _collapsed = !show;
                _pending++;

                //SlidePanel.SetIsVisible(element, show);

                element.Visibility = Visibility.Visible;

                var pending = _pending;
                var height = _expectedHeight > 0 ? _expectedHeight : element.ActualSize.Y;

                if (height == 0 && element is FrameworkElement framework)
                {
                    await framework.UpdateLayoutAsync();
                }

                var visual = ElementCompositionPreview.GetElementVisual(element);
                visual.Clip ??= visual.Compositor.CreateInsetClip();

                var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    if (_collapsed && _pending == pending)
                    {
                        //visual.Clip = null;
                        //visual.Properties.InsertVector3("Translation", Vector3.Zero);

                        element.Visibility = Visibility.Collapsed;
                    }
                };

                //_chatView.UpdateMessagesHeaderPadding();

                var clip = visual.Compositor.CreateScalarKeyFrameAnimation();
                clip.InsertKeyFrame(show ? 0 : 1, height);
                clip.InsertKeyFrame(show ? 1 : 0, 0);
                clip.Duration = Constants.FastAnimation;

                var offset = visual.Compositor.CreateScalarKeyFrameAnimation();
                offset.InsertKeyFrame(show ? 0 : 1, -height);
                offset.InsertKeyFrame(show ? 1 : 0, 0);
                offset.Duration = Constants.FastAnimation;

                visual.Clip.StartAnimation("TopInset", clip);
                visual.StartAnimation("Translation.Y", offset);

                batch.End();
            }
        }

        // Children are stacked by composition rather than by ArrangeOverride (which puts every
        // one of them at the origin), so a header sliding in can push the ones below it down
        // without a layout pass per frame. Each visible child's Offset.Y follows the previous
        // visible one through an expression animation.
        //
        // All of it is one-time-per-child setup, and measure runs on every header show/hide:
        // establishing it per pass meant an animation allocation and a StartAnimation per child
        // each time, and handed the element a new transform in the middle of the pass.
        // Kept in index-parallel arrays instead of a dictionary, so the steady state is a
        // reference comparison per child and nothing is retained once a child goes away.
        private UIElement[] _children = Array.Empty<UIElement>();

        // The hand-off visual is created once per element and outlives unload/reload, so it's
        // safe to hold and saves an interop call per child per measure.
        private Visual[] _visuals = Array.Empty<Visual>();

        // Whose Offset.Y each child currently follows. null means no animation is running,
        // which is the correct state for the topmost visible child.
        private Visual[] _anchors = Array.Empty<Visual>();

        private ExpressionAnimation _anchorAnimation;

        protected override Size MeasureOverride(Size availableSize)
        {
            var count = Children.Count;

            if (_children.Length != count)
            {
                Array.Resize(ref _children, count);
                Array.Resize(ref _visuals, count);
                Array.Resize(ref _anchors, count);
            }

            var width = 0d;
            var height = 0d;

            Visual previous = null;

            for (int i = 0; i < count; i++)
            {
                var child = Children[i];
                child.Measure(availableSize);

                width = Math.Max(width, child.DesiredSize.Width);
                height += child.DesiredSize.Height;

                if (_children[i] != child)
                {
                    _children[i] = child;
                    _visuals[i] = ElementComposition.GetElementVisual(child);
                    _anchors[i] = null;

                    // Read by the expression below, on the previous child rather than this one,
                    // so every child needs it whether or not it's visible right now.
                    ElementCompositionPreview.SetIsTranslationEnabled(child, true);
                }

                if (child.Visibility != Visibility.Visible)
                {
                    continue;
                }

                var visual = _visuals[i];

                if (_anchors[i] != previous)
                {
                    if (previous == null)
                    {
                        visual.StopAnimation("Offset.Y");
                        visual.Offset = new Vector3(visual.Offset.X, 0, visual.Offset.Z);
                    }
                    else
                    {
                        // One instance reused: StartAnimation snapshots the expression and its
                        // parameters, so the reference can be repointed for the next child.
                        _anchorAnimation ??= previous.Compositor.CreateExpressionAnimation(
                            "reference.Offset.Y + (reference.Size.Y > 0 ? reference.Translation.Y : 0) + reference.Size.Y");
                        _anchorAnimation.SetReferenceParameter("reference", previous);

                        visual.StartAnimation("Offset.Y", _anchorAnimation);
                    }

                    _anchors[i] = previous;
                }

                previous = visual;
            }

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                child.Arrange(new Rect(0, 0, finalSize.Width, child.DesiredSize.Height));
            }

            return finalSize;
        }

        #region IsVisible

        // TODO: would be great to somehow use attached properties, to make this more "integrated" (as in, plug and play)
        // but currently this panel is only used to control chat header, where each component handles its current state anyway
        // plus, there are a few unusual behaviors (specifically ChatPinnedMessage collapsing on Unload) and adding all the code there
        // is quite an overkill, without considering attached properties overhead.
        // This said, it's also unclear how to conciliate various factors when using attached properties:
        // We need a backing field to store the actual state, plus a real attached property for controlling the visibility.
        // It's not clear whether or not this is a good pattern and how to properly keep them in sync.
        // Additionally, it's somehow confusing how to set the initial property value in regards of UIElement.Visibility.
        // And more, initial value set shouldn't be animated, so supposedly this should be controlled somewhere else.
        //public static bool GetIsVisible(DependencyObject obj)
        //{
        //    var state = (SlideState)obj.GetValue(SlideStateProperty);
        //    if (state == null)
        //    {
        //        obj.SetValue(SlideStateProperty, state = new SlideState(obj as UIElement, true));
        //    }

        //    return state.IsVisible;
        //}

        //public static void SetIsVisible(DependencyObject obj, bool value)
        //{
        //    var state = (SlideState)obj.GetValue(SlideStateProperty);
        //    if (state == null)
        //    {
        //        obj.SetValue(SlideStateProperty, state = new SlideState(obj as UIElement, value));
        //    }
        //    else
        //    {
        //        state.IsVisible = value;
        //    }

        //    obj.SetValue(IsVisibleProperty, value);
        //}

        //public static readonly DependencyProperty IsVisibleProperty =
        //    DependencyProperty.RegisterAttached("IsVisible", typeof(bool), typeof(UIElement), new PropertyMetadata(true, OnIsVisibleChanged));

        //public static readonly DependencyProperty SlideStateProperty =
        //    DependencyProperty.RegisterAttached("SlideState", typeof(SlideState), typeof(UIElement), new PropertyMetadata(null));

        //private static void OnIsVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        //{
        //    var child = d as UIElement;
        //    var parent = VisualTreeHelper.GetParent(child);

        //    //if (parent is SlidePanel panel && panel._states.TryGetValue(child, out SlideState state))
        //    //{
        //    //    state.ShowHide(child, (bool)e.NewValue);
        //    //}
        //}

        #endregion
    }
}
