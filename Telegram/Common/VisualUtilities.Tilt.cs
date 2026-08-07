//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Navigation;
using Windows.Devices.Input;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
{
    /// <summary>
    /// Reports the state of a tilt once per rendered frame.
    /// </summary>
    /// <param name="pointer">
    /// Where the pointer is, -1 to 1 from the centre of the element on each axis.
    /// Not clamped: the pointer is captured, so it goes past 1 when it leaves.
    /// </param>
    /// <param name="amount">How far the tilt has eased in, 0 to 1.</param>
    public delegate void TiltUpdateHandler(Vector2 pointer, float amount);

    public partial class VisualUtilities
    {
        // Keyed weakly so a page that forgets to detach cannot pin its own card.
        private static readonly ConditionalWeakTable<FrameworkElement, PointerTilt> _tilts = new();

        /// <summary>
        /// Tilts <paramref name="element"/> towards the pointer while it is held
        /// down, and rotates <paramref name="sheen"/> with it so a conic-gradient
        /// highlight sweeps like light across the surface.
        ///
        /// Unlike PointerDownThemeAnimation this keeps tracking as the pointer
        /// moves — that animation samples the press point once and eases to a fixed
        /// target, and restarting it per move restarts its easing. Everything here
        /// runs on the compositor: a pointer move costs one InsertVector2 and no
        /// layout or render pass, which matters because PointerMoved arrives at
        /// input rate (120-240Hz on a precision mouse).
        ///
        /// Pressed rather than hover, deliberately: hover does not exist on touch
        /// or pen, so a hover effect would be feedback only mouse users ever see.
        /// </summary>
        /// <param name="sheen">
        /// A child of <paramref name="element"/> carrying the gradient. It is scaled
        /// past the element's bounds so neither the rotation nor the shift below can
        /// swing an empty corner into view.
        /// </param>
        /// <param name="sheenDegrees">
        /// How far the highlight sweeps, at the element's edge. Bounded on purpose:
        /// following the pointer's angle instead looks like a pinwheel, because a
        /// conic gradient with four arms is nearly back where it started after a
        /// quarter turn.
        /// </param>
        /// <param name="sheenShift">
        /// How far the gradient slides against the tilt, as a fraction of the
        /// element's width, which moves where its arms converge. Off by default:
        /// it stops the highlight turning about the middle of the element, which
        /// is usually not what you want on a centred gradient.
        /// </param>
        /// <param name="onUpdate">
        /// Raised once per rendered frame while the element is tilted or settling back,
        /// with the same normalized pointer and eased strength the expressions above
        /// use. For effects that are not a rigid transform and must be redrawn, such as
        /// a gradient whose bands change shape.
        ///
        /// Per frame rather than per pointer event: input arrives at up to 240Hz, and
        /// redrawing more than once between two frames is not observable.
        /// </param>
        public static void AttachTilt(FrameworkElement element, FrameworkElement sheen = null,
            float degrees = 6, float sheenDegrees = 30, float sheenShift = 0,
            TiltUpdateHandler onUpdate = null)
        {
            if (element == null || _tilts.TryGetValue(element, out _))
            {
                return;
            }

            var tilt = new PointerTilt(element, sheen, degrees, sheenDegrees, sheenShift, onUpdate);
            _tilts.Add(element, tilt);
            tilt.Attach();
        }

        /// <summary>Undoes <see cref="AttachTilt"/> and restores the visuals.</summary>
        public static void DetachTilt(FrameworkElement element)
        {
            if (element == null || !_tilts.TryGetValue(element, out var tilt))
            {
                return;
            }

            _tilts.Remove(element);
            tilt.Detach();
        }

        private sealed class PointerTilt
        {
            // How far the viewer sits from the plane. Smaller exaggerates the
            // perspective; much below this and the card looks like it is falling
            // away rather than tipping.
            private const float Distance = 800f;

            // The durations Animate uses, so a redraw driven off the callback starts
            // and finishes with the transform driven off the compositor. The ramp
            // between is linear here against that one's default easing; the two drive
            // different aspects of one surface, so the difference is not observable.
            private const float RiseDuration = 120f;
            private const float FallDuration = 250f;

            private readonly FrameworkElement _element;
            private readonly FrameworkElement _sheen;
            private readonly float _degrees;
            private readonly float _sheenDegrees;
            private readonly float _sheenShift;
            private readonly TiltUpdateHandler _onUpdate;

            private FrameworkElement _host;
            private Visual _visual;
            private Visual _hostVisual;
            private Visual _sheenVisual;
            private CompositionPropertySet _properties;
            private CompositionRoundedRectangleGeometry _clip;

            private bool _pressed;

            // Mirrors of the compositor-animated values, maintained only when the
            // per-frame callback is in use.
            private Vector2 _pointer;
            private float _amount;
            private TimeSpan _timestamp;
            private bool _rendering;

            public PointerTilt(FrameworkElement element, FrameworkElement sheen,
                float degrees, float sheenDegrees, float sheenShift, TiltUpdateHandler onUpdate)
            {
                _element = element;
                _sheen = sheen;
                _degrees = degrees;
                _sheenDegrees = sheenDegrees;
                _sheenShift = sheenShift;
                _onUpdate = onUpdate;
            }

            public void Attach()
            {
                Initialize();

                _element.PointerPressed += OnPointerPressed;
                _element.PointerMoved += OnPointerMoved;
                _element.PointerReleased += OnPointerReleased;
                _element.PointerCanceled += OnPointerCanceled;
                // The one that actually matters: when a press turns into a pan the
                // ScrollViewer takes the pointer and no Released ever arrives, so
                // without this the card stays tilted for the rest of the scroll.
                _element.PointerCaptureLost += OnPointerCaptureLost;
                _element.SizeChanged += OnSizeChanged;
                _element.Unloaded += OnUnloaded;
            }

            public void Detach()
            {
                _element.PointerPressed -= OnPointerPressed;
                _element.PointerMoved -= OnPointerMoved;
                _element.PointerReleased -= OnPointerReleased;
                _element.PointerCanceled -= OnPointerCanceled;
                _element.PointerCaptureLost -= OnPointerCaptureLost;
                _element.SizeChanged -= OnSizeChanged;
                _element.Unloaded -= OnUnloaded;

                StopRendering();

                if (_visual != null)
                {
                    _visual.StopAnimation("RotationAxis");
                    _visual.StopAnimation("RotationAngleInDegrees");
                    _visual.RotationAngleInDegrees = 0;
                    _visual.Clip = null;
                }

                if (_sheenVisual != null)
                {
                    _sheenVisual.StopAnimation("RotationAngleInDegrees");
                    _sheenVisual.RotationAngleInDegrees = 0;
                    _sheenVisual.Scale = Vector3.One;

                    // Translation lives in the visual's own property set, put there
                    // by SetIsTranslationEnabled, so it is cleared there too.
                    _sheenVisual.StopAnimation("Translation");
                    _sheenVisual.Properties.InsertVector3("Translation", Vector3.Zero);
                }

                if (_hostVisual != null)
                {
                    _hostVisual.TransformMatrix = Matrix4x4.Identity;
                }
            }

            private void Initialize()
            {
                var compositor = BootStrapper.Current.Compositor;

                _visual = ElementComposition.GetElementVisual(_element);

                _properties = compositor.CreatePropertySet();
                _properties.InsertVector2("Pointer", Vector2.Zero);
                _properties.InsertScalar("Amount", 0);
                _properties.InsertScalar("Shift", 0);

                // The axis is perpendicular to the pointer offset, so the edge under
                // the pointer is the one that dips. Its length does not matter —
                // Composition normalizes it — and when the offset is zero the angle
                // is zero too, so the degenerate axis never reaches a rotation.
                var axis = compositor.CreateExpressionAnimation("Vector3(-props.Pointer.Y, props.Pointer.X, 0)");
                axis.SetReferenceParameter("props", _properties);
                _visual.StartAnimation("RotationAxis", axis);

                // Clamped, because the corners of the element are further than 1
                // from the centre and would otherwise tilt half again as far as the
                // edges.
                var angle = compositor.CreateExpressionAnimation("Clamp(Length(props.Pointer), 0, 1) * props.Amount * degrees");
                angle.SetReferenceParameter("props", _properties);
                angle.SetScalarParameter("degrees", _degrees);
                _visual.StartAnimation("RotationAngleInDegrees", angle);

                if (_sheen != null)
                {
                    _sheenVisual = ElementComposition.GetElementVisual(_sheen);

                    // Horizontal offset only: a highlight that sweeps as the card
                    // turns left and right reads as light moving over it, while
                    // coupling it to both axes just looks like the artwork spinning.
                    // Divided by Max(Length, 1), not clamped per component, so this
                    // tracks the card's actual lean rather than something merely
                    // similar.
                    //
                    // The tilt saturates its ANGLE at Clamp(Length, 0, 1) while its
                    // axis keeps turning, so past the edge the card stops tipping
                    // further but goes on tipping around. A per-component clamp
                    // freezes solid the moment X passes 1, and the highlight visibly
                    // parts company with the card. This expression is identical to
                    // Pointer.X inside the element (Max is 1 there) and becomes the
                    // normalized direction outside it — which is exactly the
                    // horizontal component of the rotation the tilt is applying.
                    var sweep = compositor.CreateExpressionAnimation("props.Pointer.X / Max(Length(props.Pointer), 1) * props.Amount * degrees * -1");
                    sweep.SetReferenceParameter("props", _properties);
                    sweep.SetScalarParameter("degrees", _sheenDegrees);
                    _sheenVisual.StartAnimation("RotationAngleInDegrees", sweep);

                    // The gradient also slides against the tilt, so its centre stays
                    // put in the world while the card turns under it. This is the
                    // part that reads as a reflection; the rotation on its own just
                    // spins the arms about a centre that never moves.
                    //
                    // Translation, not Offset: XAML owns an element's Offset and
                    // rewrites it on layout — the same reason the perspective had to
                    // become a PerspectiveTransform3D in the markup.
                    ElementCompositionPreview.SetIsTranslationEnabled(_sheen, true);

                    // Divided per component: the expression language has no
                    // vector-by-scalar division, so Vector3(...) / scalar does not
                    // compile. Max(Length, 1) is the same radial clamp the sweep
                    // above uses, so the slide stays with the card past the edge
                    // instead of freezing.
                    var shift = compositor.CreateExpressionAnimation(
                        "Vector3(props.Pointer.X / Max(Length(props.Pointer), 1), props.Pointer.Y / Max(Length(props.Pointer), 1), 0) * props.Shift * props.Amount * -1");
                    shift.SetReferenceParameter("props", _properties);
                    _sheenVisual.StartAnimation("Translation", shift);
                }

                // Perspective belongs on the parent: on the visual's own
                // TransformMatrix it would be composed inside the rotation and the
                // tilt would come out as a flat squash with no depth. Siblings are
                // unaffected — the matrix only bends geometry with a non-zero Z, and
                // theirs is zero.
                _host = VisualTreeHelper.GetParent(_element) as FrameworkElement;
                if (_host != null)
                {
                    _hostVisual = ElementComposition.GetElementVisual(_host);
                    _hostVisual.TransformMatrix = new Matrix4x4(
                        1, 0, 0, 0,
                        0, 1, 0, 0,
                        0, 0, 1, -1 / Distance,
                        0, 0, 0, 1);
                }

                UpdateSize();
            }

            private void UpdateSize()
            {
                var width = (float)_element.ActualWidth;
                var height = (float)_element.ActualHeight;

                if (width <= 0 || height <= 0)
                {
                    return;
                }

                _visual.CenterPoint = new Vector3(width / 2, height / 2, 0);

                // A fraction of the width, so the slide keeps its proportions when
                // the card is laid out at another size.
                _properties.InsertScalar("Shift", width * _sheenShift);

                if (_hostVisual != null)
                {
                    // The vanishing point has to sit over the card, not over the
                    // parent, or the card tips as though it were off to one side.
                    var origin = _element.TransformToVisual(_host).TransformPoint(new Point(0, 0));
                    _hostVisual.CenterPoint = new Vector3(
                        (float)origin.X + width / 2,
                        (float)origin.Y + height / 2,
                        0);
                }

                var radius = _element switch
                {
                    Control control => (float)control.CornerRadius.TopLeft,
                    Grid grid => (float)grid.CornerRadius.TopLeft,
                    Border border => (float)border.CornerRadius.TopLeft,
                    _ => 0
                };

                _clip ??= _visual.Compositor.CreateRoundedRectangleGeometry();
                //_clip.Size = new Vector2(width, height);
                //_clip.CornerRadius = new Vector2(radius);
                //_visual.Clip ??= _visual.Compositor.CreateGeometricClip(_clip);

                if (_sheenVisual != null)
                {
                    var sheenWidth = (float)_sheen.ActualWidth;
                    var sheenHeight = (float)_sheen.ActualHeight;

                    _sheenVisual.CenterPoint = new Vector3(sheenWidth / 2, sheenHeight / 2, 0);

                    // The diagonal over the side: enough that no rotation about the
                    // centre can bring an uncovered corner into the clip.
                    var scale = MathF.Sqrt(4);
                    _sheenVisual.Scale = new Vector3(scale, scale, 1);
                }
            }

            private void OnSizeChanged(object sender, SizeChangedEventArgs e)
            {
                UpdateSize();
            }

            private void OnUnloaded(object sender, RoutedEventArgs e)
            {
                DetachTilt(_element);
            }

            private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
            {
                // Continuous motion is exactly what this setting exists to suppress,
                // and it can change while the page is open, so it is read per press
                // rather than once at attach.
                if (!PowerSavingPolicy.AreSmoothTransitionsEnabled)
                {
                    return;
                }

                _pressed = true;

                // Without capture the moves stop at the element's edge and the tilt
                // freezes wherever the pointer crossed it.
                //
                // Mouse and pen only: a touch press-drag IS the ScrollViewer's pan
                // gesture, and capturing it here would stop the list scrolling.
                // Nothing is lost by leaving touch uncaptured — a touch that leaves
                // the card is a pan, the scroller takes the pointer, and the
                // PointerCaptureLost that follows is exactly the reset we want.
                if (e.Pointer.PointerDeviceType != PointerDeviceType.Touch)
                {
                    _element.CapturePointer(e.Pointer);
                }

                UpdatePointer(e);
                Animate(1);

                if (_onUpdate != null && !_rendering)
                {
                    _rendering = true;
                    _timestamp = TimeSpan.Zero;
                    // Qualified: Windows.UI.Composition also declares CompositionTarget.
                    Windows.UI.Xaml.Media.CompositionTarget.Rendering += OnRendering;
                }
            }

            private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
            {
                if (_pressed)
                {
                    UpdatePointer(e);
                }
            }

            private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
            {
                Release();
            }

            private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
            {
                Release();
            }

            private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
            {
                Release();
            }

            private void Release()
            {
                if (_pressed)
                {
                    // Cleared first: releasing capture raises PointerCaptureLost,
                    // which lands straight back here.
                    _pressed = false;

                    _element.ReleasePointerCaptures();
                    Animate(0);
                }
            }

            private void StopRendering()
            {
                if (_rendering)
                {
                    _rendering = false;
                    Windows.UI.Xaml.Media.CompositionTarget.Rendering -= OnRendering;
                }
            }

            /// <summary>
            /// Drives <see cref="_onUpdate"/>. Subscribed only between the press and the
            /// end of the settle, since this is the one part of the effect with a
            /// per-frame cost.
            /// </summary>
            private void OnRendering(object sender, object e)
            {
                // Amount is eased on the compositor, and reading an animating property
                // back off a property set is unreliable, so it is eased again here
                // against the frame clock. Wall time rather than a fixed step per frame,
                // so the two agree on a slow frame.
                var timestamp = e is RenderingEventArgs args ? args.RenderingTime : _timestamp;

                // Clamped: the first frame has no predecessor, and a frame dropped
                // behind a long layout pass must not snap the ease to its endpoint.
                var elapsed = _timestamp == TimeSpan.Zero
                    ? 0f
                    : MathF.Min((float)(timestamp - _timestamp).TotalMilliseconds, 100f);

                _timestamp = timestamp;

                if (_pressed)
                {
                    _amount = MathF.Min(_amount + elapsed / RiseDuration, 1);
                }
                else
                {
                    _amount = MathF.Max(_amount - elapsed / FallDuration, 0);
                }

                _onUpdate(_pointer, _amount);

                // A final callback at exactly zero has already been raised above, so the
                // redrawn surface lands on its rest state before the pump stops.
                if (!_pressed && _amount == 0)
                {
                    StopRendering();
                }
            }

            private void UpdatePointer(PointerRoutedEventArgs e)
            {
                var width = (float)_element.ActualWidth;
                var height = (float)_element.ActualHeight;

                if (width <= 0 || height <= 0)
                {
                    return;
                }

                var point = e.GetCurrentPoint(_element).Position;

                // -1..1 from the centre. The expressions above read this on the
                // compositor thread, so a move costs the two writes here and no
                // layout or render pass.
                _pointer = new Vector2(
                    (float)(point.X / width) * 2 - 1,
                    (float)(point.Y / height) * 2 - 1);

                _properties.InsertVector2("Pointer", _pointer);
            }

            private void Animate(float amount)
            {
                var compositor = _properties.Compositor;

                // Only the press and the settle are eased. The tilt itself follows
                // the pointer directly — mouse and touch input is already continuous,
                // and smoothing it would just add lag.
                var animation = compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(1, amount);
                animation.Duration = TimeSpan.FromMilliseconds(amount > 0 ? 120 : 250);

                _properties.StartAnimation("Amount", animation);
            }
        }
    }
}
