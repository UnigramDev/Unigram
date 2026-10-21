//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Telegram.Controls;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Common
{
    /// <summary>
    /// Opens a <see cref="MenuFlyout"/> out of the element that opened it: the menu starts as the
    /// element's own shape and grows into place, and folds back into it on close.
    /// </summary>
    /// <remarks>
    /// The flyout and its presenter template stay stock. For the one show the open/close theme
    /// transition is off, the default ThemeShadow is replaced by a ThemeShadowEx drop shadow that can
    /// follow the shape, and everything put on the presenter's visuals comes off again on close,
    /// because a flyout caches its presenter and reuses it.
    /// <para>
    /// Two springs on a property set, X and Y, run 0 (target) to 1 (menu); every animated value is an
    /// expression over them, so the UI thread does no per-frame work.
    /// </para>
    /// <para>
    /// Right-to-left is not fully supported: the app does not ship it. The one known fault is that the
    /// overshoot scales the presenter about a centre point composition reads mirrored there, so the
    /// menu sways sideways as it settles.
    /// </para>
    /// </remarks>
    public static class MenuFlyoutMorph
    {
        private static readonly ConditionalWeakTable<MenuFlyout, Session> _byFlyout = new ConditionalWeakTable<MenuFlyout, Session>();
        private static readonly ConditionalWeakTable<FrameworkElement, Session> _byTarget = new ConditionalWeakTable<FrameworkElement, Session>();

        /// <param name="placement">
        /// The direction the menu grows from the target. The menu always starts over the target, so
        /// unlike the stock meaning <see cref="FlyoutPlacementMode.Bottom"/> puts the menu's top edge
        /// on the target's top edge, not below it, and the EdgeAligned variants name the edge the two
        /// share. Left and right are mirrored in right-to-left, as they are for a stock flyout. Auto
        /// grows toward the middle of the window. A side without room flips to the other one first,
        /// and the menu slides inside the window only if neither fits. Full centres the menu on the
        /// target, so it grows out from the target's centre.
        /// </param>
        /// <param name="overlap">
        /// False puts the menu beside the target instead, as a stock flyout would be, and leaves the
        /// target visible: the shape starts over the target and moves into place as it grows. Top,
        /// Bottom and Auto grow vertically, Left and Right sideways. Full always overlaps.
        /// </param>
        /// <param name="cornerRadius">
        /// The radius the menu starts from, for a target that looks rounded without being so itself:
        /// a panel whose CornerRadius would clip what it hosts. By default it is read from the source.
        /// </param>
        /// <param name="source">
        /// The element the menu grows out of, when it is not the one it is placed against: an emoji
        /// button whose menu lines up with the whole input box around it. It is expected to be
        /// inside the placement target, and it is the one that fades. By default it is the target.
        /// </param>
        public static bool ShowMorphAt(this MenuFlyout flyout, DependencyObject placementTarget, FlyoutPlacementMode placement = FlyoutPlacementMode.Auto, FlyoutShowMode showMode = FlyoutShowMode.Auto, bool overlap = true, double? cornerRadius = null, FrameworkElement source = null)
        {
            if (flyout.Items.Count == 0)
            {
                return false;
            }

            var target = source ?? placementTarget as FrameworkElement;

            if (!PowerSavingPolicy.AreSmoothTransitionsEnabled
                || placementTarget is not FrameworkElement destination
                || destination.XamlRoot == null
                || destination.ActualWidth < 1
                || destination.ActualHeight < 1
                || target.XamlRoot != destination.XamlRoot
                || target.ActualWidth < 1
                || target.ActualHeight < 1)
            {
                return ShowStock(flyout, placementTarget, placement, showMode);
            }

            // Still open, or still folding back into its target.
            if (_byFlyout.TryGetValue(flyout, out _))
            {
                return false;
            }

            if (_byTarget.TryGetValue(target, out var previous))
            {
                previous.Abort();
            }

            return new Session(flyout, target, destination, placement, overlap, cornerRadius ?? GetCornerRadius(target)).Show(showMode);
        }

        private static bool ShowStock(MenuFlyout flyout, DependencyObject target, FlyoutPlacementMode placement, FlyoutShowMode showMode)
        {
            try
            {
                flyout.ShowAt(target, new FlyoutShowOptions
                {
                    Placement = placement,
                    ShowMode = showMode
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        private enum Alignment
        {
            Auto,
            Start,
            Center,
            End
        }

        private enum Axis
        {
            None,
            Horizontal,
            Vertical
        }

        // The gap the framework leaves between a flyout and its target (FlyoutBase::FlyoutMargin).
        private const double BesideMargin = 4;

        // Room left past the far edge for the overshoot: the springs peak about 5.4% past 1 at
        // OpenDamping, the presenter shows 0.6 of that, and it scales about the target, so the far
        // edge moves out by up to 3.3% of the presenter's length - past the popup's HWND without it.
        private const double OvershootRoom = 0.04;

        // Which edge of the menu meets the same edge of the target, per axis, and the axis the menu
        // grows along when it goes beside the target rather than over it.
        private static void GetAlignment(FlyoutPlacementMode placement, out Alignment horizontal, out Alignment vertical, out Axis growth)
        {
            switch (placement)
            {
                case FlyoutPlacementMode.Left:
                case FlyoutPlacementMode.Right:
                case FlyoutPlacementMode.LeftEdgeAlignedTop:
                case FlyoutPlacementMode.LeftEdgeAlignedBottom:
                case FlyoutPlacementMode.RightEdgeAlignedTop:
                case FlyoutPlacementMode.RightEdgeAlignedBottom:
                    growth = Axis.Horizontal;
                    break;
                case FlyoutPlacementMode.Full:
                    growth = Axis.None;
                    break;
                default:
                    growth = Axis.Vertical;
                    break;
            }

            switch (placement)
            {
                case FlyoutPlacementMode.Top:
                    horizontal = Alignment.Center;
                    vertical = Alignment.End;
                    break;
                case FlyoutPlacementMode.Bottom:
                    horizontal = Alignment.Center;
                    vertical = Alignment.Start;
                    break;
                case FlyoutPlacementMode.Left:
                    horizontal = Alignment.End;
                    vertical = Alignment.Center;
                    break;
                case FlyoutPlacementMode.Right:
                    horizontal = Alignment.Start;
                    vertical = Alignment.Center;
                    break;
                case FlyoutPlacementMode.TopEdgeAlignedLeft:
                    horizontal = Alignment.Start;
                    vertical = Alignment.End;
                    break;
                case FlyoutPlacementMode.TopEdgeAlignedRight:
                    horizontal = Alignment.End;
                    vertical = Alignment.End;
                    break;
                case FlyoutPlacementMode.BottomEdgeAlignedLeft:
                    horizontal = Alignment.Start;
                    vertical = Alignment.Start;
                    break;
                case FlyoutPlacementMode.BottomEdgeAlignedRight:
                    horizontal = Alignment.End;
                    vertical = Alignment.Start;
                    break;
                case FlyoutPlacementMode.LeftEdgeAlignedTop:
                    horizontal = Alignment.End;
                    vertical = Alignment.Start;
                    break;
                case FlyoutPlacementMode.LeftEdgeAlignedBottom:
                    horizontal = Alignment.End;
                    vertical = Alignment.End;
                    break;
                case FlyoutPlacementMode.RightEdgeAlignedTop:
                    horizontal = Alignment.Start;
                    vertical = Alignment.Start;
                    break;
                case FlyoutPlacementMode.RightEdgeAlignedBottom:
                    horizontal = Alignment.Start;
                    vertical = Alignment.End;
                    break;
                case FlyoutPlacementMode.Full:
                    horizontal = Alignment.Center;
                    vertical = Alignment.Center;
                    break;
                default:
                    horizontal = Alignment.Auto;
                    vertical = Alignment.Auto;
                    break;
            }
        }

        // Where the menu starts on one axis, in the same coordinates as the target's edges. Beside,
        // Start puts the menu after the target and End before it, instead of sharing that edge.
        private static double Place(Alignment alignment, double start, double end, double length, double min, double max, bool beside)
        {
            if (alignment == Alignment.Auto)
            {
                alignment = (start + end) / 2 > (min + max) / 2 ? Alignment.End : Alignment.Start;
            }

            var origin = Align(alignment, start, end, length, beside);

            // Flip before sliding: a menu growing away from its target still reads as coming out of
            // it, one pushed back across it does not.
            if (alignment == Alignment.Start && origin + length > max)
            {
                origin = Align(Alignment.End, start, end, length, beside);
            }
            else if (alignment == Alignment.End && origin < min)
            {
                origin = Align(Alignment.Start, start, end, length, beside);
            }

            // Then slide inside. The start edge wins when the menu is larger than the window.
            if (origin + length > max)
            {
                origin = max - length;
            }

            if (origin < min)
            {
                origin = min;
            }

            return origin;
        }

        private static double Align(Alignment alignment, double start, double end, double length, bool beside)
        {
            return alignment switch
            {
                Alignment.End => beside ? start - BesideMargin - length : end - length,
                Alignment.Center => (start + end - length) / 2,
                _ => beside ? end + BesideMargin : start
            };
        }

        #region Presenter style

        // The framework's ThemeShadow is cast from the presenter's full bounds, so it would show the
        // finished menu around a target-sized shape. It has to be kept off rather than taken off:
        // the presenter gets it in OnApplyTemplate together with an elevation Translation, and
        // removing the shadow from under that Translation left the menu unclickable. So the morph
        // shows the flyout with its own presenter style extended by IsDefaultShadowEnabled=False.
        //
        // Per thread, because a Style is a DependencyObject and every view has its own thread.
        [ThreadStatic]
        private static ConditionalWeakTable<Style, Style> _shadowless;
        [ThreadStatic]
        private static Style _implicitShadowless;

        private static Style GetShadowlessStyle(Style style)
        {
            if (style == null)
            {
                return _implicitShadowless ??= CreateShadowlessStyle(GetImplicitStyle());
            }

            _shadowless ??= new ConditionalWeakTable<Style, Style>();

            if (!_shadowless.TryGetValue(style, out var derived))
            {
                derived = CreateShadowlessStyle(style);
                _shadowless.Add(style, derived);
            }

            return derived;
        }

        // An explicit style replaces the implicit one rather than layering on it, so the derived
        // style has to be based on it to keep the app's look.
        private static Style GetImplicitStyle()
        {
            var resources = Application.Current.Resources;

            if (resources.TryGetValue(typeof(MenuFlyoutPresenter), out var found) && found is Style style)
            {
                return style;
            }

            if (resources.TryGetValue("DefaultMenuFlyoutPresenterStyle", out found) && found is Style fallback)
            {
                return fallback;
            }

            return null;
        }

        private static Style CreateShadowlessStyle(Style basedOn)
        {
            var style = new Style(typeof(MenuFlyoutPresenter))
            {
                BasedOn = basedOn
            };

            style.Setters.Add(new Setter(MenuFlyoutPresenter.IsDefaultShadowEnabledProperty, false));
            return style;
        }

        #endregion

        #region Templates

        // Translation.Z the framework gives a top-level flyout presenter (ElevationHelper's
        // s_elevationBaseDepth): the elevation of the shadow being replaced.
        private const float FlyoutElevation = 32;

        // Periods are the springs' oscillation periods, not durations. Width leads height on the way
        // out, which is what makes the shape read as stretching rather than scaling.
        private const float OpenDamping = 0.68f;
        private const double OpenPeriodX = 42;
        private const double OpenPeriodY = 58;

        private const double CloseDurationX = 240;
        private const double CloseDurationY = 190;

        private const string CX = "Clamp(p.X, 0, 1)";
        private const string CY = "Clamp(p.Y, 0, 1)";

        // The scale the items grow from, and how much of the springs' overshoot the presenter shows.
        private const string ContentScaleFrom = "0.6";
        private const string Bounce = "0.6";

        private static string Lerp(string a, string b, string t) => "Lerp(" + a + ", " + b + ", " + t + ")";

        // A composition animation is a template that StartAnimation instantiates, so one set per
        // compositor serves every show; only the reference parameter changes between them.
        private sealed class Templates
        {
            public readonly ExpressionAnimation ClipOffset;
            public readonly ExpressionAnimation ClipSize;
            public readonly ExpressionAnimation ClipCornerRadius;
            public readonly ExpressionAnimation BodyOpacity;
            public readonly ExpressionAnimation ContentOpacity;
            public readonly ExpressionAnimation ContentScale;
            public readonly ExpressionAnimation PresenterScale;
            public readonly ExpressionAnimation TargetOpacity;
            public readonly ExpressionAnimation ShadowOffset;
            public readonly ExpressionAnimation ShadowSize;
            public readonly ExpressionAnimation ShadowFromOpacity;
            public readonly ExpressionAnimation ShadowToOpacity;
            public readonly ExpressionAnimation StrokeOffset;
            public readonly ExpressionAnimation StrokeSize;
            public readonly ExpressionAnimation StrokeCornerRadius;

            public readonly SpringScalarNaturalMotionAnimation OpenX;
            public readonly SpringScalarNaturalMotionAnimation OpenY;
            public readonly ScalarKeyFrameAnimation CloseX;
            public readonly ScalarKeyFrameAnimation CloseY;

            // ThemeShadowEx.CreateBrush is uncached; a menu only ever needs a couple of radii.
            public readonly Dictionary<ShadowKey, CompositionBrush> Shadows = new Dictionary<ShadowKey, CompositionBrush>();

            public Templates(Compositor compositor)
            {
                var left = Lerp("p.From.X", "p.To.X", CX);
                var right = Lerp("p.From.Z", "p.To.Z", CX);
                var top = Lerp("p.From.Y", "p.To.Y", CY);
                var bottom = Lerp("p.From.W", "p.To.W", CY);
                var radius = Lerp("p.Radius.X", "p.Radius.Y", CY);
                var scale = Lerp(ContentScaleFrom, "1", "Clamp((p.X + p.Y) / 2, 0, 1)");
                var bounce = "1 + Max((p.X + p.Y) / 2 - 1, 0) * " + Bounce;

                // Inside the stroke, as Border draws its background with InnerBorderEdge: concentric
                // with the stroke's inner edge, so the radius shrinks by the same thickness.
                var inner = "Max(" + radius + " - p.Thickness, 0)";

                ClipOffset = compositor.CreateExpressionAnimation("Vector2(" + left + " + p.Thickness, " + top + " + p.Thickness)");
                ClipSize = compositor.CreateExpressionAnimation("Vector2(" + right + " - " + left + " - p.Thickness * 2, " + bottom + " - " + top + " - p.Thickness * 2)");
                ClipCornerRadius = compositor.CreateExpressionAnimation("Vector2(" + inner + ", " + inner + ")");

                // The body is the menu's material, so it is opaque well before the shape is grown,
                // while the items only appear once there is room for them.
                BodyOpacity = compositor.CreateExpressionAnimation("Clamp(p.X * 5, 0, 1)");
                ContentOpacity = compositor.CreateExpressionAnimation("Clamp((p.Y - 0.35) * 2.5, 0, 1)");
                ContentScale = compositor.CreateExpressionAnimation("Vector3(" + scale + ", " + scale + ", 1)");

                // The springs overshoot past 1, and a clip cannot grow past the element it clips, so
                // the overshoot is spent as a small scale about the target instead.
                PresenterScale = compositor.CreateExpressionAnimation("Vector3(" + bounce + ", " + bounce + ", 1)");

                // The target stays where it is under the menu, and only fades while the body fades in.
                TargetOpacity = compositor.CreateExpressionAnimation("1 - Clamp(p.X * 3, 0, 1)");

                // The shadow is a hollow nine grid: it only has to be the shape plus its insets.
                ShadowOffset = compositor.CreateExpressionAnimation("Vector3(" + left + " - p.Insets.X, " + top + " - p.Insets.Y, 0)");
                ShadowSize = compositor.CreateExpressionAnimation("Vector2(" + right + " - " + left + " + p.Insets.X + p.Insets.Z, " + bottom + " - " + top + " + p.Insets.Y + p.Insets.W)");
                ShadowFromOpacity = compositor.CreateExpressionAnimation("1 - " + CY);
                ShadowToOpacity = compositor.CreateExpressionAnimation(CY);

                StrokeOffset = compositor.CreateExpressionAnimation("Vector2(" + left + " + p.Inset, " + top + " + p.Inset)");
                StrokeSize = compositor.CreateExpressionAnimation("Vector2(" + right + " - " + left + " - p.Inset * 2, " + bottom + " - " + top + " - p.Inset * 2)");
                StrokeCornerRadius = compositor.CreateExpressionAnimation("Vector2(Max(" + radius + " - p.Inset, 0), Max(" + radius + " - p.Inset, 0))");

                OpenX = compositor.CreateSpringScalarAnimation();
                OpenX.FinalValue = 1;
                OpenX.Period = TimeSpan.FromMilliseconds(OpenPeriodX);
                OpenX.DampingRatio = OpenDamping;

                OpenY = compositor.CreateSpringScalarAnimation();
                OpenY.FinalValue = 1;
                OpenY.Period = TimeSpan.FromMilliseconds(OpenPeriodY);
                OpenY.DampingRatio = OpenDamping;

                // The close is a fixed curve rather than a spring: the menu stays up until it ends,
                // and a critically damped spring spends half a second creeping over the last pixel.
                // It starts from wherever the open had got to, so closing mid-open does not jump.
                var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(0.2f, 1));

                CloseX = compositor.CreateScalarKeyFrameAnimation();
                CloseX.InsertExpressionKeyFrame(0, "this.StartingValue");
                CloseX.InsertKeyFrame(1, 0, easing);
                CloseX.Duration = TimeSpan.FromMilliseconds(CloseDurationX);

                CloseY = compositor.CreateScalarKeyFrameAnimation();
                CloseY.InsertExpressionKeyFrame(0, "this.StartingValue");
                CloseY.InsertKeyFrame(1, 0, easing);
                CloseY.Duration = TimeSpan.FromMilliseconds(CloseDurationY);
            }

            public CompositionBrush GetShadow(Compositor compositor, ElementTheme theme, float radius)
            {
                var key = new ShadowKey(theme, radius);
                if (Shadows.TryGetValue(key, out var brush))
                {
                    return brush;
                }

                brush = ThemeShadowEx.CreateBrush(compositor, FlyoutElevation, theme, radius, radius);
                Shadows[key] = brush;

                return brush;
            }
        }

        private readonly struct ShadowKey : IEquatable<ShadowKey>
        {
            public readonly ElementTheme Theme;
            public readonly float Radius;

            public ShadowKey(ElementTheme theme, float radius)
            {
                Theme = theme;
                Radius = radius;
            }

            public bool Equals(ShadowKey other) => Theme == other.Theme && Radius == other.Radius;

            public override bool Equals(object obj) => obj is ShadowKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Theme, Radius);
        }

        // Keyed on the compositor rather than the window: the menu is a windowed popup, and its
        // visuals are what the templates are started on.
        private static readonly ConditionalWeakTable<Compositor, Templates> _templates = new ConditionalWeakTable<Compositor, Templates>();
        private static readonly ConditionalWeakTable<Compositor, Templates>.CreateValueCallback _createTemplates = CreateTemplates;

        private static Templates CreateTemplates(Compositor compositor) => new Templates(compositor);

        #endregion

        private sealed class Session
        {
            private readonly MenuFlyout _flyout;
            private readonly FrameworkElement _target;
            private readonly FrameworkElement _placementTarget;
            private readonly FlyoutPlacementMode _placement;
            private readonly bool _overlap;
            private readonly double _cornerRadius;

            private Templates _templates;
            private Compositor _compositor;
            private CompositionPropertySet _props;
            private CompositionRoundedRectangleGeometry _geometry;
            private CompositionGeometricClip _clip;
            private ContainerVisual _shadows;
            private SpriteVisual _shadowFrom;
            private SpriteVisual _shadowTo;
            private ShapeVisual _stroke;
            private CompositionRoundedRectangleGeometry _strokeGeometry;
            private CompositionSpriteShape _strokeShape;
            private CompositionColorBrush _strokeBrush;
            private CompositionScopedBatch _batch;

            private MenuFlyoutPresenter _presenter;
            private FrameworkElement _body;
            private Visual _targetVisual;
            private Visual _presenterVisual;
            private Visual _bodyVisual;
            private Visual _contentVisual;

            // What the morph replaced, put back on close.
            private bool _areOpenCloseAnimationsEnabled;
            private Style _presenterStyle;
            private bool _presenterHitTestVisible;
            private float _targetOpacity;
            private Vector3 _presenterScale;
            private Vector3 _presenterCenterPoint;
            private CompositionClip _bodyClip;
            private float _bodyOpacity;
            private float _contentOpacity;
            private Vector3 _contentScale;
            private Vector3 _contentCenterPoint;
            private bool _padded;
            private Thickness _padding;
            private double _maxWidth;
            private double _maxHeight;

            private Alignment _horizontal;
            private Alignment _vertical;
            private Axis _growth;
            private double _strip;
            private double _room;
            private bool _stripAfter;
            private Rect _menu;
            private object _borderBrush;
            private PointerEventHandler _stripPressed;

            private bool _closing;
            private bool _closeAllowed;
            private bool _targetRestored;
            private bool _released;

            public Session(MenuFlyout flyout, FrameworkElement target, FrameworkElement placementTarget, FlyoutPlacementMode placement, bool overlap, double cornerRadius)
            {
                _flyout = flyout;
                _target = target;
                _placementTarget = placementTarget;
                _placement = placement;
                _overlap = overlap;
                _cornerRadius = cornerRadius;
            }

            public bool Show(FlyoutShowMode showMode)
            {
                _areOpenCloseAnimationsEnabled = _flyout.AreOpenCloseAnimationsEnabled;
                _flyout.AreOpenCloseAnimationsEnabled = false;

                _presenterStyle = _flyout.MenuFlyoutPresenterStyle;
                _flyout.MenuFlyoutPresenterStyle = GetShadowlessStyle(_presenterStyle);

                _flyout.Opened += OnOpened;
                _flyout.Closing += OnClosing;
                _flyout.Closed += OnClosed;

                _byFlyout.Add(_flyout, this);
                _byTarget.Add(_target, this);

                try
                {
                    // A MenuFlyout shown at a Position ignores Placement and opens like a context
                    // menu. Its size is unknown until it opens, so OnOpened moves it into place.
                    _flyout.ShowAt(_target, new FlyoutShowOptions
                    {
                        Position = new Point(0, 0),
                        ShowMode = showMode
                    });

                    return true;
                }
                catch
                {
                    Release();
                    return false;
                }
            }

            // Another show wants the target: finish now rather than fold back into it.
            public void Abort()
            {
                _closeAllowed = true;
                RestoreTarget();

                _byTarget.Remove(_target);
                _flyout.Hide();
            }

            // FlyoutBase raises Opened from the presenter's Loaded, after layout and before the frame
            // is committed, so the collapsed state set here is the first thing on screen.
            private void OnOpened(object sender, object e)
            {
                // Opened is the presenter's Loaded, which can be raised again while the menu is open.
                // A second setup would pad the already padded presenter and run two property sets
                // against the same visuals.
                if (_body != null)
                {
                    return;
                }

                _presenter = FindPresenter(_flyout);

                if (_presenter == null
                    || VisualTreeHelper.GetChildrenCount(_presenter) == 0
                    || !(VisualTreeHelper.GetChild(_presenter, 0) is FrameworkElement body)
                    || body.ActualWidth < 1
                    || body.ActualHeight < 1)
                {
                    // Shown without any animation; Closing lets it go.
                    return;
                }

                _body = body;
                _bodyVisual = ElementCompositionPreview.GetElementVisual(body);
                _bodyOpacity = _bodyVisual.Opacity;

                GetAlignment(_placement, out _horizontal, out _vertical, out _growth);

                // A presenter FlyoutBase has sized itself, to fit the screen, would not grow with
                // the padding below, and SizeChanged would never come: those overlap instead.
                if (_overlap || _growth == Axis.None || !double.IsNaN(_presenter.Width) || !double.IsNaN(_presenter.Height))
                {
                    Setup(0);
                    return;
                }

                // Beside the target. The popup is windowed: its HWND is the menu plus a shadow
                // gutter, and nothing is drawn past it, so to start over the target the menu has to
                // cover the target. Padding grows the presenter by a strip over the target on the side
                // facing it; the clip keeps the strip out of sight and the shape moves through it.
                // The resize makes FlyoutBase run its placement again from its own SizeChanged
                // handler, which would undo a move made before it. Ours can run first, so the rest
                // waits for the LayoutUpdated that follows, raised once every SizeChanged handler of
                // the pass has run, and still before the frame.
                var vertical = _growth == Axis.Vertical;
                var current = GetBounds(_target, body);
                var anchor = GetBounds(_placementTarget, body);
                var window = _target.XamlRoot.Content.TransformToVisual(body).TransformBounds(new Rect(new Point(0, 0), _target.XamlRoot.Size));

                // The side the menu lands on, flips included, is the side the strip goes on.
                var origin = vertical
                    ? Place(_vertical, anchor.Top, anchor.Bottom, body.ActualHeight, window.Top, window.Bottom, true)
                    : Place(_horizontal, anchor.Left, anchor.Right, body.ActualWidth, window.Left, window.Right, true);

                _stripAfter = vertical ? origin >= anchor.Bottom : origin >= anchor.Right;

                // From the menu's edge back to the far edge of the source, so the shape can start
                // over it: the placement target's length plus the margin when the two are one.
                _strip = vertical
                    ? (_stripAfter ? anchor.Bottom - current.Top : current.Bottom - anchor.Top) + BesideMargin
                    : (_stripAfter ? anchor.Right - current.Left : current.Right - anchor.Left) + BesideMargin;
                _room = Math.Ceiling(OvershootRoom * ((vertical ? body.ActualHeight : body.ActualWidth) + _strip)) + 1;

                _padded = true;
                _padding = _presenter.Padding;
                _maxWidth = _presenter.MaxWidth;
                _maxHeight = _presenter.MaxHeight;

                var padding = _padding;

                if (vertical)
                {
                    if (_stripAfter)
                    {
                        padding.Top += _strip;
                        padding.Bottom += _room;
                    }
                    else
                    {
                        padding.Bottom += _strip;
                        padding.Top += _room;
                    }

                    _presenter.MaxHeight = _maxHeight + _strip + _room;
                }
                else
                {
                    if (_stripAfter)
                    {
                        padding.Left += _strip;
                        padding.Right += _room;
                    }
                    else
                    {
                        padding.Right += _strip;
                        padding.Left += _room;
                    }

                    _presenter.MaxWidth = _maxWidth + _strip + _room;
                }

                _bodyVisual.Opacity = 0;
                _presenter.Padding = padding;
                _presenter.SizeChanged += OnPresenterSizeChanged;

                // The strip is the presenter's own background, so a press on it would land on the
                // menu instead of dismissing it. Handled too: nothing in the strip should handle it,
                // but a press there must close the menu whatever did.
                _stripPressed = OnStripPointerPressed;
                _presenter.AddHandler(UIElement.PointerPressedEvent, _stripPressed, true);
            }

            private void OnPresenterSizeChanged(object sender, SizeChangedEventArgs e)
            {
                _presenter.SizeChanged -= OnPresenterSizeChanged;
                _presenter.LayoutUpdated += OnStripLayoutUpdated;
            }

            private void OnStripLayoutUpdated(object sender, object e)
            {
                _presenter.LayoutUpdated -= OnStripLayoutUpdated;

                if (_released || _closing)
                {
                    return;
                }

                Setup(_strip);
            }

            private void OnStripPointerPressed(object sender, PointerRoutedEventArgs e)
            {
                if (_props == null || _closing || _menu.Contains(e.GetCurrentPoint(_body).Position))
                {
                    return;
                }

                e.Handled = true;
                _flyout.Hide();
            }

            private void Setup(double strip)
            {
                var body = _body;
                var content = FindContent(body);
                var width = body.ActualWidth;
                var height = body.ActualHeight;

                // Everything is in the body's own coordinates, which are logical: in right-to-left
                // the start edge is on the right, and Left/Right placements mirror with it.
                var xamlRoot = _target.XamlRoot;
                var current = GetBounds(_target, body);
                var anchor = GetBounds(_placementTarget, body);
                var window = xamlRoot.Content.TransformToVisual(body).TransformBounds(new Rect(new Point(0, 0), xamlRoot.Size));

                // The menu inside the presenter: all of it, or all of it but the strip, which reaches
                // from the menu's edge to the source's far edge.
                var menu = new Rect(0, 0, width, height);
                double x;
                double y;

                if (strip > 0 && _growth == Axis.Vertical)
                {
                    menu = _stripAfter ? new Rect(0, strip, width, height - strip - _room) : new Rect(0, _room, width, height - strip - _room);
                    x = Place(_horizontal, anchor.Left, anchor.Right, width, window.Left, window.Right, false);
                    y = _stripAfter ? current.Top : current.Bottom - height;
                }
                else if (strip > 0)
                {
                    menu = _stripAfter ? new Rect(strip, 0, width - strip - _room, height) : new Rect(_room, 0, width - strip - _room, height);
                    x = _stripAfter ? current.Left : current.Right - width;
                    y = Place(_vertical, anchor.Top, anchor.Bottom, height, window.Top, window.Bottom, false);
                }
                else
                {
                    x = Place(_horizontal, anchor.Left, anchor.Right, width, window.Left, window.Right, false);
                    y = Place(_vertical, anchor.Top, anchor.Bottom, height, window.Top, window.Bottom, false);
                }

                _menu = menu;

                // On the pixel grid, or layout rounds the popup's offset and the shape misses its
                // target by half a pixel; a centred placement lands between pixels routinely.
                var scale = xamlRoot.RasterizationScale;
                x = Math.Round(x * scale) / scale;
                y = Math.Round(y * scale) / scale;

                // The shape starts as the target's footprint, kept inside the presenter because a
                // clip cannot draw past its element.
                var fromWidth = Math.Min(current.Width, width);
                var fromHeight = Math.Min(current.Height, height);

                var from = new Rect(
                    Math.Max(0, Math.Min(current.X - x, width - fromWidth)),
                    Math.Max(0, Math.Min(current.Y - y, height - fromHeight)),
                    fromWidth,
                    fromHeight);

                if (_presenter.Parent is Popup popup)
                {
                    // The popup's offsets are physical in either flow direction - in right-to-left the
                    // framework only anchors them at the presenter's right edge - while x is logical.
                    popup.HorizontalOffset += body.FlowDirection == FlowDirection.RightToLeft ? -x : x;
                    popup.VerticalOffset += y;
                }
                else
                {
                    from = current;
                }

                var fromRadius = (float)Math.Min(Math.Min(from.Width, from.Height) / 2, _cornerRadius);
                var toRadius = (float)_presenter.CornerRadius.TopLeft;

                _presenterVisual = ElementCompositionPreview.GetElementVisual(_presenter);
                _targetVisual = ElementCompositionPreview.GetElementVisual(_target);
                _compositor = _bodyVisual.Compositor;
                _templates = MenuFlyoutMorph._templates.GetValue(_compositor, _createTemplates);

                _props = _compositor.CreatePropertySet();
                _props.InsertScalar("X", 0);
                _props.InsertScalar("Y", 0);
                _props.InsertVector4("From", new Vector4((float)from.Left, (float)from.Top, (float)from.Right, (float)from.Bottom));
                _props.InsertVector4("To", new Vector4((float)menu.Left, (float)menu.Top, (float)menu.Right, (float)menu.Bottom));
                _props.InsertVector2("Radius", new Vector2(fromRadius, toRadius));

                // The clip's expressions read it before AttachStroke knows whether it draws a stroke.
                _props.InsertScalar("Thickness", 0);

                _geometry = _compositor.CreateRoundedRectangleGeometry();
                Start(_geometry, "Offset", _templates.ClipOffset);
                Start(_geometry, "Size", _templates.ClipSize);
                Start(_geometry, "CornerRadius", _templates.ClipCornerRadius);

                _clip = _compositor.CreateGeometricClip(_geometry);
                _bodyClip = _bodyVisual.Clip;
                _bodyVisual.Clip = _clip;
                Start(_bodyVisual, "Opacity", _templates.BodyOpacity);

                var center = new Point(from.X + from.Width / 2, from.Y + from.Height / 2);

                if (content != null)
                {
                    var origin = content.TransformToVisual(body).TransformPoint(new Point(0, 0));

                    _contentVisual = ElementCompositionPreview.GetElementVisual(content);
                    _contentOpacity = _contentVisual.Opacity;
                    _contentScale = _contentVisual.Scale;
                    _contentCenterPoint = _contentVisual.CenterPoint;
                    _contentVisual.CenterPoint = new Vector3((float)(center.X - origin.X), (float)(center.Y - origin.Y), 0);
                    Start(_contentVisual, "Opacity", _templates.ContentOpacity);
                    Start(_contentVisual, "Scale", _templates.ContentScale);
                }

                var bodyOrigin = body.TransformToVisual(_presenter).TransformPoint(new Point(0, 0));

                _presenterScale = _presenterVisual.Scale;
                _presenterCenterPoint = _presenterVisual.CenterPoint;
                _presenterVisual.CenterPoint = new Vector3((float)(center.X + bodyOrigin.X), (float)(center.Y + bodyOrigin.Y), 0);
                Start(_presenterVisual, "Scale", _templates.PresenterScale);

                // Over the target, the target fades out under the body fading in; beside it, it stays.
                _targetOpacity = _targetVisual.Opacity;

                if (_overlap || _growth == Axis.None)
                {
                    Start(_targetVisual, "Opacity", _templates.TargetOpacity);
                }

                if (ElementCompositionPreview.GetElementChildVisual(_presenter) == null)
                {
                    AttachShadow(body.ActualTheme, fromRadius, toRadius, bodyOrigin);

                    if (strip > 0)
                    {
                        AttachStroke(body, width, height);
                    }
                }

                _presenterHitTestVisible = _presenter.IsHitTestVisible;

                Animate(false);
            }

            // The shadow ThemeShadow would have drawn, as ThemeShadowEx builds it. A hollow nine grid
            // is drawn on top of the presenter without covering it, so it needs no slot behind the
            // body and no template of ours. The radius is baked into the brush, so there is one per
            // end of the morph, crossfaded as the radius lerps.
            private void AttachShadow(ElementTheme theme, float fromRadius, float toRadius, Point bodyOrigin)
            {
                var insets = ThemeShadowEx.GetInsets(FlyoutElevation);
                _props.InsertVector4("Insets", new Vector4((float)insets.Left, (float)insets.Top, (float)insets.Right, (float)insets.Bottom));

                _shadows = _compositor.CreateContainerVisual();
                _shadows.Offset = new Vector3((float)bodyOrigin.X, (float)bodyOrigin.Y, 0);

                if (fromRadius != toRadius)
                {
                    _shadowFrom = CreateShadow(theme, fromRadius, _templates.ShadowFromOpacity);
                    _shadows.Children.InsertAtTop(_shadowFrom);
                }

                _shadowTo = CreateShadow(theme, toRadius, fromRadius != toRadius ? _templates.ShadowToOpacity : null);
                _shadows.Children.InsertAtTop(_shadowTo);

                Start(_shadows, "Opacity", _templates.BodyOpacity);
                ElementCompositionPreview.SetElementChildVisual(_presenter, _shadows);
            }

            // With the strip, the template's Border strokes the whole presenter, strip included: the
            // menu's own edge facing the target has no stroke, and the clip cuts the side strokes off
            // at its rounded corners. The stroke is drawn here instead, around the shape, with the
            // template's colour and thickness. BorderThickness stays, so layout does not change.
            private void AttachStroke(FrameworkElement body, double width, double height)
            {
                if (!(body is Border border) || !(border.BorderBrush is SolidColorBrush brush) || border.BorderThickness.Left <= 0)
                {
                    return;
                }

                var thickness = (float)border.BorderThickness.Left;
                var color = brush.Color;
                color.A = (byte)(color.A * brush.Opacity);

                _props.InsertScalar("Inset", thickness / 2);
                _props.InsertScalar("Thickness", thickness);

                _strokeGeometry = _compositor.CreateRoundedRectangleGeometry();
                Start(_strokeGeometry, "Offset", _templates.StrokeOffset);
                Start(_strokeGeometry, "Size", _templates.StrokeSize);
                Start(_strokeGeometry, "CornerRadius", _templates.StrokeCornerRadius);

                _strokeBrush = _compositor.CreateColorBrush(color);
                _strokeShape = _compositor.CreateSpriteShape(_strokeGeometry);
                _strokeShape.StrokeBrush = _strokeBrush;
                _strokeShape.StrokeThickness = thickness;

                _stroke = _compositor.CreateShapeVisual();
                _stroke.Size = new Vector2((float)width, (float)height);
                _stroke.Shapes.Add(_strokeShape);
                _shadows.Children.InsertAtTop(_stroke);

                // The presenter's value, not the Border's: the template binds the Border to it.
                _borderBrush = _presenter.ReadLocalValue(Control.BorderBrushProperty);
                _presenter.BorderBrush = null;
            }

            private SpriteVisual CreateShadow(ElementTheme theme, float radius, ExpressionAnimation opacity)
            {
                var sprite = _compositor.CreateSpriteVisual();
                sprite.Brush = _templates.GetShadow(_compositor, theme, radius);

                Start(sprite, "Offset", _templates.ShadowOffset);
                Start(sprite, "Size", _templates.ShadowSize);

                if (opacity != null)
                {
                    Start(sprite, "Opacity", opacity);
                }

                return sprite;
            }

            private void OnClosing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
            {
                if (_closeAllowed || _props == null)
                {
                    return;
                }

                args.Cancel = true;

                if (_closing)
                {
                    return;
                }

                _closing = true;
                _presenter.IsHitTestVisible = false;

                Animate(true);
            }

            private void Animate(bool closing)
            {
                _batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

                if (closing)
                {
                    _props.StartAnimation("X", _templates.CloseX);
                    _props.StartAnimation("Y", _templates.CloseY);
                }
                else
                {
                    _props.StartAnimation("X", _templates.OpenX);
                    _props.StartAnimation("Y", _templates.OpenY);
                }

                _batch.End();
                _batch.Completed += OnCompleted;
            }

            private void OnCompleted(object sender, CompositionBatchCompletedEventArgs args)
            {
                var batch = (CompositionScopedBatch)sender;
                batch.Completed -= OnCompleted;

                // An open superseded by the close completes too.
                if (batch != _batch || !_closing || _released)
                {
                    return;
                }

                _closeAllowed = true;
                _flyout.Hide();
            }

            private void OnClosed(object sender, object e)
            {
                Release();
            }

            private void RestoreTarget()
            {
                if (_targetRestored || _targetVisual == null)
                {
                    return;
                }

                _targetRestored = true;
                _targetVisual.StopAnimation("Opacity");
                _targetVisual.Opacity = _targetOpacity;
            }

            private void Release()
            {
                if (_released)
                {
                    return;
                }

                _released = true;

                _flyout.Opened -= OnOpened;
                _flyout.Closing -= OnClosing;
                _flyout.Closed -= OnClosed;
                _flyout.AreOpenCloseAnimationsEnabled = _areOpenCloseAnimationsEnabled;
                _flyout.MenuFlyoutPresenterStyle = _presenterStyle;

                _byFlyout.Remove(_flyout);

                if (_byTarget.TryGetValue(_target, out var owner) && owner == this)
                {
                    _byTarget.Remove(_target);
                }

                RestoreTarget();

                if (_bodyVisual != null)
                {
                    _bodyVisual.StopAnimation("Opacity");
                    _bodyVisual.Opacity = _bodyOpacity;
                    _bodyVisual.Clip = _bodyClip;
                }

                if (_contentVisual != null)
                {
                    _contentVisual.StopAnimation("Opacity");
                    _contentVisual.StopAnimation("Scale");
                    _contentVisual.Opacity = _contentOpacity;
                    _contentVisual.Scale = _contentScale;
                    _contentVisual.CenterPoint = _contentCenterPoint;
                }

                if (_presenterVisual != null)
                {
                    _presenterVisual.StopAnimation("Scale");
                    _presenterVisual.Scale = _presenterScale;
                    _presenterVisual.CenterPoint = _presenterCenterPoint;
                }

                if (_padded)
                {
                    _presenter.SizeChanged -= OnPresenterSizeChanged;
                    _presenter.LayoutUpdated -= OnStripLayoutUpdated;
                    _presenter.RemoveHandler(UIElement.PointerPressedEvent, _stripPressed);
                    _presenter.Padding = _padding;
                    _presenter.MaxWidth = _maxWidth;
                    _presenter.MaxHeight = _maxHeight;
                }

                if (_stroke != null)
                {
                    if (_borderBrush == DependencyProperty.UnsetValue)
                    {
                        _presenter.ClearValue(Control.BorderBrushProperty);
                    }
                    else
                    {
                        _presenter.SetValue(Control.BorderBrushProperty, _borderBrush);
                    }
                }

                if (_presenter != null && _props != null)
                {
                    _presenter.IsHitTestVisible = _presenterHitTestVisible;

                    if (_shadows != null)
                    {
                        ElementCompositionPreview.SetElementChildVisual(_presenter, null);
                    }
                }

                // Proxy objects in the compositor's process, released at once rather than whenever
                // the finalizer gets to the wrappers. Everything still animating against the
                // property set goes before it: an expression whose reference is gone fails at commit.
                _shadowFrom?.Dispose();
                _shadowTo?.Dispose();
                _stroke?.Dispose();
                _strokeShape?.Dispose();
                _strokeGeometry?.Dispose();
                _strokeBrush?.Dispose();
                _shadows?.Dispose();
                _clip?.Dispose();
                _geometry?.Dispose();
                _props?.Dispose();
                _batch?.Dispose();
            }

            private void Start(CompositionObject target, string property, ExpressionAnimation animation)
            {
                animation.SetReferenceParameter("p", _props);
                target.StartAnimation(property, animation);
            }
        }

        // The glyph buttons draw their background on a template root inset by GlyphButtonPadding, so
        // what the user sees as the button, and what an overlapping menu has to grow out of, is that
        // root rather than the control's bounds.
        private static Rect GetVisibleBounds(FrameworkElement target)
        {
            if (target is GlyphButton or AnimatedGlyphButton or GlyphToggleButton or AnimatedGlyphToggleButton
                && VisualTreeHelper.GetChildrenCount(target) > 0
                && VisualTreeHelper.GetChild(target, 0) is FrameworkElement root)
            {
                var margin = root.Margin;
                return new Rect(
                    margin.Left,
                    margin.Top,
                    Math.Max(0, target.ActualWidth - margin.Left - margin.Right),
                    Math.Max(0, target.ActualHeight - margin.Top - margin.Bottom));
            }

            return new Rect(0, 0, target.ActualWidth, target.ActualHeight);
        }

        private static Rect GetBounds(FrameworkElement element, UIElement relativeTo)
        {
            return element.TransformToVisual(relativeTo).TransformBounds(GetVisibleBounds(element));
        }

        private static double GetCornerRadius(FrameworkElement element)
        {
            return element switch
            {
                Control control => control.CornerRadius.TopLeft,
                Border border => border.CornerRadius.TopLeft,
                _ => 0
            };
        }

        private static MenuFlyoutPresenter FindPresenter(MenuFlyout flyout)
        {
            DependencyObject node = flyout.Items[0];

            while (node != null && !(node is MenuFlyoutPresenter))
            {
                node = VisualTreeHelper.GetParent(node);
            }

            return node as MenuFlyoutPresenter;
        }

        // The template part MenuFlyoutPresenter itself looks up, so a custom template that keeps the
        // menu working keeps it too. Missing, the items simply are not faded.
        private static FrameworkElement FindContent(DependencyObject root)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(root, i) is FrameworkElement child)
                {
                    if (child.Name == "MenuFlyoutPresenterScrollViewer")
                    {
                        return child;
                    }

                    if (!(child is ItemsPresenter) && FindContent(child) is FrameworkElement found)
                    {
                        return found;
                    }
                }
            }

            return null;
        }
    }
}
