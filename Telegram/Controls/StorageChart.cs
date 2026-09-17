//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Telegram.Charts;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    // The storage and network donut, ported from Android's org.telegram.ui.Components.CacheChart.
    //
    // It owns the whole of what the pages used to assemble around it: the ring, the percentage on
    // each sector, the size in the middle, the indeterminate state, the empty state, and the icons
    // drifting out through the ring (StorageChartParticles). A page gives it a list and a background
    // colour and nothing else.
    //
    // NOTHING HERE RUNS PER FRAME. The original drives all of it from its draw pass; doing the same
    // from CompositionTarget.Rendering is a frame of ~90 property writes, eleven of them through XAML
    // element visuals, so the XAML tree is dirtied every frame on top of the compositor's own work,
    // and it is visibly laggy. Instead:
    //
    //  - two scalars, Loading and Complete, are the only things a state change animates, and they
    //    are ordinary key frame animations
    //  - the spinner window is sampled into one looping pair of key frame animations (see StartSpin)
    //    because getSegments is a sum of four eased terms with no closed form
    //  - the ring is ExpressionAnimations over those two: thickness, radius, both trims, and each
    //    label's position, scale and opacity
    //  - the particles are key frames of their own, rebuilt when the sectors settle
    //
    // So the UI thread touches the chart only when the data changes, and the animation keeps running
    // while the thread is blocked. Two things cannot be animated or expressed and are done on a
    // single timer instead: the stroke cap, and the gradient, which cannot afford to be on a ring
    // whose trims are being re-cut every frame.
    public partial class StorageChart : Grid
    {
        // The original's dp figures. A UWP effective pixel is the same thing, so they carry over
        // as-is.
        private const float Diameter = 172;

        // The ring's own surface. Wide enough for a selected sector, which grows outwards by
        // SelectGrow, plus two pixels so the outer edge is not clipped by the ShapeVisual it lives
        // in.
        private const float Box = Diameter + SelectGrow * 2 + 4;
        private const float ControlHeight = 200;
        private const float ThicknessLoaded = 38;
        private const float ThicknessLoading = 10;

        // The gap between sectors, in degrees. A gap measured in degrees is a wedge: at 2 degrees
        // it is 1.7px across at the ring's inner edge and 3.0px at the outer, because those radii
        // are 48 and 86 - it visibly flares. The original softens it by insetting each sector's
        // inner arc by a quarter of the separator, which makes the sides slightly non-radial, and a
        // stroke cannot do that at all: both ends of a flat-capped arc are chords perpendicular to
        // the tangent, which is to say radial lines.
        //
        // So the even mode takes the gap out of the layout entirely - the sectors abut - and draws
        // the separators as radial lines of a constant pixel width on top, in the colour behind the
        // chart. Constant width, all the way across the ring. The catch is that it only works over
        // a solid background, which the settings page is.
        private const float WedgeSeparator = 2;
        private const float SeparatorWidth = 2.5f;
        private const float GlyphSize = 15;
        private const int SectorCount = 11;

        // A composition ellipse geometry trims from twelve o'clock, clockwise; the angles every
        // sector is laid out in run from three. Every rotation crosses this. If the ring ever comes
        // out turned a quarter, this is the line to change.
        private const float TrimZero = 90;

        private const double LoadingDuration = 750;
        private const double CompleteDuration = 650;
        private const double AngleDuration = 650;
        private const double TextDuration = 150;
        private const double SelectDuration = 200;

        // A selected sector grows outwards only - the outer edge moves out, the inner edge stays.
        // On a stroke that is half of it on the radius and all of it on the width.
        private const float SelectGrow = 9;

        // How far past the rim a pointer still counts as being in the ring.
        private const float HitSlop = 14;

        // CacheChart's DEFAULT_COLORS. The app's own palette in LineViewData agrees on the seven
        // it has.
        private static readonly Color[] DefaultColors =
        {
            Color.FromArgb(0xFF, 0x58, 0xA8, 0xED), // lightblue
            Color.FromArgb(0xFF, 0x32, 0x7F, 0xE5), // blue
            Color.FromArgb(0xFF, 0x61, 0xC7, 0x52), // green
            Color.FromArgb(0xFF, 0x9F, 0x79, 0xE8), // purple
            Color.FromArgb(0xFF, 0x8F, 0xCF, 0x39), // lightgreen
            Color.FromArgb(0xFF, 0xE0, 0x53, 0x56), // red
            Color.FromArgb(0xFF, 0xE3, 0xB7, 0x27), // orange
            Color.FromArgb(0xFF, 0x40, 0xD0, 0xCA), // cyan
            Color.FromArgb(0xFF, 0x9F, 0x79, 0xE8), // purple
            Color.FromArgb(0xFF, 0xDE, 0xBA, 0x08), // golden
            Color.FromArgb(0xFF, 0xDE, 0xBA, 0x08), // golden
        };

        // CircularProgressDrawable.getSegments, the Material indeterminate spinner expressed as a
        // pair of angles over a 5400ms period. The chart runs eleven of these 80ms apart and clamps
        // each to the window of the first, which is what gives the loading state its stacked look.
        //
        // A sum of four eased terms over a linear ramp: no closed form, which is why it is sampled
        // into key frames rather than expressed.
        private static class CircularProgress
        {
            public const float Period = 5400;

            // Elapsed time is scaled by this before being taken modulo the period, so one turn of
            // the spinner is nine seconds of wall clock.
            public const float Rate = .6f;

            // FastOutSlowInInterpolator, which is what CircularProgressDrawable uses. It is a path
            // interpolator there, sampled from these same control points.
            private static readonly CubicBezierInterpolator _fastOutSlowIn = new(.4, 0, .2, 1);

            public static void GetSegments(float t, out float from, out float to)
            {
                from = Math.Max(0, 1520 * t / Period - 20);
                to = 1520 * t / Period;

                for (int i = 0; i < 4; i++)
                {
                    to += Ease((t - i * 1350) / 667f) * 250;
                    from += Ease((t - (667 + i * 1350)) / 667f) * 250;
                }
            }

            // A PathInterpolator clamps its input; CubicBezierInterpolator does not, and getSegments
            // hands it values well outside [0, 1] - at which point solving for x diverges.
            private static float Ease(float t)
            {
                return t <= 0 ? 0 : t >= 1 ? 1 : _fastOutSlowIn.getInterpolation(t);
            }
        }

        private struct GlyphSurface
        {
            public CompositionSurfaceBrush Brush;
            public Vector2 Size;
        }

        private sealed class Sector
        {
            public CompositionEllipseGeometry Geometry;
            public CompositionSpriteShape Shape;
            public CompositionRadialGradientBrush Gradient;
            public CompositionColorBrush Solid;
            public CompositionColorGradientStop Stop0;
            public CompositionColorGradientStop Stop1;

            // Settled: where the sector lands once there is data. Blend: that crossfaded with its
            // share of the spinner's window, which is what everything else reads.
            public CompositionPropertySet Settled;
            public CompositionPropertySet Blend;

            // The sector's place on the ring, in degrees, for hit testing.
            public float From;
            public float To;

            public CompositionLineGeometry Line;
            public CompositionSpriteShape Separator;

            public TextBlock Label;
            public Visual LabelVisual;
        }

        private readonly Compositor _compositor;
        private readonly CompositionPropertySet _state;
        private readonly CompositionPropertySet _chart;

        // The spinner's window - its minimum and maximum angle. Every sector takes a fixed share of
        // it while loading, so this is the only thing the sampled animation drives.
        private readonly CompositionPropertySet _window;


        private readonly CompositionEasingFunction _linear;
        private readonly CompositionEasingFunction _easeOutQuint;
        private readonly CompositionEasingFunction _easeOut;

        private readonly Border _ringHost;
        private readonly Border _particleHost;
        private readonly Canvas _glyphHost;
        private readonly StackPanel _centerHost;
        private readonly TextBlock _topText;
        private readonly TextBlock _bottomText;
        // One source TextBlock and one frozen surface per distinct icon, keyed by its code point.
        private readonly Dictionary<string, TextBlock> _glyphSources = new Dictionary<string, TextBlock>();
        private readonly Dictionary<string, GlyphSurface> _glyphSurfaces = new Dictionary<string, GlyphSurface>();
        private readonly Canvas _checkHost;

        private readonly ContainerVisual _root;
        private readonly ShapeVisual _trackVisual;
        private readonly ShapeVisual _shapeVisual;
        private readonly ShapeVisual _separatorVisual;
        private readonly CompositionColorBrush _separatorBrush;
        private readonly CompositionColorBrush _trackBrush;
        private readonly ShapeVisual _completeVisual;
        private readonly ContainerVisual _particleRoot;

        private readonly CompositionEllipseGeometry _trackEllipse;
        private readonly CompositionSpriteShape _trackShape;

        private readonly CompositionEllipseGeometry _completeEllipse;
        private readonly CompositionSpriteShape _completeShape;

        private readonly Sector[] _sectors = new Sector[SectorCount];
        private readonly StorageChartParticles _particles;

        // The two things a transition needs that are neither animatable nor expressible: the
        // stroke cap, and the gradient that cannot afford to be on while the geometry moves. One
        // timer puts both back when the transition lands. See Morph.
        private readonly DispatcherTimer _morphTimer = new DispatcherTimer();
        private bool _roundPending;

        private bool _loading = true;
        private bool _complete;
        private bool _spinning;
        private ScalarKeyFrameAnimation _spinFrom;
        private ScalarKeyFrameAnimation _spinTo;
        private bool _even;
        private int _tiled = -1;
        private int _selected = -1;

        private IList<StorageChartItem> _items;

        private Vector2 _center;
        private Vector2 _clipCenter;


        public StorageChart()
        {
            _compositor = BootStrapper.Current.Compositor;

            _linear = _compositor.CreateLinearEasingFunction();
            // The control points Telegram.Charts.CubicBezierInterpolator carries as EaseOutQuint
            // and EaseOut. That class solves the curve on the CPU, and what is wanted here is for
            // the compositor to have it.
            _easeOutQuint = _compositor.CreateCubicBezierEasingFunction(new Vector2(.23f, 1), new Vector2(.32f, 1));
            _easeOut = _compositor.CreateCubicBezierEasingFunction(new Vector2(0, 0), new Vector2(.58f, 1));

            // The only two values a state change writes. Everything else hangs off them.
            _state = _compositor.CreatePropertySet();
            _state.InsertScalar("Loading", 1);
            _state.InsertScalar("Complete", 0);
            _state.InsertScalar("Particles", 1);

            _chart = _compositor.CreatePropertySet();
            _chart.InsertScalar("Diameter", Diameter);
            _chart.InsertScalar("Thickness", ThicknessLoading);
            _chart.InsertScalar("Separator", 0);
            _chart.InsertScalar("Hidden", 0);

            RestoreThickness();

            // The spinner's window - its minimum and maximum angle. Every sector takes a fixed
            // share of it while loading, so this is the only thing the sampled animation drives.
            _window = _compositor.CreatePropertySet();
            _window.InsertScalar("From", 0);
            _window.InsertScalar("To", 0);

            _root = _compositor.CreateContainerVisual();
            _trackVisual = _compositor.CreateShapeVisual();
            _shapeVisual = _compositor.CreateShapeVisual();
            _separatorVisual = _compositor.CreateShapeVisual();
            _completeVisual = _compositor.CreateShapeVisual();

            _root.Children.InsertAtTop(_trackVisual);
            _root.Children.InsertAtTop(_shapeVisual);
            _root.Children.InsertAtTop(_separatorVisual);
            _root.Children.InsertAtTop(_completeVisual);

            _separatorBrush = _compositor.CreateColorBrush(Colors.Black);

            // Off while loading, where the sectors tile the window and have no real boundaries, and
            // off in the complete state, where there are no sectors.
            _separatorVisual.StartAnimation("Opacity", Expression(
                "C.Separator * (1 - S.Loading) * (1 - S.Complete)"));

            _trackVisual.StartAnimation("Opacity", Expression("S.Loading"));

            // The sector angles are left where they are when the cache turns out to be empty, and
            // the ring thins to 10px and fades while the green ring and the tick come up behind it.
            // Unwinding every trim to zero at the same time, which is what this used to do, reads as
            // the chart falling apart rather than being replaced.
            _shapeVisual.StartAnimation("Opacity", Expression("1 - S.Complete"));
            _completeVisual.StartAnimation("Opacity", Expression("S.Complete"));

            // The track is only ever on screen at Loading = 1, where the morph's thickness is
            // exactly ThicknessLoading - so it is a fixed circle that fades, not one more piece of
            // geometry regenerated on every frame of the transition. Same for the complete ring,
            // which is only on screen at Complete = 1.
            _trackEllipse = _compositor.CreateEllipseGeometry();
            _trackEllipse.Radius = new Vector2((Diameter - ThicknessLoading) / 2);

            _trackShape = _compositor.CreateSpriteShape(_trackEllipse);
            _trackShape.StrokeBrush = _trackBrush = _compositor.CreateColorBrush();
            _trackShape.StrokeThickness = ThicknessLoading;
            _trackVisual.Shapes.Add(_trackShape);

            for (int i = 0; i < SectorCount; i++)
            {
                _sectors[i] = CreateSector(DefaultColors[i]);

                _shapeVisual.Shapes.Add(_sectors[i].Shape);
                _separatorVisual.Shapes.Add(_sectors[i].Separator);
            }

            for (int i = 0; i < SectorCount; i++)
            {
                BindSector(_sectors[i]);
            }

            Tile(SectorCount);

            _completeEllipse = _compositor.CreateEllipseGeometry();
            _completeEllipse.Radius = new Vector2((Diameter - ThicknessLoading) / 2);

            _completeShape = _compositor.CreateSpriteShape(_completeEllipse);
            _completeShape.StrokeBrush = _compositor.CreateColorBrush(Color.FromArgb(0xFF, 0x55, 0xC6, 0x63));
            _completeShape.StrokeThickness = ThicknessLoading;
            _completeVisual.Shapes.Add(_completeShape);

            _ringHost = new Border();
            ElementComposition.SetElementChildVisual(_ringHost, _root);

            _particleRoot = _compositor.CreateContainerVisual();
            _particleHost = new Border();
            ElementComposition.SetElementChildVisual(_particleHost, _particleRoot);

            _particles = new StorageChartParticles(_compositor, _particleRoot, _state)
            {
                Count = 52
            };

            // The source the particle brush is rasterized from.
            //
            // It was clipped to nothing to begin with, on the theory that a VisualSurface renders
            // its source visual's own content and an ancestor's clip does not reach into it. It
            // does: the surface came back empty and the particles were invisible unless the clip
            // was dropped. XAML prunes a subtree it can see is clipped away before there is any
            // content to capture. So the host is hidden on its composition visual instead, which
            // XAML knows nothing about and cannot optimize against.
            _glyphHost = new Canvas
            {
                IsHitTestVisible = false
            };

            // Driven by an expression rather than assigned, because UIElement.Opacity is pushed
            // down onto this same visual on every render walk and would put the sources back on
            // screen. A composition property that an animation owns ignores a plain write.
            ElementComposition.GetElementVisual(_glyphHost)
                .StartAnimation("Opacity", Expression("C.Hidden"));

            // The same two styles the pages used when they owned these labels themselves, so the
            // chart reads exactly as it did before it took them over.
            _topText = new TextBlock
            {
                Style = BootStrapper.Current.Resources["PopupTextBlockStyle"] as Style,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, -4)
            };

            _bottomText = new TextBlock
            {
                Style = BootStrapper.Current.Resources["InfoBodyTextBlockStyle"] as Style,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            _centerHost = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            _centerHost.Children.Add(_topText);
            _centerHost.Children.Add(_bottomText);

            var check = new Windows.UI.Xaml.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x55, 0xC6, 0x63)),
                StrokeThickness = 10,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.None,
                Data = BuildCheck()
            };

            // A Canvas, because the tick's points are absolute inside the chart box and a Canvas is
            // the one panel that will not shift a child to its own bounding box.
            _checkHost = new Canvas
            {
                Width = Diameter,
                Height = Diameter,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            _checkHost.Children.Add(check);

            // A panel with nothing painted in it is not hit-testable, and the ring is a child visual
            // rather than XAML content, so there is nothing else to hit.
            Background = new SolidColorBrush(Colors.Transparent);

            // Every category the ring draws is listed underneath it, with its name and its size, so
            // there is nothing here a screen reader can reach that it cannot reach better below.
            AutomationProperties.SetAccessibilityView(this, Windows.UI.Xaml.Automation.Peers.AccessibilityView.Raw);

            Children.Add(_ringHost);
            Children.Add(_particleHost);

            foreach (var sector in _sectors)
            {
                Children.Add(sector.Label);
            }

            Children.Add(_checkHost);
            Children.Add(_centerHost);
            Children.Add(_glyphHost);

            // Opacity on a XAML element's visual is only overwritten if UIElement.Opacity is
            // assigned, and nothing here assigns it, so an expression can own it outright.
            ElementComposition.GetElementVisual(_centerHost)
                .StartAnimation("Opacity", Expression("(1 - S.Loading) * (1 - S.Complete)"));

            ElementComposition.GetElementVisual(_checkHost)
                .StartAnimation("Opacity", Expression("S.Complete"));

            _morphTimer.Tick += OnMorphTick;

            ActualThemeChanged += OnActualThemeChanged;
            UpdateTrack();


            PointerMoved += OnPointerMoved;
            PointerExited += OnPointerExited;
            PointerCanceled += OnPointerCanceled;
            PointerReleased += OnPointerReleased;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            StartSpin();
            Morph(false);
        }

        #region Expressions

        private const string RadiusExpression =
            "Vector2((C.Diameter - C.Thickness) / 2 + T.Selected * " + Half + ", " +
            "(C.Diameter - C.Thickness) / 2 + T.Selected * " + Half + ")";

        private const string ThicknessExpression2 = "C.Thickness + T.Selected * " + Grow;

        // As strings, because a float written into an expression under a comma decimal locale comes
        // out as "4,5" and the parser will not have it.
        private const string Grow = "9";
        private const string Half = "4.5";
        private const string SelectText = "0.15";

        private const string ThicknessExpression = "Lerp(38, 10, Max(S.Loading, S.Complete))";

        // Every expression in the control gets the same three references, so they can be written
        // against C (chart metrics), S (state) and - where a sector supplies them - P, M, T and B.
        private ExpressionAnimation Expression(string expression)
        {
            var animation = StateExpression(expression);
            animation.SetReferenceParameter("C", _chart);

            return animation;
        }

        // Without C, for the two properties of _chart that are themselves expressions: handing an
        // animation a reference to the set it is being applied to is a cycle waiting to be called
        // one.
        private ExpressionAnimation StateExpression(string expression)
        {
            var animation = _compositor.CreateExpressionAnimation(expression);
            animation.SetReferenceParameter("S", _state);

            return animation;
        }

        // The share of the spinner's window each sector holds while loading.
        //
        // Sectors the caller did not fill have to get none. Left with an eleventh each they stay
        // full-thickness arcs for the whole blend - their size lerps from that share down to zero
        // while their centre travels to the end of the ring - so they sweep right across it, in
        // whatever colour the default palette left them. Two of them are golden, which is also the
        // colour of Videos, so they read as the big sector having grown a notch.
        private void Tile(int count)
        {
            if (_tiled == count)
            {
                return;
            }

            _tiled = count;

            for (int i = 0; i < SectorCount; i++)
            {
                BindBlend(
                    _sectors[i],
                    i < count ? (float)i / count : 1,
                    i < count ? (float)(i + 1) / count : 1);
            }
        }

        // While loading, the sectors tile the spinner's window rather than each running its own arc
        // 80ms behind the last, the way the original stacks them.
        //
        // That is what makes the settle clean. Interpolating a sector's centre and half-width is
        // interpolating its two boundaries, so the ring stays partitioned at every intermediate
        // frame as long as both ends of the blend are partitions. The settled pie is one; eleven
        // staggered arcs overlapping inside a window are not, and starting from those is what made
        // the ring arrive in pieces. A tiled window is, so the whole settle is one unroll of the
        // same arrangement, from the window out to the full circle.
        private void BindBlend(Sector sector, float w0, float w1)
        {
            // The spinner's angles are unbounded, so the settled target is lifted by however many
            // turns the window has taken before the two are blended - otherwise the ring unwinds
            // backwards through every lap it has done.
            var center = _compositor.CreateExpressionAnimation(
                "Lerp(T.Center + Floor(M.To / 360) * 360, M.From + W * (M.To - M.From), S.Loading)");

            var size = _compositor.CreateExpressionAnimation(
                "Lerp(T.Size, W * (M.To - M.From), S.Loading)");

            center.SetScalarParameter("W", (w0 + w1) / 2);
            size.SetScalarParameter("W", (w1 - w0) / 2);

            foreach (var animation in new[] { center, size })
            {
                animation.SetReferenceParameter("M", _window);
                animation.SetReferenceParameter("T", sector.Settled);
                animation.SetReferenceParameter("S", _state);
            }

            sector.Blend.StartAnimation("Center", center);
            sector.Blend.StartAnimation("Size", size);
        }

        private void BindSector(Sector sector)
        {
            sector.Geometry.StartAnimation("Radius", SectorExpression(sector, RadiusExpression));
            sector.Geometry.StartAnimation("TrimEnd", SectorExpression(sector, "Min(B.Size * 2 / 360, 1)"));

            // The sector is placed by rotating its shape, not by offsetting its trim.
            //
            // A trim that runs past the geometry's path origin is drawn as two arcs, and they do not
            // meet: there is a hairline of background between them, the full width of the ring. The
            // origin is a fixed point on screen, so that showed up as a black radial line in the
            // same place every time, through whichever sector happened to be crossing twelve
            // o'clock. Leaving TrimOffset at zero keeps every sector's trim inside [0, 1] and
            // nothing ever wraps; the shape carries the angle instead, the way the separators
            // already did.
            //
            // The trim starts at twelve o'clock and the sector angles start at three, which is what
            // the offset is for.
            sector.Shape.StartAnimation("RotationAngleInDegrees",
                SectorExpression(sector, "B.Center - B.Size + " + TrimZero));

            // No test for a collapsed sector: a zero length arc only paints a dot under a round cap,
            // and round caps now exist solely in the settled loading state, where every sector the
            // caller filled holds a share of the window and none of them is zero.
            sector.Shape.StartAnimation("StrokeThickness", SectorExpression(sector, ThicknessExpression2));

            // Out with the sector when it grows: the label radius is halfway between the two edges,
            // and only the outer one moves on selection, so the label moves by half of what it does.
            sector.LabelVisual.StartAnimation("Translation", SectorExpression(sector,
                "Vector3(" +
                "Cos(B.Center * 0.017453292) * ((C.Diameter - C.Thickness) / 2 + T.Selected * " + Half + "), " +
                "Sin(B.Center * 0.017453292) * ((C.Diameter - C.Thickness) / 2 + T.Selected * " + Half + "), 0)"));

            // Angle zero is three o'clock and the line is drawn along +X, so the sector's start
            // boundary is the rotation with no correction.
            sector.Separator.StartAnimation("RotationAngleInDegrees", SectorExpression(sector, "B.Center - B.Size"));
            // Invariant, because a float interpolated into an expression string under a comma
            // decimal locale writes "2,5" and the parser will not have it.
            sector.Separator.StartAnimation("StrokeThickness", SectorExpression(sector,
                "B.Size > 0.0001 ? " + SeparatorWidth.ToString(CultureInfo.InvariantCulture) + " : 0"));

            sector.LabelVisual.StartAnimation("Opacity", SectorExpression(sector, "T.TextAlpha * (1 - S.Loading) * (1 - S.Complete)"));
            // Ours alone: textScale is otherwise the sector's share and nothing else, but the ring
            // grows by a quarter of its width on selection, and a label that stays put against that
            // reads as a mistake. Set SelectText to 0 to drop it.
            sector.LabelVisual.StartAnimation("Scale", SectorExpression(sector,
                "Vector3(T.TextScale * (1 + T.Selected * " + SelectText + "), " +
                "T.TextScale * (1 + T.Selected * " + SelectText + "), 1)"));
        }

        private ExpressionAnimation SectorExpression(Sector sector, string expression)
        {
            var animation = Expression(expression);
            animation.SetReferenceParameter("B", sector.Blend);
            animation.SetReferenceParameter("T", sector.Settled);

            return animation;
        }

        #endregion

        #region Construction

        private Sector CreateSector(Color color)
        {
            var sector = new Sector();

            // Each sector is filled with a radial gradient centred on the chart, running from a
            // slightly lightened colour at 30% of the 86px radius out to the flat colour at the rim.
            // On a stroke that band is most of the ring's width at 38px thick, so it shows.
            sector.Stop0 = _compositor.CreateColorGradientStop(.3f, BlendOver(color, Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)));
            sector.Stop1 = _compositor.CreateColorGradientStop(1f, BlendOver(color, Color.FromArgb(0x03, 0x00, 0x00, 0x00)));

            sector.Gradient = _compositor.CreateRadialGradientBrush();
            sector.Gradient.MappingMode = CompositionMappingMode.Absolute;
            sector.Gradient.EllipseRadius = new Vector2(86);
            sector.Gradient.ColorStops.Add(sector.Stop0);
            sector.Gradient.ColorStops.Add(sector.Stop1);

            sector.Solid = _compositor.CreateColorBrush(color);

            sector.Settled = _compositor.CreatePropertySet();
            sector.Settled.InsertScalar("Center", 0);
            sector.Settled.InsertScalar("Size", 0);
            sector.Settled.InsertScalar("TextAlpha", 0);
            sector.Settled.InsertScalar("TextScale", 1);
            sector.Settled.InsertScalar("Selected", 0);

            sector.Blend = _compositor.CreatePropertySet();
            sector.Blend.InsertScalar("Center", 0);
            sector.Blend.InsertScalar("Size", 0);

            sector.Geometry = _compositor.CreateEllipseGeometry();
            sector.Geometry.TrimStart = 0;
            sector.Geometry.TrimEnd = 0;

            sector.Shape = _compositor.CreateSpriteShape(sector.Geometry);
            sector.Shape.StrokeBrush = sector.Gradient;

            // A composition ellipse is four bezier segments, and a trimmed arc keeps the joins
            // between them. CompositionSpriteShape defaults to a mitre with a limit of 1, which is
            // low enough to bevel every join on a curve - invisible on the 8px ring StorageChart
            // draws, a faceted outer edge on a 38px one.
            sector.Shape.StrokeLineJoin = CompositionStrokeLineJoin.Round;
            sector.Shape.StrokeMiterLimit = 10;

            // A fixed box so the scale has a fixed centre point and the text never relayouts the
            // chart when its digits change.
            sector.Label = new TextBlock
            {
                Width = 48,
                Height = 20,
                FontSize = 15,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            // A radial line at the sector's start boundary. It is stroked at a fixed pixel width,
            // which is the whole point, and the shape carries the rotation so the geometry itself
            // never changes.
            sector.Line = _compositor.CreateLineGeometry();

            sector.Separator = _compositor.CreateSpriteShape(sector.Line);
            sector.Separator.StrokeBrush = _separatorBrush;
            sector.Separator.StrokeThickness = SeparatorWidth;

            ElementCompositionPreview.SetIsTranslationEnabled(sector.Label, true);

            sector.LabelVisual = ElementComposition.GetElementVisual(sector.Label);
            sector.LabelVisual.CenterPoint = new Vector3(24, 10, 0);

            return sector;
        }

        private static Geometry BuildCheck()
        {
            // The three points of the tick, as fractions of the chart box.
            var figure = new PathFigure { StartPoint = new Point(Diameter * .348, Diameter * .538) };
            figure.Segments.Add(new LineSegment { Point = new Point(Diameter * .447, Diameter * .636) });
            figure.Segments.Add(new LineSegment { Point = new Point(Diameter * .678, Diameter * .402) });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return geometry;
        }

        private static Color BlendOver(Color bottom, Color top)
        {
            var a = top.A / 255f;

            return Color.FromArgb(
                bottom.A,
                (byte)(bottom.R * (1 - a) + top.R * a),
                (byte)(bottom.G * (1 - a) + top.G * a),
                (byte)(bottom.B * (1 - a) + top.B * a));
        }

        #endregion

        #region Spinner

        // getSegments has no closed form, so it is sampled. 120 steps over the period is one sample
        // every 45ms of spinner time - the curve moves at most .37 degrees per ms, so a linear
        // segment across one step is within about a degree of the real thing.
        private const int SpinSteps = 120;

        private void StartSpin()
        {
            if (_spinning)
            {
                return;
            }

            _spinning = true;

            if (_spinFrom == null)
            {
                _spinFrom = _compositor.CreateScalarKeyFrameAnimation();
                _spinTo = _compositor.CreateScalarKeyFrameAnimation();

                FillSpin(_spinFrom, _spinTo);

                _spinFrom.Duration = TimeSpan.FromMilliseconds(CircularProgress.Period / CircularProgress.Rate);
                _spinTo.Duration = _spinFrom.Duration;

                _spinFrom.IterationBehavior = AnimationIterationBehavior.Forever;
                _spinTo.IterationBehavior = AnimationIterationBehavior.Forever;
            }

            _window.StartAnimation("From", _spinFrom);
            _window.StartAnimation("To", _spinTo);
        }

        private void StopSpin()
        {
            if (!_spinning)
            {
                return;
            }

            _spinning = false;

            // The spinner's clock freezes the moment loading ends, so the ring unwinds from
            // wherever it had got to rather than from a phase that kept running underneath the
            // transition. Stopping an animation leaves the property at its last value, which is
            // exactly that.
            _window.StopAnimation("From");
            _window.StopAnimation("To");
        }

        private void FillSpin(ScalarKeyFrameAnimation from, ScalarKeyFrameAnimation to)
        {
            for (int s = 0; s <= SpinSteps; s++)
            {
                var progress = (float)s / SpinSteps;

                // The value at the end of the loop is the one just before it restarts, not the one
                // at zero - otherwise the last segment runs the whole seven turns backwards instead
                // of jumping the way a per-frame evaluation does.
                var t = s == SpinSteps
                    ? CircularProgress.Period - 1
                    : progress * CircularProgress.Period;

                CircularProgress.GetSegments(t, out var f, out var v);

                from.InsertKeyFrame(progress, f, _linear);
                to.InsertKeyFrame(progress, v, _linear);
            }
        }

        #endregion

        #region Layout

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = double.IsInfinity(availableSize.Width) ? Diameter : availableSize.Width;
            var size = new Size(width, ControlHeight);

            foreach (var child in Children)
            {
                child.Measure(size);
            }

            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var bounds = new Rect(0, 0, finalSize.Width, finalSize.Height);

            foreach (var child in Children)
            {
                child.Arrange(bounds);
            }

            _center = new Vector2((float)finalSize.Width / 2, (float)finalSize.Height / 2);

            var size = finalSize.ToVector2();

            _root.Size = size;
            _particleRoot.Size = size;

            // Everything a shape visual owns is re-rasterized whenever any of its shapes changes,
            // so the surface is the chart box rather than the control - on a wide control that is
            // most of the pixels gone for nothing.
            var box = new Vector2(Box);
            var origin = new Vector3(_center.X - Box / 2, _center.Y - Box / 2, 0);
            var middle = new Vector2(Box / 2);

            foreach (var visual in new[] { _trackVisual, _shapeVisual, _separatorVisual, _completeVisual })
            {
                visual.Size = box;
                visual.Offset = origin;
            }

            _trackEllipse.Center = middle;
            _completeEllipse.Center = middle;

            // The separators span the loaded ring. They are only ever visible on one, and a static
            // geometry is one less thing for an expression to write every frame.
            // Out to where a selected sector reaches. The overhang is the background colour over the
            // background, so it costs nothing to leave it there on the sectors that have not grown.
            var inner = new Vector2((Diameter - ThicknessLoaded * 2) / 2, 0);
            var outer = new Vector2(Diameter / 2 + SelectGrow, 0);

            foreach (var sector in _sectors)
            {
                sector.Geometry.Center = middle;
                sector.Gradient.EllipseCenter = middle;

                // The shape carries the sector's angle, so it has to turn about the ring's centre.
                sector.Shape.CenterPoint = middle;

                sector.Line.Start = inner;
                sector.Line.End = outer;
                sector.Separator.Offset = middle;
            }

            // The band the particles travel through is the loaded ring: they are held at zero
            // opacity until the chart has settled, and a settled chart is 38px thick.
            _particles.SetRing(
                _center,
                (Diameter - ThicknessLoaded * 2) / 2,
                Diameter / 2,
                GlyphSize);

            ClipParticleField();

            return finalSize;
        }

        #endregion

        #region Glyph

        // The track behind the spinner is the one colour here that is neither the caller's nor fixed,
        // and it only has to read as "slightly lighter than the page".
        //
        // Both ends of the separator's colour move with the theme - the overlay the page passes and
        // the Mica tint under it - and a ThemeResource may hand back the same brush with a new
        // colour rather than a new brush, which would not raise the property changed.
        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateTrack();
            UpdateSeparator();
        }

        // What the window sits on, which is what a separator punched out of the ring can be.
        //
        // Mica's own tints, so that turning the effect off does not change the window's colour, only
        // its depth: MicaController::sc_lightThemeColor and sc_darkThemeColor. High contrast wins
        // over the theme - the system's background colour is the only one that respects the user's
        // scheme.
        //
        // Telegram.Host.WindowBackdrop says the same thing for the window itself, and is the place
        // to check if these ever move. It cannot be shared from here: Host is compiled only by the
        // Win32 flavour, and this control ships in all of them.
        private Color Behind()
        {
            if (new AccessibilitySettings().HighContrast)
            {
                return new UISettings().GetColorValue(UIColorType.Background);
            }

            return ActualTheme == ElementTheme.Dark
                ? Color.FromArgb(0xFF, 0x20, 0x20, 0x20)
                : Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3);
        }

        private void UpdateTrack()
        {
            _trackBrush.Color = ActualTheme == ElementTheme.Dark
                ? Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x14, 0x00, 0x00, 0x00);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // ActualTheme is not settled until the chart is in a tree, and it does not raise a
            // change for the value it had all along.
            UpdateTrack();
            UpdateSeparator();

            if (_loading)
            {
                StartSpin();
            }

            Particles();
        }

        // The spinner is the one thing here that runs without anyone watching, so a chart that is
        // navigated away from mid-count stops costing the compositor anything. It restarts from the
        // top of its loop on the way back, which on a spinner is not something anyone can see.
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _morphTimer.Stop();
            StopSpin();
        }

        // The symbol font is packaged, so the first layout can measure a fallback and only settle
        // once the real one has loaded. Rasterizing a glyph before that bakes the wrong shape into
        // the surface for good, so the brush is thrown away and rebuilt whenever a source resizes.
        private void OnGlyphSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is TextBlock source && _glyphSurfaces.ContainsKey(source.Text))
            {
                _glyphSurfaces[source.Text].Brush?.Dispose();
                _glyphSurfaces.Remove(source.Text);
            }

            Particles();
        }

        // Rasterizes anything the current items ask for that is not cached yet, and hands the field
        // one band per sector. A source that has not been laid out yet is skipped and picked up by
        // its own SizeChanged, which is also what catches the packaged font arriving after the first
        // pass and re-measuring everything.
        private void Particles()
        {
            if (_items == null)
            {
                return;
            }

            // The icons are decoration and nothing else - the ring says everything they say, and
            // upstream they go with the rest of the chat animations. The closest thing here is the
            // transitions flag, which also carries the system's own "play animations in Windows"
            // setting, and a perpetual ornament is exactly what that setting is for.
            //
            // Read where it is read, not subscribed to: the settings that move it are a page away,
            // so the chart will have been rebuilt by the time any of them has.
            if (!PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                _particles.SetBands(null);
                return;
            }

            var count = Math.Min(_items.Count, SectorCount);
            var bands = new List<StorageChartBand>(count);

            for (int i = 0; i < count; i++)
            {
                var glyph = _items[i].Glyph;
                if (glyph == null || _sectors[i].To <= _sectors[i].From)
                {
                    continue;
                }

                if (!_glyphSurfaces.TryGetValue(glyph, out var surface) && !TryRasterize(glyph, out surface))
                {
                    continue;
                }

                bands.Add(new StorageChartBand
                {
                    From = _sectors[i].From,
                    To = _sectors[i].To,
                    Brush = surface.Brush,
                    Size = surface.Size
                });
            }

            _particles.SetBands(bands);
        }

        private bool TryRasterize(string glyph, out GlyphSurface result)
        {
            result = default(GlyphSurface);

            if (!_glyphSources.TryGetValue(glyph, out var source))
            {
                source = new TextBlock
                {
                    FontFamily = BootStrapper.Current.Resources["SymbolThemeFontFamily"] as FontFamily,
                    FontSize = 28,
                    Foreground = new SolidColorBrush(Colors.White),
                    Text = glyph
                };

                source.SizeChanged += OnGlyphSizeChanged;

                _glyphSources[glyph] = source;
                _glyphHost.Children.Add(source);

                // Nothing has measured it yet; SizeChanged will bring us back.
                return false;
            }

            var size = new Vector2((float)source.ActualWidth, (float)source.ActualHeight);
            if (size.X <= 0 || size.Y <= 0)
            {
                return false;
            }

            var visual = _compositor.CreateVisualSurface();
            visual.SourceVisual = ElementComposition.GetElementVisual(source);
            visual.SourceOffset = Vector2.Zero;
            visual.SourceSize = size;

            // A live visual surface tracks its source, so every particle sampling it can cost a
            // realization. Freeze detaches it - at the commit that follows, never at the call - and
            // every particle on that icon then shares one texture. RealizationSize is what keeps
            // that texture from being rasterized at one device pixel per effective pixel and coming
            // out soft.
            if (visual.TryGetPartner(out var partner))
            {
                var scale = XamlRoot == null ? 1 : XamlRoot.RasterizationScale;

                partner.SetStretch(CompositionStretch.Fill);
                partner.SetRealizationSize(size * (float)scale);
                partner.Freeze();
            }

            result = new GlyphSurface
            {
                Brush = _compositor.CreateSurfaceBrush(visual),
                Size = size
            };

            _glyphSurfaces[glyph] = result;
            return true;
        }

        // A real clip, on the pixels. Fading a sprite by how much of it is outside the ring is not
        // the same thing and cannot be: one half over the rim is half transparent everywhere,
        // including the half that is outside, so icons kept spilling a few pixels past the ellipse.
        //
        // It is affordable because it never changes. The particles are only visible on the settled
        // loaded ring, whose two radii are constants, so the annulus is built once per layout rather
        // than per frame - which is what made a geometric clip look out of the question earlier,
        // when the ring's thickness was still being animated underneath it.
        private void ClipParticleField()
        {
            // The annulus depends on nothing but where the ring sits, and arrange runs for reasons
            // that have nothing to do with that.
            if (_clipCenter == _center)
            {
                return;
            }

            _clipCenter = _center;

            // Unassign before disposing: a clip that is still on a visual when it goes is a
            // use-after-free waiting for the next commit.
            var stale = _particleRoot.Clip;

            _particleRoot.Clip = null;
            stale?.Dispose();

            var inner = (Diameter - ThicknessLoaded * 2) / 2;
            var outer = Diameter / 2;

            // Rooted for the duration of the call: the Win2D projection has no KeepAlive, so a
            // geometry only referenced by a local can be collected while the call is still in
            // native code.
            var device = ElementComposition.GetSharedDevice();

            using (var disc = CanvasGeometry.CreateCircle(device, _center, outer))
            using (var hole = CanvasGeometry.CreateCircle(device, _center, inner))
            using (var ring = disc.CombineWith(hole, Matrix3x2.Identity, CanvasGeometryCombine.Exclude))
            {
                _particleRoot.Clip = _compositor.CreateGeometricClip(
                    _compositor.CreatePathGeometry(new CompositionPath(ring)));
            }
        }

        // Each sector's angular range is walked in 7 degree steps, which over the whole ring is a
        // grid of 52. A page can thin it out, but nothing needs to.
        public int ParticleCount
        {
            get => _particles.Count;
            set => _particles.Count = value;
        }

        #endregion

        #region Input

        // A sector grows when it is tapped; on a pointer device the same thing belongs on hover.
        // Hit testing is arithmetic rather than geometry: the ring is an annulus and the sectors are
        // angular ranges of it, both of which the layout already knows.
        public event EventHandler<StorageChartItem> SectorClick;

        public StorageChartItem SelectedItem => _selected < 0 || _items == null || _selected >= _items.Count
            ? null
            : _items[_selected];

        // Subscribed rather than overridden: the OnPointer* virtuals belong to Control, and this is
        // a Panel.
        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            Select(HitTest(e.GetCurrentPoint(this).Position));
        }

        private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        {
            Select(-1);
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            Select(-1);
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var index = HitTest(e.GetCurrentPoint(this).Position);
            if (index >= 0 && _items != null && index < _items.Count)
            {
                SectorClick?.Invoke(this, _items[index]);
            }
        }

        private int HitTest(Point point)
        {
            // Nothing to hit while the ring is a spinner or a tick.
            if (_loading || _complete || _items == null)
            {
                return -1;
            }

            var x = (float)point.X - _center.X;
            var y = (float)point.Y - _center.Y;

            var radius = (float)Math.Sqrt(x * x + y * y);

            // The slop is more than a sector grows, so the pointer does not fall out of one the
            // moment it has grown to meet it.
            if (radius < Diameter / 2 - ThicknessLoaded || radius > Diameter / 2 + HitSlop)
            {
                return -1;
            }

            var angle = (float)(Math.Atan2(y, x) * 180 / Math.PI);
            if (angle < 0)
            {
                angle += 360;
            }

            var count = Math.Min(_items.Count, SectorCount);

            for (int i = 0; i < count; i++)
            {
                if (angle >= _sectors[i].From && angle < _sectors[i].To)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Select(int index)
        {
            if (_selected == index)
            {
                return;
            }

            if (_selected >= 0)
            {
                Animate(_sectors[_selected].Settled, "Selected", 0, SelectDuration, _easeOutQuint, true);
            }

            _selected = index;

            if (_selected >= 0)
            {
                Animate(_sectors[_selected].Settled, "Selected", 1, SelectDuration, _easeOutQuint, true);
            }
        }

        #endregion

        #region State

        // The surface the pages already bound against. A null list is the loading state, which is
        // what both view models hand over until their statistics come back.
        public IList<StorageChartItem> Items
        {
            get => _items;
            set => SetItems(value, true);
        }

        // One entry's visibility changed. The caller has already written IsVisible - the checkbox it
        // came from needs the value too - so this only has to lay the ring out again.
        public void Update(int index, bool visible)
        {
            if (_items == null || index < 0 || index >= _items.Count)
            {
                return;
            }

            _items[index].IsVisible = visible;
            SetItems(_items, true);
        }

        public void SetLoading()
        {
            if (_loading)
            {
                return;
            }

            _loading = true;
            _complete = false;

            Select(-1);
            StartSpin();

            Animate(_state, "Loading", 1, LoadingDuration, _easeOutQuint, true);
            Animate(_state, "Complete", 0, CompleteDuration, _easeOutQuint, true);

            Morph(true);
        }

        public void SetItems(IList<StorageChartItem> items, bool animated)
        {
            if (items == null)
            {
                SetLoading();
                return;
            }

            var wasLoading = _loading;

            _items = items;

            var sum = Sum(items);

            for (int i = 0; i < items.Count && i < SectorCount; i++)
            {
                _sectors[i].Stop0.Color = BlendOver(items[i].Stroke, Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
                _sectors[i].Stop1.Color = BlendOver(items[i].Stroke, Color.FromArgb(0x03, 0x00, 0x00, 0x00));
                _sectors[i].Solid.Color = items[i].Stroke;
            }

            _loading = false;
            _complete = sum <= 0;

            Select(-1);
            StopSpin();

            // Coming out of loading the arrangement has to be in place before the blend starts, or
            // the blend spends its 750ms chasing a target that is itself easing out of twelve
            // o'clock - two eases of different lengths compounding, which is what made the ring
            // look like it was assembling out of pieces. Drawing per frame gets this for free: the
            // animated angles are never touched while loading >= 1, so the first value they ever
            // see is the final one and the animator snaps to it. Once loaded, a new set of numbers
            // does ease, which is the only case this is false.
            var settle = animated && !wasLoading;

            Animate(_state, "Loading", 0, LoadingDuration, _easeOutQuint, animated);
            Animate(_state, "Complete", _complete ? 1 : 0, CompleteDuration, _easeOutQuint, animated);

            if (_complete)
            {
                var readable = FileSizeConverter.Convert(0, true).Split(' ');

                _topText.Text = readable[0];
                _bottomText.Text = readable.Length > 1 ? readable[1] : string.Empty;

                // The angles stay. The ring fades out as a whole, and the labels go with it - their
                // opacity already carries a (1 - S.Complete).
            }
            else
            {
                Layout(items, sum, settle);

                var readable = FileSizeConverter.Convert(sum, true).Split(' ');

                _topText.Text = readable[0];
                _bottomText.Text = readable.Length > 1 ? readable[1] : string.Empty;
            }

            Morph(animated);
        }

        private static long Sum(IList<StorageChartItem> items)
        {
            long sum = 0;

            for (int i = 0; i < items.Count && i < SectorCount; i++)
            {
                if (items[i].IsVisible)
                {
                    sum += items[i].TotalBytes;
                }
            }

            return sum;
        }

        private void Animate(CompositionPropertySet properties, string property, float value,
            double duration, CompositionEasingFunction easing, bool animated)
        {
            if (!animated)
            {
                Set(properties, property, value);
                return;
            }

            var animation = _compositor.CreateScalarKeyFrameAnimation();

            // Starting from wherever the property is means a target that changes mid-flight just
            // redirects the ease instead of restarting it.
            animation.InsertExpressionKeyFrame(0, "this.StartingValue");
            animation.InsertKeyFrame(1, value, easing);
            animation.Duration = TimeSpan.FromMilliseconds(duration);

            properties.StartAnimation(property, animation);
        }

        // Run on every transition, not only the ones that move the ring's thickness.
        private void Morph(bool animated)
        {
            _morphTimer.Stop();

            // The particle field bakes the sector angles into its key frames, so it is stale the
            // moment they start moving. Hide it now rather than leave the last chart's icons drifting
            // over the new one until the rebuild at the end of the transition.
            Set(_state, "Particles", 0);

            var round = _loading || _complete;

            // Plain colour for the duration. A CompositionRadialGradientBrush re-realizes whenever
            // the geometry it paints changes bounds, and eleven of them do that on every frame of
            // any transition - the trims move whether or not the thickness does. This went in once
            // before, made a visible difference, and I took it back out on the strength of a frame
            // counter that was measuring the UI thread in a control that leaves the UI thread idle.
            // It was never evidence of anything.
            Stroke(false);

            if (!animated)
            {
                Caps(round);
                Stroke(Gradient);

                Particles();
                Set(_state, "Particles", 1);

                return;
            }

            // A cap cannot be animated, and the jump it makes when it is swapped is half the stroke
            // width at each end - 19px on a loaded ring, which is most of a small sector. So it is
            // always swapped while the ring is thin: straight away on the way to a thick one, and
            // at the end of the morph on the way back to a thin one.
            _roundPending = round;

            if (!round)
            {
                Caps(false);
            }

            _morphTimer.Interval = TimeSpan.FromMilliseconds(LoadingDuration);
            _morphTimer.Start();
        }

        private void OnMorphTick(object sender, object e)
        {
            _morphTimer.Stop();

            if (_roundPending)
            {
                _roundPending = false;
                Caps(true);
            }

            Stroke(Gradient);

            // Rebuilt here rather than when the numbers change: it keeps a few thousand
            // InsertKeyFrame calls off the frames that are animating, and the angles it bakes in are
            // the ones the sectors have actually landed on.
            Particles();
            Animate(_state, "Particles", 1, TextDuration, _easeOut, true);
        }

        // The gradient is only affordable on a ring that has stopped, and the loading ring never
        // stops - its eleven trims are re-cut on every frame for as long as it is up, and a
        // CompositionRadialGradientBrush re-realizes with them. Leaving it on there was enough to
        // use up the whole frame, which is why a click on a button that does nothing could be seen.
        private bool Gradient => !_loading;

        private void Stroke(bool gradient)
        {
            foreach (var sector in _sectors)
            {
                sector.Shape.StrokeBrush = gradient ? (CompositionBrush)sector.Gradient : sector.Solid;
            }
        }

        // Flat caps are not a nicety: a round cap on a 38px ring adds 19px to each end, which would
        // swallow the 2 degree separator whole. A round one is just as necessary while loading,
        // where it is a 60px corner rounding clamped to half the thickness.
        private void Caps(bool round)
        {
            var cap = round
                ? CompositionStrokeCap.Round
                : CompositionStrokeCap.Flat;

            foreach (var sector in _sectors)
            {
                sector.Shape.StrokeStartCap = cap;
                sector.Shape.StrokeEndCap = cap;
            }
        }

        // `animated` here is the settle flag, not SetItems's: on the way out of loading these land
        // immediately and the blend does the moving.
        private void Layout(IList<StorageChartItem> items, long sum, bool animated)
        {
            var count = Math.Min(items.Count, SectorCount);

            Tile(count);
            var shares = new float[SectorCount];
            var percents = new int[SectorCount];

            var visible = 0;

            for (int i = 0; i < count; i++)
            {
                shares[i] = items[i].IsVisible ? (float)((double)items[i].TotalBytes / sum) : 0;

                if (shares[i] > 0)
                {
                    visible++;
                }
            }

            RoundPercents(shares, percents);

            // Anything under 2% of the ring is pushed up to 2% and everything else is squeezed by
            // what that cost, so the smallest categories stay visible instead of collapsing into
            // the separator.
            var under = 0;
            var minus = 0f;

            for (int i = 0; i < count; i++)
            {
                if (shares[i] > 0 && shares[i] < .02f)
                {
                    under++;
                    minus += shares[i];
                }
            }

            // Sorting the segments by size here, so the ring reads small-to-large, cannot survive
            // being animated. Interpolating a sector's
            // centre and half-width is the same as interpolating its two boundaries, so as long as
            // a sector keeps the same neighbours, the boundary it shares with each of them is the
            // same number on both sides and every intermediate frame is still a partition of the
            // ring. Re-sorting breaks that: a sector lands between a different pair, the shared
            // boundaries no longer agree, and the separators open into gaps for the length of the
            // animation and close again - which is what reads as one long run of ring arriving in
            // pieces. The caller's order is stable, so the ring keeps it.
            var separator = _even ? 0 : WedgeSeparator;
            var span = 360 - separator * (visible < 2 ? 0 : visible);
            var prev = 0f;
            var k = 0;

            for (int i = 0; i < count; i++)
            {
                var sector = _sectors[i];
                var share = shares[i];

                var textAlpha = share > .05f && share < 1 ? 1 : 0;
                var textScale = share < .08f || percents[i] >= 100 ? .85f : 1;

                if (textAlpha > 0)
                {
                    sector.Label.Text = percents[i].ToString() + "%";
                }

                if (share < .02f && share > 0)
                {
                    share = .02f;
                }
                else
                {
                    share *= 1 - (.02f * under - minus);
                }

                var from = prev * span + k * separator;
                var to = from + share * span;

                if (share <= 0)
                {
                    textAlpha = 0;
                }
                else
                {
                    prev += share;
                    k++;
                }

                sector.From = from;
                sector.To = to;

                Animate(sector.Settled, "Center", (from + to) / 2, AngleDuration, _easeOutQuint, animated);
                Animate(sector.Settled, "Size", Math.Abs(to - from) / 2, AngleDuration, _easeOutQuint, animated);
                Animate(sector.Settled, "TextAlpha", textAlpha, TextDuration, _easeOut, animated);
                Animate(sector.Settled, "TextScale", textScale, TextDuration, _easeOut, animated);
            }

            // The pool is eleven whatever the caller brought. A sector with nothing in it collapses
            // at the end of the ring, the way a zero-sized one collapses on its own boundary above -
            // left at its default it would instead sweep back to twelve o'clock during the blend,
            // as a stray arc crossing everything on its way.
            var tail = prev * span + k * separator;

            for (int i = count; i < SectorCount; i++)
            {
                _sectors[i].From = tail;
                _sectors[i].To = tail;

                Animate(_sectors[i].Settled, "Center", tail, AngleDuration, _easeOutQuint, animated);
                Animate(_sectors[i].Settled, "Size", 0, AngleDuration, _easeOutQuint, animated);
                Animate(_sectors[i].Settled, "TextAlpha", 0, TextDuration, _easeOut, animated);
            }
        }

        // Largest remainder, so the labels add up to 100.
        private static void RoundPercents(float[] shares, int[] percents)
        {
            var total = 0;

            for (int i = 0; i < shares.Length; i++)
            {
                percents[i] = (int)(shares[i] * 100);
                total += percents[i];
            }

            while (total < 100)
            {
                var best = -1;
                var bestRemainder = -1f;

                for (int i = 0; i < shares.Length; i++)
                {
                    if (shares[i] <= 0)
                    {
                        continue;
                    }

                    var remainder = shares[i] * 100 - percents[i];
                    if (remainder > bestRemainder)
                    {
                        bestRemainder = remainder;
                        best = i;
                    }
                }

                if (best < 0)
                {
                    break;
                }

                percents[best]++;
                total++;
            }
        }

        #endregion

        #region Geometry

        // Whatever the page layers over the window behind the chart.
        //
        // A gap measured in degrees is a wedge: at 2 degrees it is 1.7px across the ring's inner
        // edge and 3.0px across the outer, and it visibly flares. Given a background the sectors can
        // abut instead and the gaps be drawn over the top as radial lines of a constant pixel width,
        // even the whole way across. Without one there is nothing to draw them in, so the ring falls
        // back to the wedge.
        //
        // It is not the colour that gets drawn, though. The window is Mica and the settings pages
        // put a translucent LayerFillColorDefaultBrush over it, so what is actually behind the ring
        // is the two composited - and drawing the overlay's own colour instead reads as a hard dark
        // line on dark, which is nothing like the surface it is meant to be cutting through. So the
        // brush a page passes is the overlay, and the chart composites it onto what is behind.
        public static readonly DependencyProperty SeparatorBrushProperty =
            DependencyProperty.Register(nameof(SeparatorBrush), typeof(SolidColorBrush), typeof(StorageChart), new PropertyMetadata(null, OnSeparatorBrushChanged));

        public SolidColorBrush SeparatorBrush
        {
            get => (SolidColorBrush)GetValue(SeparatorBrushProperty);
            set => SetValue(SeparatorBrushProperty, value);
        }

        private static void OnSeparatorBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((StorageChart)d).UpdateSeparator();
        }

        private void UpdateSeparator()
        {
            var brush = SeparatorBrush;
            var even = brush != null;

            if (even)
            {
                _separatorBrush.Color = BlendOver(Behind(), brush.Color);
            }

            if (_even == even)
            {
                return;
            }

            _even = even;
            _chart.InsertScalar("Separator", even ? 1 : 0);

            if (_items != null && !_loading)
            {
                Layout(_items, Sum(_items), false);
            }
        }

        private void RestoreThickness()
        {
            _chart.StartAnimation("Thickness", StateExpression(ThicknessExpression));
        }

        private static void Set(CompositionPropertySet properties, string property, float value)
        {
            properties.StopAnimation(property);
            properties.InsertScalar(property, value);
        }

        #endregion
    }

    public partial class StorageChartItem
    {
        public string Name { get; set; }

        public string Glyph { get; set; }

        public long TotalBytes { get; set; }

        public long SentBytes { get; set; }

        public long ReceivedBytes { get; set; }

        public Color Stroke { get; set; }

        public bool IsVisible { get; set; } = true;

        protected List<FileType> _types;
        public IList<FileType> Types => _types;

        public StorageChartItem(StorageStatisticsByFileType statistics)
        {
            _types = new List<FileType>(1)
            {
                statistics.FileType
            };

            TotalBytes = statistics.Size;

            Initialize(statistics.FileType, false);
        }

        public StorageChartItem(NetworkStatisticsEntryFile statistics)
        {
            _types = new List<FileType>(1)
            {
                statistics.FileType
            };

            SentBytes = statistics.SentBytes;
            ReceivedBytes = statistics.ReceivedBytes;
            TotalBytes = statistics.SentBytes + statistics.ReceivedBytes;

            Initialize(statistics.FileType, true);
        }

        public StorageChartItem(FileType fileType)
        {
            _types = new List<FileType>(1)
            {
                fileType
            };

            SentBytes = 0;
            ReceivedBytes = 0;
            TotalBytes = 0;

            Initialize(fileType, true);
        }

        private void Initialize(FileType fileType, bool network)
        {
            switch (fileType)
            {
                case FileTypePhoto:
                    Name = Strings.LocalPhotoCache;
                    Glyph = Icons.ImageFilled;
                    Stroke = Color.FromArgb(0xFF, 0x32, 0x7F, 0xE5);
                    break;
                case FileTypeVideo:
                    Name = Strings.LocalVideoCache;
                    Glyph = Icons.VideoFilled;
                    Stroke = Color.FromArgb(0xFF, 0xDE, 0xBA, 0x08);
                    break;
                case FileTypeDocument:
                    Name = Strings.LocalDocumentCache;
                    Glyph = Icons.DocumentFilled;
                    Stroke = Color.FromArgb(0xFF, 0x61, 0xC7, 0x52);
                    break;
                case FileTypeAudio:
                    Name = Strings.LocalMusicCache;
                    Glyph = Icons.PlayCircleFilled;
                    Stroke = Color.FromArgb(0xFF, 0x7F, 0x79, 0xF3);
                    break;
                case FileTypeVideoNote:
                case FileTypeVoiceNote:
                    Name = Strings.LocalAudioCache;
                    Glyph = Icons.MicOnFilled;
                    Stroke = Color.FromArgb(0xFF, 0xE0, 0x53, 0x56);
                    break;
                case FileTypeSticker:
                    Name = Strings.AccDescrStickers;
                    Glyph = Icons.StickerFilled;
                    Stroke = Color.FromArgb(0xFF, 0x8F, 0xCF, 0x39);
                    break;
                case FileTypePhotoStory:
                case FileTypeVideoStory:
                    Name = Strings.LocalStoriesCache;
                    Glyph = Icons.Stories; // Not the right icon but currently not used
                    Stroke = Color.FromArgb(0xFF, 0x7F, 0x79, 0xF3);
                    break;
                default:
                    Name = network ? Strings.MessagesOverview : Strings.LocalCache;
                    Glyph = Icons.ChatEmptyFilled;
                    Stroke = Color.FromArgb(0xFF, 0x58, 0xA8, 0xED);
                    break;
            }
        }

        public StorageChartItem Add(StorageStatisticsByFileType statistics)
        {
            _types.Add(statistics.FileType);
            TotalBytes += statistics.Size;
            return this;
        }

        public StorageChartItem Add(NetworkStatisticsEntryFile statistics)
        {
            _types.Add(statistics.FileType);
            SentBytes += statistics.SentBytes;
            ReceivedBytes += statistics.ReceivedBytes;
            TotalBytes += statistics.SentBytes + statistics.ReceivedBytes;
            return this;
        }
    }
}
