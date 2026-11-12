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
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    /// <summary>
    /// A <see cref="SlidePanel"/> child told whether it opens or closes the run of visible
    /// children that implement this interface. Children that do not implement it are skipped,
    /// so they neither count nor separate two that do.
    /// </summary>
    public interface ISlidePanelChild
    {
        bool IsFirst { get; set; }

        bool IsLast { get; set; }

        /// <summary>
        /// Whether the child is shown or on its way to being shown, which its Visibility cannot
        /// say: that stays Visible until a child sliding out has finished.
        /// </summary>
        SlidePanel.SlideState SlideState { get; }
    }

    public partial class SlidePanel : Panel
    {
        public class SlideState
        {
            private readonly UIElement _element;
            private readonly float _expectedHeight;

            private bool _collapsed;
            private float _height;

            // Hiding as the only child, the one way out another child arriving can take over.
            private bool _leavingAlone;

            // The latest animation, the only one whose end may collapse the element.
            private CompositionScopedBatch _batch;

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
                _leavingAlone = false;
                _element.Visibility = Visibility.Visible;
            }

            public void Collapse()
            {
                _collapsed = true;
                _leavingAlone = false;
                _element.Visibility = Visibility.Collapsed;
            }

            public async void ShowHide(UIElement element, bool show)
            {
                if (_collapsed != show)
                {
                    return;
                }

                // Decided when asked rather than once the layout below is done, by which time
                // another child may have started showing or hiding.
                var panel = VisualTreeHelper.GetParent(element) as SlidePanel;
                var alone = panel != null && panel.IsAlone(element);

                // Switching chats hides one header and shows another, rarely in the same call: the
                // first, alone when it went, has started fading out with the card under it.
                var replaced = show && alone ? FindLeavingAlone(panel, element) : null;

                _collapsed = !show;
                _leavingAlone = false;

                element.Visibility = Visibility.Visible;

                var height = _expectedHeight > 0 ? _expectedHeight : element.ActualSize.Y;

                if (height == 0 && element is FrameworkElement framework)
                {
                    await framework.UpdateLayoutAsync();
                }

                _height = height;

                if (replaced != null && !replaced._leavingAlone)
                {
                    replaced = null;
                }

                var visual = ElementCompositionPreview.GetElementVisual(element);
                var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                if (replaced != null)
                {
                    AnimateSlide(visual, true, height, true);
                }
                else if (alone)
                {
                    AnimateAlone(visual, show, height + (float)panel.LeadingSpacing);
                    _leavingAlone = !show;
                }
                else
                {
                    AnimateSlide(visual, show, height, false);
                }

                batch.End();
                Track(batch);

                // After this one's batch has ended, so the two are not nested.
                replaced?.GiveWay();
            }

            private static SlideState FindLeavingAlone(SlidePanel panel, UIElement element)
            {
                foreach (var child in panel.Children)
                {
                    if (child != element && child is ISlidePanelChild slide && slide.SlideState is SlideState state && state._leavingAlone)
                    {
                        return state;
                    }
                }

                return null;
            }

            // Leaves from under the child replacing it instead, as if the two had been shown
            // together, so the card resizes from one to the other rather than fading out and back.
            // Every animation runs from its current value, wherever the fade had got to, and the
            // opacity goes back up because the card follows it until the newcomer's fade catches up.
            private void GiveWay()
            {
                _leavingAlone = false;

                var visual = ElementCompositionPreview.GetElementVisual(_element);
                var compositor = visual.Compositor;

                visual.Clip ??= compositor.CreateInsetClip();

                var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                var clip = compositor.CreateScalarKeyFrameAnimation();
                clip.InsertKeyFrame(1, _height);
                clip.Duration = Constants.FastAnimation;

                var offset = compositor.CreateScalarKeyFrameAnimation();
                offset.InsertKeyFrame(1, -_height);
                offset.Duration = Constants.FastAnimation;

                var opacity = compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(1, 1);
                opacity.Duration = Constants.FastAnimation;

                visual.Clip.StartAnimation("TopInset", clip);
                visual.StartAnimation("Translation.Y", offset);
                visual.StartAnimation("Opacity", opacity);

                batch.End();
                Track(batch);
            }

            private void Track(CompositionScopedBatch batch)
            {
                _batch = batch;
                batch.Completed += OnBatchCompleted;
            }

            private void OnBatchCompleted(object sender, CompositionBatchCompletedEventArgs args)
            {
                var batch = sender as CompositionScopedBatch;
                batch.Completed -= OnBatchCompleted;

                if (batch == _batch)
                {
                    _batch = null;
                    _leavingAlone = false;

                    if (_collapsed)
                    {
                        _element.Visibility = Visibility.Collapsed;
                    }
                }
            }

            // The only child has no sibling to slide out from under, so it slides out from
            // behind whatever covers the panel, through the leading spacing that it alone holds
            // open, and fades instead of clipping because that spacing is not covered.
            private static void AnimateAlone(Visual visual, bool show, float distance)
            {
                // A slide leaves its clip behind, at full height after hiding. Reset rather than
                // removed, because the panel's card reads it.
                if (visual.Clip is InsetClip inset)
                {
                    inset.StopAnimation("TopInset");
                    inset.TopInset = 0;
                }

                var offset = visual.Compositor.CreateScalarKeyFrameAnimation();
                offset.InsertKeyFrame(show ? 0 : 1, -distance);
                offset.InsertKeyFrame(show ? 1 : 0, 0);
                offset.Duration = Constants.FastAnimation;

                var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(show ? 0 : 1, 0);
                opacity.InsertKeyFrame(show ? 1 : 0, 1);
                opacity.Duration = Constants.FastAnimation;

                visual.StartAnimation("Translation.Y", offset);
                visual.StartAnimation("Opacity", opacity);
            }

            private static void AnimateSlide(Visual visual, bool show, float height, bool fade)
            {
                if (fade)
                {
                    var fadeIn = visual.Compositor.CreateScalarKeyFrameAnimation();
                    fadeIn.InsertKeyFrame(0, 0);
                    fadeIn.InsertKeyFrame(1, 1);
                    fadeIn.Duration = Constants.FastAnimation;

                    visual.StartAnimation("Opacity", fadeIn);
                }
                else
                {
                    // A fade leaves its opacity behind, at 0 after hiding.
                    visual.StopAnimation("Opacity");
                    visual.Opacity = 1;
                }

                visual.Clip ??= visual.Compositor.CreateInsetClip();

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

        // Which spacing each child's expression was started with: 1 when the child carries the
        // leading spacing, 2 when the child it follows does. Either changes without the anchor
        // changing, when a child further up shows or hides.
        private int[] _spacings = Array.Empty<int>();

        // Where each child comes to rest, for code: its visual's Offset.Y is the origin that
        // ArrangeOverride gives it, since the stacked value lives in the expression and is never
        // written back.
        private float[] _offsets = Array.Empty<float>();

        private ExpressionAnimation _anchorAnimation;

        private ISlidePanelChild _first;
        private ISlidePanelChild _last;

        /// <summary>
        /// Space above the first visible child that implements <see cref="ISlidePanelChild"/>,
        /// so there only while one is. The only child showing or hiding slides through it, which
        /// opens and closes it along with that child. It moves the children by composition and
        /// is left out of the measured size, so whatever is sized from the panel does not cover it.
        /// </summary>
        public double LeadingSpacing { get; set; }

        // The card the children that implement ISlidePanelChild sit on: one surface rather than
        // one per child, so the backdrop is blurred once and there is no seam where two meet. Its
        // top follows the visible top of the first and its bottom the bottom of the last, and it
        // takes their opacity, so it slides, grows, shrinks and fades with them.
        //
        // Both elements belong to the panel and stay first in Children; the stacking skips them.
        // They are arranged tall enough for every position the run can slide to, the background
        // is clipped to the run, and the shadow is a sprite on its own host so that clip does not
        // cut it off.
        private readonly Border _card;
        private readonly Border _cardShadowHost;

        private readonly Visual _cardVisual;
        private readonly CompositionRoundedRectangleGeometry _cardGeometry;
        private SpriteVisual _cardShadow;
        private Thickness _cardShadowInsets;

        private ExpressionAnimation _cardOffsetAnimation;
        private ExpressionAnimation _cardSizeAnimation;
        private ExpressionAnimation _cardOpacityAnimation;
        private ExpressionAnimation _cardShadowOffsetAnimation;
        private ExpressionAnimation _cardShadowSizeAnimation;

        private Visual _cardFirst;
        private Visual _cardLast;

        // How far above the panel the card may have to reach: the tallest child, which is how far
        // the only child slides up when it hides.
        private float _cardTravel;

        public SlidePanel()
        {
            _cardShadowHost = new Border { IsHitTestVisible = false };
            _card = new Border { IsHitTestVisible = false };

            // Above whatever else the panel draws behind the children, below the children.
            Canvas.SetZIndex(_cardShadowHost, 1);
            Canvas.SetZIndex(_card, 1);

            Children.Add(_cardShadowHost);
            Children.Add(_card);

            // Clipped from the start, to nothing until a child shows: set during the first measure,
            // the clip reaches the screen a frame after the background does.
            _cardVisual = ElementComposition.GetElementVisual(_card);
            _cardGeometry = _cardVisual.Compositor.CreateRoundedRectangleGeometry();
            _cardVisual.Clip = _cardVisual.Compositor.CreateGeometricClip(_cardGeometry);

            // Its own event, so the subscription cannot outlive the panel.
            ActualThemeChanged += OnActualThemeChanged;
        }

        public Brush CardBackground
        {
            get => (Brush)GetValue(CardBackgroundProperty);
            set => SetValue(CardBackgroundProperty, value);
        }

        public static readonly DependencyProperty CardBackgroundProperty =
            DependencyProperty.Register("CardBackground", typeof(Brush), typeof(SlidePanel), new PropertyMetadata(null, OnCardBackgroundChanged));

        private static void OnCardBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SlidePanel)d)._card.Background = e.NewValue as Brush;
        }

        private double _cardCornerRadius;
        public double CardCornerRadius
        {
            get => _cardCornerRadius;
            set
            {
                _cardCornerRadius = value;
                _cardGeometry.CornerRadius = new Vector2((float)value);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var count = Children.Count;

            // Before any child is measured, so one that restyles itself for its new position
            // does it ahead of its measure rather than invalidating it.
            UpdateFirstAndLast(count);

            if (_children.Length != count)
            {
                Array.Resize(ref _children, count);
                Array.Resize(ref _visuals, count);
                Array.Resize(ref _anchors, count);
                Array.Resize(ref _spacings, count);
                Array.Resize(ref _offsets, count);
            }

            var spacing = (float)LeadingSpacing;

            var width = 0d;
            var height = 0d;

            Visual previous = null;
            var previousLeads = false;

            Visual first = null;
            Visual last = null;
            var travel = 0f;

            for (int i = 0; i < count; i++)
            {
                var child = Children[i];
                child.Measure(availableSize);

                if (child == _card || child == _cardShadowHost)
                {
                    continue;
                }

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

                    // Read by the card's expressions, which cannot follow a clip that comes and goes.
                    if (child is ISlidePanelChild)
                    {
                        _visuals[i].Clip ??= _visuals[i].Compositor.CreateInsetClip();
                    }
                }

                if (child.Visibility != Visibility.Visible)
                {
                    continue;
                }

                var visual = _visuals[i];

                var leads = _first != null && ReferenceEquals(child, _first);
                var spacings = (leads ? 1 : 0) | (previousLeads ? 2 : 0);

                if (_anchors[i] != previous || _spacings[i] != spacings)
                {
                    if (previous == null)
                    {
                        visual.StopAnimation("Offset.Y");
                        visual.Offset = new Vector3(visual.Offset.X, leads ? spacing : 0, visual.Offset.Z);
                    }
                    else
                    {
                        // One instance reused: StartAnimation snapshots the expression and its
                        // parameters, so the reference can be repointed for the next child.
                        // Translation is clamped at the reference's height plus the spacing it
                        // carries: the only child slides that far, up behind the cover, and
                        // what follows has to stop where the reference's predecessor ends.
                        _anchorAnimation ??= previous.Compositor.CreateExpressionAnimation(
                            "reference.Offset.Y + (reference.Size.Y > 0 ? Max(reference.Translation.Y, -(reference.Size.Y + referenceSpacing)) : 0) + reference.Size.Y + spacing");
                        _anchorAnimation.SetReferenceParameter("reference", previous);
                        _anchorAnimation.SetScalarParameter("referenceSpacing", previousLeads ? spacing : 0);
                        _anchorAnimation.SetScalarParameter("spacing", leads ? spacing : 0);

                        visual.StartAnimation("Offset.Y", _anchorAnimation);
                    }

                    _anchors[i] = previous;
                    _spacings[i] = spacings;
                }

                previous = visual;
                previousLeads = leads;

                if (child is ISlidePanelChild)
                {
                    first ??= visual;
                    last = visual;
                    travel = Math.Max(travel, (float)child.DesiredSize.Height);
                }
            }

            _cardTravel = travel;
            UpdateCard(first, last);

            return new Size(width, height);
        }

        private void UpdateCard(Visual first, Visual last)
        {
            if (first == _cardFirst && last == _cardLast)
            {
                return;
            }

            _cardFirst = first;
            _cardLast = last;

            var card = _cardVisual;
            var compositor = card.Compositor;

            if (first == null)
            {
                _cardGeometry.StopAnimation("Offset");
                _cardGeometry.StopAnimation("Size");
                _cardGeometry.Size = Vector2.Zero;

                card.StopAnimation("Opacity");

                if (_cardShadow != null)
                {
                    _cardShadow.StopAnimation("Offset");
                    _cardShadow.StopAnimation("Size");
                    _cardShadow.StopAnimation("Opacity");
                    _cardShadow.IsVisible = false;
                }

                return;
            }

            if (_cardShadow == null)
            {
                var radius = (float)CardCornerRadius;

                _cardShadowInsets = ThemeShadowEx.GetInsets(Constants.BubbleElevation);
                _cardShadow = compositor.CreateSpriteVisual();
                _cardShadow.Brush = ThemeShadowEx.CreateBrush(compositor, Constants.BubbleElevation, ActualTheme, radius, radius);
                ElementComposition.SetElementChildVisual(_cardShadowHost, _cardShadow);
            }

            // The visible top of the first child: where its own clip starts, which is its top
            // unless it is sliding in or out from under the one above it.
            const string top = "(first.Offset.Y + first.Translation.Y + firstClip.TopInset)";
            const string bottom = "(last.Offset.Y + last.Translation.Y + last.Size.Y)";

            _cardOffsetAnimation ??= compositor.CreateExpressionAnimation("Vector2(0, " + top + " - card.Offset.Y)");
            _cardSizeAnimation ??= compositor.CreateExpressionAnimation("Vector2(card.Size.X, " + bottom + " - " + top + ")");
            // Only a child alone fades, or two while one replaces the other: then the card stays
            // as opaque as the more opaque of them.
            _cardOpacityAnimation ??= compositor.CreateExpressionAnimation("Max(first.Opacity, last.Opacity)");

            _cardShadowOffsetAnimation ??= compositor.CreateExpressionAnimation("Vector3(-insetLeft, " + top + " - card.Offset.Y - insetTop, 0)");
            _cardShadowSizeAnimation ??= compositor.CreateExpressionAnimation("Vector2(card.Size.X + insetLeft + insetRight, " + bottom + " - " + top + " + insetTop + insetBottom)");

            SetCardParameters(_cardOffsetAnimation, first, last, card);
            SetCardParameters(_cardSizeAnimation, first, last, card);
            SetCardParameters(_cardOpacityAnimation, first, last, card);
            SetCardParameters(_cardShadowOffsetAnimation, first, last, card);
            SetCardParameters(_cardShadowSizeAnimation, first, last, card);

            _cardGeometry.StartAnimation("Offset", _cardOffsetAnimation);
            _cardGeometry.StartAnimation("Size", _cardSizeAnimation);
            card.StartAnimation("Opacity", _cardOpacityAnimation);

            _cardShadow.IsVisible = true;
            _cardShadow.StartAnimation("Offset", _cardShadowOffsetAnimation);
            _cardShadow.StartAnimation("Size", _cardShadowSizeAnimation);
            _cardShadow.StartAnimation("Opacity", _cardOpacityAnimation);
        }

        // StartAnimation snapshots the parameters, so each expression is reused across restarts.
        private void SetCardParameters(ExpressionAnimation animation, Visual first, Visual last, Visual card)
        {
            animation.SetReferenceParameter("first", first);
            animation.SetReferenceParameter("firstClip", first.Clip);
            animation.SetReferenceParameter("last", last);
            animation.SetReferenceParameter("card", card);
            animation.SetScalarParameter("insetLeft", (float)_cardShadowInsets.Left);
            animation.SetScalarParameter("insetTop", (float)_cardShadowInsets.Top);
            animation.SetScalarParameter("insetRight", (float)_cardShadowInsets.Right);
            animation.SetScalarParameter("insetBottom", (float)_cardShadowInsets.Bottom);
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_cardShadow != null)
            {
                var radius = (float)CardCornerRadius;
                _cardShadow.Brush = ThemeShadowEx.CreateBrush(_cardShadow.Compositor, Constants.BubbleElevation, ActualTheme, radius, radius);
            }
        }

        // Visibility is what is read, and a child sliding out stays Visible until its animation
        // ends, so the one after it takes over IsFirst or IsLast only then.
        private void UpdateFirstAndLast(int count)
        {
            ISlidePanelChild first = null;
            ISlidePanelChild last = null;

            for (int i = 0; i < count; i++)
            {
                var element = Children[i];
                if (element is ISlidePanelChild child && element.Visibility == Visibility.Visible)
                {
                    first ??= child;
                    last = child;
                }
            }

            if (_first != first)
            {
                _first?.IsFirst = false;
                first?.IsFirst = true;
                _first = first;
            }

            if (_last != last)
            {
                _last?.IsLast = false;
                last?.IsLast = true;
                _last = last;
            }
        }

        /// <summary>
        /// Whether no child other than <paramref name="element"/> is shown or on its way to being
        /// shown, counting only the children that implement <see cref="ISlidePanelChild"/>.
        /// </summary>
        public bool IsAlone(UIElement element)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child != element && child is ISlidePanelChild slide && slide.SlideState?.IsVisible == true)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether no child that implements <see cref="ISlidePanelChild"/> is shown or on its way
        /// to being shown. One still sliding out does not count.
        /// </summary>
        public bool IsEmpty => IsAlone(null);

        protected override Size ArrangeOverride(Size finalSize)
        {
            var offset = 0f;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                var height = child.DesiredSize.Height;

                if (child == _card || child == _cardShadowHost)
                {
                    child.Arrange(new Rect(0, -_cardTravel, finalSize.Width, _cardTravel + finalSize.Height + LeadingSpacing));
                    continue;
                }

                child.Arrange(new Rect(0, 0, finalSize.Width, height));

                if (_first != null && ReferenceEquals(child, _first))
                {
                    offset += (float)LeadingSpacing;
                }

                if (i < _offsets.Length)
                {
                    _offsets[i] = offset;
                }

                offset += (float)height;
            }

            return finalSize;
        }

        /// <summary>
        /// The Offset.Y the child settles at once no header is sliding, 0 for one this panel does
        /// not hold.
        /// </summary>
        public float GetStackedOffset(UIElement element)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                if (_children[i] == element)
                {
                    return _offsets[i];
                }
            }

            return 0;
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
