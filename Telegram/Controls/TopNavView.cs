//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Windows.Foundation;
using Windows.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace Telegram.Controls
{
    public class TopNavView : ListViewEx
    {
        private readonly Vector2 c_frame1point1 = new(0.9f, 0.1f);
        private readonly Vector2 c_frame1point2 = new(1.0f, 0.2f);
        private readonly Vector2 c_frame2point1 = new(0.1f, 0.9f);
        private readonly Vector2 c_frame2point2 = new(0.2f, 1.0f);

        private UIElement _prevIndicator;
        private UIElement _nextIndicator;

        private UIElement _activeIndicator;

        private bool _needsSelectionUpdate;

        public TopNavView()
        {
            DefaultStyleKey = typeof(TopNavView);

            Loaded += OnLoaded;
            SelectionChanged += OnSelectionChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _activeIndicator = null;
            AnimateSelectionChanged(SelectedItem);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_needsSelectionUpdate)
            {
                return;
            }

            _needsSelectionUpdate = true;
            VisualUtilities.QueueCallbackForCompositionRendering(UpdateSelection);
        }

        private void UpdateSelection()
        {
            _needsSelectionUpdate = false;

            if (SelectionMode == ListViewSelectionMode.Single)
            {
                AnimateSelectionChanged(SelectedItem);

                if (FocusFollowsSingleSelection)
                {
                    // TODO: would be cool to do this only on programmatic changes, but I'm afraid it's not possible.
                    _ = this.ScrollToItem2(SelectedItem, VerticalAlignment.Center);
                }
            }
            else
            {
                AnimateSelectionChanged(null);
            }
        }

        protected override DependencyObject GetContainerForItemOverride()
        {
            var container = new TopNavViewItem();
            container.ContextRequested += ItemContextRequested;

            return container;
        }

        public event TypedEventHandler<UIElement, ContextRequestedEventArgs> ItemContextRequested;

        #region FocusFollowsSingleSelection

        public bool FocusFollowsSingleSelection
        {
            get { return (bool)GetValue(FocusFollowsSingleSelectionProperty); }
            set { SetValue(FocusFollowsSingleSelectionProperty, value); }
        }

        public static readonly DependencyProperty FocusFollowsSingleSelectionProperty =
            DependencyProperty.Register("FocusFollowsSingleSelection", typeof(bool), typeof(TopNavView), new PropertyMetadata(false));

        #endregion

        #region Orientation

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(Orientation), typeof(TopNavView), new PropertyMetadata(Orientation.Horizontal));

        #endregion

        private void AnimateSelectionChanged(object nextItem, bool retry = true)
        {
            var prevIndicator = _activeIndicator;
            var nextIndicator = FindSelectionIndicator(nextItem, retry);

            bool haveValidAnimation = false;
            // It's possible that AnimateSelectionChanged is called multiple times before the first animation is complete.
            // To have better user experience, if the selected target is the same, keep the first animation
            // If the selected target is not the same, abort the first animation and launch another animation.
            if (_prevIndicator != null || _nextIndicator != null) // There is ongoing animation
            {
                if (nextIndicator != null && _nextIndicator == nextIndicator) // animate to the same target, just wait for animation complete
                {
                    if (prevIndicator != null && prevIndicator != _prevIndicator)
                    {
                        ResetElementAnimationProperties(prevIndicator, 0.0f);
                    }

                    haveValidAnimation = true;
                }
                else
                {
                    // If the last animation is still playing, force it to complete.
                    OnAnimationCompleted(null, null);
                }
            }

            if (!haveValidAnimation)
            {
                UIElement paneContentGrid = this;

                if ((prevIndicator != nextIndicator) && paneContentGrid != null && prevIndicator != null && nextIndicator != null /*&& SharedHelpers::IsAnimationsEnabled()*/)
                {
                    // Make sure both indicators are visible and in their original locations
                    ResetElementAnimationProperties(prevIndicator, 1.0f);
                    ResetElementAnimationProperties(nextIndicator, 1.0f);

                    // get the item positions in the pane
                    float prevPos;
                    float nextPos;

                    var prevPosPoint = prevIndicator.TransformToVisual(this).TransformPoint(new Point()).ToVector2();
                    var nextPosPoint = nextIndicator.TransformToVisual(this).TransformPoint(new Point()).ToVector2();
                    var prevSize = prevIndicator.RenderSize.ToVector2();
                    var nextSize = nextIndicator.RenderSize.ToVector2();

                    if (Orientation == Orientation.Horizontal)
                    {
                        prevPos = prevPosPoint.X;
                        nextPos = nextPosPoint.X;
                    }
                    else
                    {
                        prevPos = prevPosPoint.Y;
                        nextPos = nextPosPoint.Y;
                    }

                    float outgoingEndPosition = (float)(nextPos - prevPos);
                    float incomingStartPosition = (float)(prevPos - nextPos);

                    var scopedBatch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                    scopedBatch.Completed += OnAnimationCompleted;

                    // Play the animation on both the previous and next indicators
                    PlayIndicatorAnimations(prevIndicator,
                        0,
                        outgoingEndPosition,
                        prevSize,
                        nextSize,
                        true);
                    PlayIndicatorAnimations(nextIndicator,
                        incomingStartPosition,
                        0,
                        prevSize,
                        nextSize,
                        false);

                    scopedBatch.End();

                    _prevIndicator = prevIndicator;
                    _nextIndicator = nextIndicator;
                }
                else
                {
                    // if all else fails, or if animations are turned off, attempt to correctly set the positions and opacities of the indicators.
                    ResetElementAnimationProperties(prevIndicator, 0.0f);
                    ResetElementAnimationProperties(nextIndicator, 1.0f);
                }

                _activeIndicator = nextIndicator;
            }
        }

        private UIElement FindSelectionIndicator(object item, bool retry)
        {
            var container = (item is TopNavViewItem ? item : ContainerFromItem(item)) as TopNavViewItem;
            if (container == null)
            {
                return null;
            }

            var indicator = container.GetSelectionIndicator();
            if (indicator == null && retry)
            {
                void handler(object sender, RoutedEventArgs e)
                {
                    container.Loaded -= handler;
                    AnimateSelectionChanged(SelectedItem, false);
                }

                container.Loaded += handler;
                return null;
            }

            return indicator;
        }

        private void PlayIndicatorAnimations(UIElement indicator, float from, float to, Vector2 beginSize, Vector2 endSize, bool isOutgoing)
        {
            Visual visual = ElementComposition.GetElementVisual(indicator);
            Compositor comp = visual.Compositor;

            Vector2 size = indicator.RenderSize.ToVector2();
            float dimension = Orientation == Orientation.Horizontal ? size.X : size.Y;

            float beginScale = 1.0f;
            float endScale = 1.0f;
            if (Orientation == Orientation.Horizontal && MathF.Abs(size.X) > 0.001f)
            {
                beginScale = beginSize.X / size.X;
                endScale = endSize.X / size.X;
            }

            StepEasingFunction singleStep = comp.CreateStepEasingFunction();
            singleStep.IsFinalStepSingleFrame = true;

            if (isOutgoing)
            {
                // fade the outgoing indicator so it looks nice when animating over the scroll area
                ScalarKeyFrameAnimation opacityAnim = comp.CreateScalarKeyFrameAnimation();
                opacityAnim.InsertKeyFrame(0.0f, 1.0f);
                opacityAnim.InsertKeyFrame(0.333f, 1.0f, singleStep);
                opacityAnim.InsertKeyFrame(1.0f, 0.0f, comp.CreateCubicBezierEasingFunction(c_frame2point1, c_frame2point2));
                opacityAnim.Duration = TimeSpan.FromMilliseconds(300);

                visual.StartAnimation("Opacity", opacityAnim);
            }
            else
            {
                visual.Opacity = 1;
            }

            ScalarKeyFrameAnimation posAnim = comp.CreateScalarKeyFrameAnimation();
            posAnim.InsertKeyFrame(0.0f, from < to ? from : (from + (dimension * (beginScale - 1))));
            posAnim.InsertKeyFrame(0.333f, from < to ? (to + (dimension * (endScale - 1))) : to, singleStep);
            posAnim.Duration = TimeSpan.FromMilliseconds(300);

            ScalarKeyFrameAnimation scaleAnim = comp.CreateScalarKeyFrameAnimation();
            scaleAnim.InsertKeyFrame(0.0f, beginScale);
            scaleAnim.InsertKeyFrame(0.333f, MathF.Abs(to - from) / dimension + (from < to ? endScale : beginScale), comp.CreateCubicBezierEasingFunction(c_frame1point1, c_frame1point2));
            scaleAnim.InsertKeyFrame(1.0f, endScale, comp.CreateCubicBezierEasingFunction(c_frame2point1, c_frame2point2));
            scaleAnim.Duration = TimeSpan.FromMilliseconds(300);

            ScalarKeyFrameAnimation centerAnim = comp.CreateScalarKeyFrameAnimation();
            centerAnim.InsertKeyFrame(0.0f, from < to ? 0.0f : dimension);
            centerAnim.InsertKeyFrame(1.0f, from < to ? dimension : 0.0f, singleStep);
            centerAnim.Duration = TimeSpan.FromMilliseconds(100);

            if (Orientation == Orientation.Horizontal)
            {
                visual.StartAnimation("Offset.X", posAnim);
                visual.StartAnimation("Scale.X", scaleAnim);
                visual.StartAnimation("CenterPoint.X", centerAnim);
            }
            else
            {
                visual.StartAnimation("Offset.Y", posAnim);
                visual.StartAnimation("Scale.Y", scaleAnim);
                visual.StartAnimation("CenterPoint.Y", centerAnim);
            }
        }

        private void OnAnimationCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            ResetElementAnimationProperties(_prevIndicator, 0);
            ResetElementAnimationProperties(_nextIndicator, 1);

            _prevIndicator = null;
            _nextIndicator = null;
        }

        private void ResetElementAnimationProperties(UIElement element, float desiredOpacity)
        {
            if (element != null)
            {
                element.Opacity = desiredOpacity;
                Visual visual = ElementComposition.GetElementVisual(element);
                if (visual != null)
                {
                    visual.Offset = new Vector3(0.0f, 0.0f, 0.0f);
                    visual.Scale = new Vector3(1.0f, 1.0f, 1.0f);
                    visual.Opacity = desiredOpacity;
                }
            }
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);

            if (element is TopNavViewItem topElement)
            {
                var indicator = topElement.GetSelectionIndicator(true);
                if (indicator != null)
                {
                    if (topElement.IsSelected)
                    {
                        _activeIndicator = indicator;
                    }

                    ResetElementAnimationProperties(indicator, topElement.IsSelected ? 1 : 0);
                }
            }
        }
    }

    public class TopNavViewItem : TextListViewItem
    {
        private UIElement SelectionIndicator;

        public TopNavViewItem()
        {
            DefaultStyleKey = typeof(TopNavViewItem);
        }

        public UIElement GetSelectionIndicator(bool fromCache = false)
        {
            if (!fromCache)
            {
                SelectionIndicator ??= GetTemplateChild("SelectionIndicator") as UIElement;
            }

            return SelectionIndicator;
        }
    }

    public class TopNavViewItemManager : VisualStateManager
    {
        private readonly static string[] _allowedStates = new[]
        {
            "Normal",
            "PointerOver",
            "Pressed",
            "Selected",
            "PointerOverSelected",
            "PressedSelected",
        };

        protected override bool GoToStateCore(Control control, FrameworkElement templateRoot, string stateName, VisualStateGroup group, VisualState state, bool useTransitions)
        {
            if (control is TopNavViewItem selector && selector.ContentTemplateRoot is UserControl element)
            {
                if (_allowedStates.Contains(stateName))
                {
                    GoToState(element, stateName, useTransitions);
                }
            }

            return base.GoToStateCore(control, templateRoot, stateName, group, state, useTransitions);
        }
    }
}
