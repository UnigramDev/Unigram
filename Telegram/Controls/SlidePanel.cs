//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
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

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = 0d;
            var height = 0d;

            UIElement previous = null;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                child.Measure(availableSize);

                width = Math.Max(width, child.DesiredSize.Width);
                height += child.DesiredSize.Height;

                ElementCompositionPreview.SetIsTranslationEnabled(child, true);

                if (child.Visibility == Visibility.Visible)
                {
                    if (previous != null)
                    {
                        var prev = ElementComposition.GetElementVisual(previous);
                        var next = ElementComposition.GetElementVisual(child);

                        var animation = prev.Compositor.CreateExpressionAnimation("reference.Offset.Y + (reference.Size.Y > 0 ? reference.Translation.Y : 0) + reference.Size.Y");
                        animation.SetReferenceParameter("reference", prev);
                        //animation.SetScalarParameter("padding", (float)Padding.Top);

                        next.StartAnimation("Offset.Y", animation);
                    }

                    previous = child;
                }

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
