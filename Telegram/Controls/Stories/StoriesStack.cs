//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Telegram.ViewModels.Stories;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Stories
{
    /// <summary>
    /// The first few chats of the stories strip as overlapping avatars, which is all the strip
    /// shows while collapsed. Collapsing, each avatar leaves its cell and gathers into the stack
    /// while the cells fade out; expanding, they spread back out over their cells.
    /// </summary>
    /// <remarks>
    /// Two things drive it. The strip's progress follows the scroll, and moves the strip and sizes
    /// the avatars. Crossing <see cref="Threshold"/> of it flips the stack between gathered and
    /// spread, which then plays out on its own clock, whatever the scroll does meanwhile.
    /// </remarks>
    public sealed partial class StoriesStack : Canvas
    {
        public const int Capacity = 3;

        /// <summary>
        /// The strip's progress below which the stack gathers, and above which it spreads.
        /// </summary>
        public const float Threshold = 0.7f;

        // The cells' pitch, and where an avatar sits in its cell.
        private const int CellWidth = 72;
        private const int AvatarLeft = 12;
        private const int AvatarTop = 12;

        // Where the gathered avatars' left edges are, after the strip's padding.
        private const int StackLeft = 18;
        private const int StackPitch = 12;

        // How far the strip moves between expanded and collapsed.
        private const int StripTravel = 48;

        // Gathering takes a second and overshoots its place twice over; spreading is quicker,
        // and overshoots once.
        private static readonly TimeSpan GatherDuration = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan SpreadMorphDuration = TimeSpan.FromMilliseconds(300);
        private static readonly TimeSpan SpreadDuration = TimeSpan.FromMilliseconds(350);

        private const float GatherTension = 0.95f;
        private const float SpreadTension = 0.9f;

        // Each avatar shrinks by a power of the collapse: the lower, the sooner. The power comes
        // from comparing the avatar's place in the stack with the first one's index in the strip,
        // so with your own cell in front the curve shifts by one place: 0.25, 0.25, 0.5 rather
        // than 0.25, 0.5, 1. That is deliberate, as the linear one settles visibly late. The
        // cells after the stack's shrink linearly, and your own cell, before it, soonest.
        private static float GetWave(int position, int first)
        {
            return position == first + 2 ? 1 : position == first + 1 ? 0.5f : 0.25f;
        }

        private static string GetWave(int position)
        {
            return string.Format(CultureInfo.InvariantCulture, "(_.First == {0} ? 1 : _.First == {1} ? 0.5 : 0.25)", position - 2, position - 1);
        }

        // How much of the strip's travel the first two avatars rise ahead of it.
        private static readonly float[] Lift = { 1, 0.65f, 0 };

        private readonly ActiveStoriesAvatar[] _slots = new ActiveStoriesAvatar[Capacity];
        private readonly ActiveStoriesViewModel[] _items = new ActiveStoriesViewModel[Capacity];

        private readonly CompositionClip[] _segmentsClips = new CompositionClip[Capacity];
        private readonly CompositionClip[] _photoClips = new CompositionClip[Capacity];

        private int _count;

        // Morph: 1 spread, 0 gathered, eased out. Gather and Spread: how far each motion has
        // got, with its overshoot. Slide: where the avatars are between the stack (0) and their
        // cells (1). Collapse: how far the avatars have shrunk. Visible: 1 while the stack differs
        // from the cells under it. Right: the last avatar's right edge, in the strip's coordinates.
        private CompositionPropertySet _morph;

        private CompositionEasingFunction _easeOutQuint;
        private CompositionEasingFunction _gatherEasing;
        private CompositionEasingFunction _spreadEasing;

        private ExpressionAnimation _covered;
        private ExpressionAnimation _follow;
        private ExpressionAnimation _followScale;

        private bool _gathered = true;

        // Morph as last started, to know where an interrupted one has got to.
        private float _morphFrom;
        private float _morphTo;
        private long _morphStart;
        private TimeSpan _morphDuration;

        /// <summary>
        /// The stack's animated state, for what moves with it; see <see cref="_morph"/>. Null until
        /// connected.
        /// </summary>
        public CompositionPropertySet Transition => _morph;

        public StoriesStack()
        {
            IsHitTestVisible = false;

            AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);

            for (int i = 0; i < Capacity; i++)
            {
                var slot = new ActiveStoriesAvatar
                {
                    Visibility = Visibility.Collapsed
                };

                // The first avatar is on top.
                SetZIndex(slot, Capacity - i);
                SetLeft(slot, AvatarLeft + CellWidth * i);
                SetTop(slot, AvatarTop);

                _slots[i] = slot;
                Children.Add(slot);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new StoriesStackAutomationPeer(this);
        }

        /// <summary>
        /// Shows the items from <paramref name="first"/> to <paramref name="last"/>, at most
        /// <see cref="Capacity"/> of them.
        /// </summary>
        public void Update(IList<ActiveStoriesViewModel> items, int first, int last)
        {
            var count = 0;

            for (int i = 0; i < Capacity; i++)
            {
                var index = first + i;
                var item = items != null && index <= last && index < items.Count ? items[index] : null;

                if (_items[i] != item)
                {
                    if (_items[i] != null)
                    {
                        _items[i].PropertyChanged -= OnPropertyChanged;
                    }

                    _items[i] = item;

                    if (item != null)
                    {
                        item.PropertyChanged += OnPropertyChanged;

                        _slots[i].Update(item);
                        _slots[i].UpdateSegments(item.Item);
                    }
                }

                _slots[i].Visibility = item != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (item != null)
                {
                    count++;
                }
            }

            _count = count;
            UpdateClips();
        }

        public void Clear()
        {
            Update(null, 0, -1);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ActiveStoriesViewModel.Item))
            {
                return;
            }

            for (int i = 0; i < Capacity; i++)
            {
                if (_items[i] == sender)
                {
                    _slots[i].UpdateSegments(_items[i].Item);
                }
            }
        }

        /// <summary>
        /// Moves a cell with the stack: the cells of the stack's items exactly as their avatars,
        /// whose drawing the stack takes over while the two differ, and the others into the
        /// nearest end of the stack.
        /// </summary>
        public void Attach(UIElement container, UIElement avatar, int index, int first, int last)
        {
            if (_follow == null)
            {
                return;
            }

            var covered = index >= first && index <= last;
            var position = index - first;

            var visual = ElementComposition.GetElementVisual(container);
            var properties = visual.Properties;

            properties.InsertScalar("Index", index);
            properties.InsertScalar("Slot", Math.Clamp(index, first, Math.Max(first, last)) - first);
            properties.InsertScalar("Wave", covered ? GetWave(position, first) : index < first ? 0.25f : 1);
            properties.InsertScalar("Lift", covered ? Lift[position] : 0);

            ElementCompositionPreview.SetIsTranslationEnabled(container, true);

            // The avatar's centre in the cell.
            visual.CenterPoint = new Vector3(AvatarLeft + 24, AvatarTop + 24, 0);
            visual.Properties.InsertVector3("Translation", Vector3.Zero);

            // Templates: each start takes its own copy, with this cell's properties.
            _follow.SetReferenceParameter("v", properties);
            _followScale.SetReferenceParameter("v", properties);

            visual.StartAnimation("Translation", _follow);
            visual.StartAnimation("Scale", _followScale);

            var photo = ElementComposition.GetElementVisual(avatar);

            if (covered)
            {
                photo.StartAnimation("Opacity", _covered);
            }
            else
            {
                photo.StopAnimation("Opacity");
                photo.Opacity = 1;
            }
        }

        /// <summary>
        /// Gathers the avatars into the stack, or spreads them back over their cells.
        /// </summary>
        public void SetGathered(bool gathered)
        {
            if (_gathered == gathered)
            {
                return;
            }

            _gathered = gathered;

            if (_morph == null)
            {
                return;
            }

            var compositor = _morph.Compositor;
            var from = GetMorph();

            _morphFrom = from;
            _morphTo = gathered ? 0 : 1;
            _morphStart = Stopwatch.GetTimestamp();
            _morphDuration = gathered ? GatherDuration : SpreadMorphDuration;

            _morph.InsertBoolean("Gathered", gathered);

            var morph = compositor.CreateScalarKeyFrameAnimation();
            morph.InsertKeyFrame(0, from);
            morph.InsertKeyFrame(1, _morphTo, _easeOutQuint);
            morph.Duration = _morphDuration;

            _morph.StartAnimation("Morph", morph);

            // Each motion starts from where the morph has got to, so that turning back halfway
            // picks up from there.
            var slide = compositor.CreateScalarKeyFrameAnimation();
            slide.InsertKeyFrame(0, gathered ? 1 - from : from);
            slide.InsertKeyFrame(1, 1, gathered ? _gatherEasing : _spreadEasing);
            slide.Duration = gathered ? GatherDuration : SpreadDuration;

            _morph.StartAnimation(gathered ? "Gather" : "Spread", slide);
        }

        // An estimate, as composition does not hand its animated values back.
        private float GetMorph()
        {
            if (_morphDuration <= TimeSpan.Zero)
            {
                return _morphTo;
            }

            var elapsed = (Stopwatch.GetTimestamp() - _morphStart) / (double)Stopwatch.Frequency;
            var t = Math.Clamp(elapsed / _morphDuration.TotalSeconds, 0, 1);

            return (float)(_morphFrom + (_morphTo - _morphFrom) * (1 - Math.Pow(1 - t, 5)));
        }

        /// <summary>
        /// Drives the stack and the cells' fade from the strip's progress: 0 collapsed, 1 expanded.
        /// <paramref name="scroll"/> is the strip's scroll viewer's manipulation property set, so
        /// that an avatar finds its cell wherever the strip is scrolled.
        /// </summary>
        public void Connect(CompositionPropertySet progress, CompositionPropertySet scroll, ListViewBase cells, bool gathered)
        {
            var compositor = progress.Compositor;

            if (_morph == null)
            {
                _morph = compositor.CreatePropertySet();

                _easeOutQuint = compositor.CreateCubicBezierEasingFunction(new Vector2(0.23f, 1), new Vector2(0.32f, 1));
                _gatherEasing = CreateOvershoot(compositor, GatherTension);
                _spreadEasing = CreateOvershoot(compositor, SpreadTension);
            }
            else
            {
                // Only once they exist: stopping an unknown property throws.
                _morph.StopAnimation("Morph");
                _morph.StopAnimation("Gather");
                _morph.StopAnimation("Spread");
            }

            _gathered = gathered;
            _morphFrom = _morphTo = gathered ? 0 : 1;
            _morphDuration = TimeSpan.Zero;

            _morph.InsertBoolean("Gathered", gathered);
            _morph.InsertScalar("Morph", _morphTo);
            _morph.InsertScalar("Gather", 1);
            _morph.InsertScalar("Spread", 1);
            _morph.InsertScalar("Slide", _morphTo);
            _morph.InsertScalar("Collapse", 1 - _morphTo);
            _morph.InsertScalar("Visible", 0);
            _morph.InsertScalar("Right", 0);

            // Gathering eases its own progress a second time, which is what overshoots twice.
            var gather = "(this.Target.Gather - 1)";
            var gatherOvershoot = string.Format(CultureInfo.InvariantCulture, "-({0} * {0} * ({1} * {0} + {2}))", gather, GatherTension + 1, GatherTension);

            StartAnimation(_morph, "Slide", $"this.Target.Gathered ? {gatherOvershoot} : this.Target.Spread", progress);
            StartAnimation(_morph, "Collapse", "(1 - _.Progress) * (1 - this.Target.Morph)", progress);
            StartAnimation(_morph, "Visible", "this.Target.Collapse > 0 || this.Target.Gathered || this.Target.Spread != 1 ? 1 : 0", progress);

            ElementComposition.GetElementVisual(this).StartAnimation("Opacity", CreateAnimation(compositor, "m.Visible"));
            ElementComposition.GetElementVisual(cells).StartAnimation("Opacity", CreateAnimation(compositor, "Clamp(1 - m.Collapse / 0.3, 0, 1)"));

            _covered = CreateAnimation(compositor, "1 - m.Visible");

            // As each avatar's below, by the cell's left edge rather than the avatar's centre.
            var shrink = "Pow(m.Collapse, v.Wave)";

            _follow = CreateAnimation(compositor, string.Format(CultureInfo.InvariantCulture,
                "Vector3((_.Padding + {4} + {5} * v.Slot - ({0} + {1} * v.Index + s.Translation.X)) * (1 - m.Slide) - 12 * {2} - 12 * {2} * {2}, -{3} * _.Progress * v.Lift * (1 - Pow(1 - m.Collapse, 5)), 0)",
                cells.Padding.Left, CellWidth, shrink, StripTravel, StackLeft, StackPitch));
            _follow.SetReferenceParameter("_", progress);
            _follow.SetReferenceParameter("s", scroll);

            _followScale = CreateAnimation(compositor, $"Vector3(1 - 0.5 * {shrink}, 1 - 0.5 * {shrink}, 1)");

            var morph = CreateAnimation(compositor, "m.Morph");
            var unmorph = CreateAnimation(compositor, "1 - m.Morph");

            for (int i = 0; i < Capacity; i++)
            {
                var slot = _slots[i];
                var visual = ElementComposition.GetElementVisual(slot);

                // Each avatar's centre, in the strip's coordinates, and its
                // scale: what the avatar is drawn at, and what its neighbours cut around. Not
                // "Scale": the visual's properties share names with its own.
                var properties = visual.Properties;
                properties.InsertScalar("Collapse", 0);
                properties.InsertScalar("CenterX", 0);
                properties.InsertScalar("CenterY", 0);
                properties.InsertScalar("Zoom", 1);

                StartAnimation(properties, "Collapse", $"Pow(m.Collapse, {GetWave(i)})", progress);
                StartAnimation(properties, "Zoom", "1 - 0.5 * this.Target.Collapse", progress);

                // From the stack to the cell of the same chat, by the left edge of a box as wide as
                // a cell, with the avatar centred in it while spread and at its left edge while
                // gathered.
                var gatheredLeft = string.Format(CultureInfo.InvariantCulture, "(_.Padding + {0} + {1} * {2})", StackLeft, StackPitch, i);
                var spreadLeft = string.Format(CultureInfo.InvariantCulture, "({0} + {1} * (_.First + {2}) + s.Translation.X)", cells.Padding.Left, CellWidth, i);
                var inner = "(36 - 12 * this.Target.Collapse - 12 * this.Target.Collapse * this.Target.Collapse)";

                var centerX = CreateAnimation(compositor, string.Format(CultureInfo.InvariantCulture, "{0} + ({1} - {0}) * m.Slide + {2}", gatheredLeft, spreadLeft, inner));
                centerX.SetReferenceParameter("_", progress);
                centerX.SetReferenceParameter("s", scroll);
                properties.StartAnimation("CenterX", centerX);

                StartAnimation(properties, "CenterY", string.Format(CultureInfo.InvariantCulture, "-{0} * _.Progress * {1} * (1 - Pow(1 - m.Collapse, 5))", StripTravel, Lift[i]), progress);

                var translation = compositor.CreateExpressionAnimation(string.Format(CultureInfo.InvariantCulture, "Vector3(v.CenterX - {0}, v.CenterY, 0)", AvatarLeft + 24 + CellWidth * i));
                translation.SetReferenceParameter("v", properties);

                var scale = compositor.CreateExpressionAnimation("Vector3(v.Zoom, v.Zoom, 1)");
                scale.SetReferenceParameter("v", properties);

                ElementCompositionPreview.SetIsTranslationEnabled(slot, true);

                visual.CenterPoint = new Vector3(24, 24, 0);
                visual.Properties.InsertVector3("Translation", Vector3.Zero);
                visual.StartAnimation("Translation", translation);
                visual.StartAnimation("Scale", scale);

                ElementComposition.GetElementVisual(slot.Segments).StartAnimation("Opacity", morph);
                ElementComposition.GetElementVisual(slot.LiveBadge).StartAnimation("Opacity", morph);
                ElementComposition.GetElementVisual(slot.LoadSegmentsSmall()).StartAnimation("Opacity", unmorph);
            }

            // Each avatar's ring gives way to the next one, and its photo to the previous.
            for (int i = 0; i < Capacity; i++)
            {
                var properties = ElementComposition.GetElementVisual(_slots[i]).Properties;

                _segmentsClips[i] = i < Capacity - 1
                    ? CreateClip(compositor, properties, ElementComposition.GetElementVisual(_slots[i + 1]).Properties)
                    : null;

                _photoClips[i] = i > 0
                    ? CreateClip(compositor, properties, ElementComposition.GetElementVisual(_slots[i - 1]).Properties)
                    : null;
            }

            // The last avatar shown, of the Count the strip puts in the stack.
            var right = CreateAnimation(compositor, "_.Count >= 3 ? a2.CenterX + 24 * a2.Zoom : _.Count == 2 ? a1.CenterX + 24 * a1.Zoom : a0.CenterX + 24 * a0.Zoom");
            right.SetReferenceParameter("_", progress);

            for (int i = 0; i < Capacity; i++)
            {
                right.SetReferenceParameter("a" + i, ElementComposition.GetElementVisual(_slots[i]).Properties);
            }

            _morph.StartAnimation("Right", right);

            UpdateClips();
        }

        private ExpressionAnimation CreateAnimation(Compositor compositor, string expression)
        {
            var animation = compositor.CreateExpressionAnimation(expression);
            animation.SetReferenceParameter("m", _morph);
            return animation;
        }

        private void StartAnimation(CompositionPropertySet target, string property, string expression, CompositionPropertySet progress)
        {
            var animation = CreateAnimation(target.Compositor, expression);
            animation.SetReferenceParameter("_", progress);
            target.StartAnimation(property, animation);
        }

        // A cubic that leaves at a slope of tension + 3, passes its end, and settles back on it:
        // (t - 1)² ((tension + 1)(t - 1) + tension) + 1, which a Bézier with its x control points
        // at a third and two thirds draws exactly.
        private static CompositionEasingFunction CreateOvershoot(Compositor compositor, float tension)
        {
            return compositor.CreateCubicBezierEasingFunction(new Vector2(1 / 3f, 1 + tension / 3), new Vector2(2 / 3f, 1));
        }

        private void UpdateClips()
        {
            if (_morph == null)
            {
                return;
            }

            for (int i = 0; i < Capacity; i++)
            {
                var slot = _slots[i];

                ElementComposition.GetElementVisual(slot.SegmentsRoot).Clip = i < _count - 1 ? _segmentsClips[i] : null;
                ElementComposition.GetElementVisual(slot.PhotoRoot).Clip = i > 0 && i < _count ? _photoClips[i] : null;
            }
        }

        // The avatar less a circle around its neighbour's centre, sized to the neighbour. The hole is
        // cut from a rectangle far larger than the avatar, so that moving and scaling the clip
        // moves only the hole.
        private static CompositionClip CreateClip(Compositor compositor, CompositionPropertySet self, CompositionPropertySet neighbour)
        {
            var rect = CanvasGeometry.CreateRectangle(null, -1000, -1000, 2000, 2000);
            var hole = CanvasGeometry.CreateGroup(null, new[] { CanvasGeometry.CreateEllipse(null, 0, 0, 22, 22), rect }, CanvasFilledRegionDetermination.Alternate);

            var clip = compositor.CreateGeometricClip(compositor.CreatePathGeometry(new CompositionPath(hole)));

            // In the avatar's own coordinates, before its scale about its centre.
            var offset = compositor.CreateExpressionAnimation("Vector2(24 + (n.CenterX - v.CenterX) / v.Zoom, 24 + (n.CenterY - v.CenterY) / v.Zoom)");
            offset.SetReferenceParameter("v", self);
            offset.SetReferenceParameter("n", neighbour);

            var scale = compositor.CreateExpressionAnimation("Vector2(n.Zoom / v.Zoom, n.Zoom / v.Zoom)");
            scale.SetReferenceParameter("v", self);
            scale.SetReferenceParameter("n", neighbour);

            clip.StartAnimation("Offset", offset);
            clip.StartAnimation("Scale", scale);

            return clip;
        }

        // The strip's cells already describe the same chats.
        private sealed partial class StoriesStackAutomationPeer : FrameworkElementAutomationPeer
        {
            public StoriesStackAutomationPeer(StoriesStack owner)
                : base(owner)
            {
            }

            protected override IList<AutomationPeer> GetChildrenCore()
            {
                return Array.Empty<AutomationPeer>();
            }
        }
    }
}
