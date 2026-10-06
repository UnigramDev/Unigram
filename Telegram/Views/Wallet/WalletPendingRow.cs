//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Wallet
{
    /// <summary>
    /// How a history row looks while its transfer is under way: raised out of the list, the
    /// amount drawn large and centred, a band of light running round its edge and a clock on the
    /// avatar - and how it sinks back once the transfer has settled, either way.
    /// </summary>
    /// <remarks>
    /// After the Android prototype the wallet is being matched to. Everything runs on the
    /// compositor off one property set - <c>Lift</c>, <c>Press</c> and <c>Arrived</c> - so a row
    /// that is pending for half a minute costs nothing on the UI thread while it waits.
    ///
    /// One per cell, made the first time the cell shows a pending row, so the rows that never do
    /// pay a field read.
    /// </remarks>
    public sealed class WalletPendingRow
    {
        // The prototype's. Raised rows stand this much larger, and the amount shrinks by the
        // other once settled, so while pending it is drawn that much larger than at rest.
        private const float LiftScale = 1.035f;
        private const float AmountScale = 1 / 0.7f;

        // How far above the row's middle the raised amount sits, set by eye.
        private const float AmountRaise = 2;

        private static readonly TimeSpan SettleDuration = WalletTransferFlight.Slow(TimeSpan.FromMilliseconds(260));
        private static readonly TimeSpan ShimmerDuration = WalletTransferFlight.Slow(TimeSpan.FromMilliseconds(1600));

        // How long a raised row stays up before it may settle, counted from the stone landing. The
        // prototype fakes a send of two to four seconds; a real one refused at once needs a floor
        // of its own, or the row is gone before it was seen.
        private static readonly TimeSpan MinimumRaised = WalletTransferFlight.Slow(TimeSpan.FromSeconds(1));

        private static readonly TimeSpan MinuteTurn = WalletTransferFlight.Slow(TimeSpan.FromSeconds(1));
        private static readonly TimeSpan HourTurn = WalletTransferFlight.Slow(TimeSpan.FromSeconds(6));

        // The prototype's springs: Spring.StiffnessMediumLow, the press's damping ratio and the
        // amount's. The press goes down for PressDown before it is let go.
        private const float MediumLow = 400;
        private const float PressDamping = 0.3f;
        private const float ArriveDamping = 0.5f;

        // The prototype's own. They read as a shake only while the list's add transition was still
        // moving the row under the stone, which is why the list has none.
        private static readonly TimeSpan PressDown = TimeSpan.FromMilliseconds(90);
        private const float PressStiffness = MediumLow;

        // The prototype's TgButton, and the cyan its light band peaks at.
        private static readonly Color Blue = Color.FromArgb(0xFF, 0x22, 0x9A, 0xF0);
        private static readonly Color Cyan = Color.FromArgb(0xFF, 0x8F, 0xE6, 0xFF);

        private readonly WalletTransactionCell _cell;

        private SelectorItem _container;
        private Compositor _compositor;
        private CompositionPropertySet _props;

        private ShapeVisual _shimmer;
        private ShapeVisual _clock;

        // The list key of the row being shown: the pending row's id, which the transaction that
        // replaces it is keyed by too.
        private string _key;

        // The settle is asked for when the transfer ends, and only starts once the stone has
        // landed and the row has been raised for MinimumRaised.
        private bool _awaitingStone;
        private bool _impactScheduled;
        private bool _settleRequested;
        private bool _settling;
        private DateTime _raisedAt;

        private CompositionScopedBatch _settleBatch;
        private DispatcherTimer _settleTimer;

        // The send screen's stone, sitting on the glyph since it landed.
        private WalletTransferFlight _stone;

        private WalletPendingRow(WalletTransactionCell cell)
        {
            _cell = cell;
        }

        /// <summary>
        /// Called for every transaction row bound, and with a null transaction when one is
        /// recycled.
        /// </summary>
        /// <param name="key">The row's key in the list - the pending row's id, which the
        /// transaction that replaces it shares.</param>
        /// <param name="incoming">Whether the send screen's stone is on its way to this row.</param>
        public static void Update(SelectorItem container, WalletTransactionCell cell, TonWalletTransaction transaction, string key, bool incoming, IWalletService wallet)
        {
            var row = cell.PendingRow;

            if (transaction == null)
            {
                row?.Reset();
                return;
            }

            var final = transaction.State is not TonWalletTransactionStatePending;
            var raised = row?._props != null && row._key == key;

            // Taken whenever it is there, so a row scrolled back to shortly after does not settle a
            // second time once this one has finished.
            var settled = final && wallet.TryTakeSettled(transaction.Id);

            if (!final || incoming || raised || settled)
            {
                // From the raised pose whether or not this container was the one showing it: the
                // list may have rebound the row to another, and the pose is the same everywhere.
                row ??= cell.PendingRow = new WalletPendingRow(cell);

                if (row.Lift(container, key) && final && !incoming)
                {
                    // Only now raised, for a transfer already over: nothing was watching it wait.
                    row._raisedAt = DateTime.MinValue;
                }

                if (incoming)
                {
                    row.AwaitStone();
                }

                if (final)
                {
                    row.Settle();
                }
            }
            else
            {
                row?.Reset();
            }
        }

        /// <summary>
        /// The flight went elsewhere, or out: the amount comes back on its own.
        /// </summary>
        public void ForgetStone()
        {
            if (_props == null || !_awaitingStone)
            {
                return;
            }

            _awaitingStone = false;
            _raisedAt = DateTime.UtcNow;

            // An impact scheduled for a stone that went elsewhere must not still land here.
            if (_impactScheduled)
            {
                _impactScheduled = false;
                _props.StopAnimation("Press");
                _props.InsertScalar("Press", 0);
            }

            Arrive(TimeSpan.Zero);

            if (_settleRequested)
            {
                StartSettle();
            }
        }

        // What the held stone's squash and the row's press are both read from.
        public CompositionPropertySet Properties => _props;

        /// <summary>
        /// The stone will reach the glyph after <paramref name="delay"/>: the press and the amount
        /// popping in start then, on the compositor's clock.
        /// </summary>
        /// <remarks>
        /// Scheduled up front rather than started when the flight reports landing, which arrives on
        /// the UI thread a frame or more after the stone has stopped - and an impact that follows
        /// the stone reads as the row settling afterwards rather than being pushed down by it.
        /// </remarks>
        public void ScheduleImpact(TimeSpan delay)
        {
            if (_props == null)
            {
                return;
            }

            _impactScheduled = true;
            Impact(delay);
        }

        /// <param name="delay">When the stone arrives, which is when the row starts to give.</param>
        private void Impact(TimeSpan delay)
        {
            Arrive(delay);

            // Down on a short ease, then back on the prototype's spring(0.3, MediumLow), which
            // overshoots into a stretch and wobbles out over most of a second.
            var down = (float)PressDown.TotalSeconds;
            var back = SpringSettleTime(PressDamping, PressStiffness);
            var total = down + back;

            var press = _compositor.CreateScalarKeyFrameAnimation();
            press.InsertKeyFrame(down / total, 1, Standard());
            InsertSpring(press, 1, 0, PressDamping, PressStiffness, down, total);
            press.Duration = WalletTransferFlight.Slow(TimeSpan.FromSeconds(total));
            press.DelayTime = delay;

            _props.StartAnimation("Press", press);
        }

        /// <summary>
        /// The stone has landed on the glyph: the row is pressed down and springs back, the amount
        /// pops in, and the stone stays over the glyph until the transfer is over.
        /// </summary>
        /// <returns>Whether the row holds the stone, which it does unless it is no longer raised.</returns>
        public bool Land(WalletTransferFlight stone)
        {
            if (_props == null)
            {
                return false;
            }

            _stone = stone;
            _cell.HideGlyph(true);

            _awaitingStone = false;
            _raisedAt = DateTime.UtcNow;

            // Already under way if the flight scheduled it, which it does unless the row changed
            // hands in the air.
            if (_impactScheduled)
            {
                _impactScheduled = false;
            }
            else
            {
                Impact(TimeSpan.Zero);
            }

            if (_settleRequested)
            {
                StartSettle();
            }

            return true;
        }

        // The amount waits for the stone, and pops in as it lands.
        private void AwaitStone()
        {
            if (_awaitingStone)
            {
                return;
            }

            _awaitingStone = true;
            _props.InsertScalar("Arrived", 0);
        }

        // The prototype's spring(0.5, MediumLow), from wherever it is to one.
        private void Arrive(TimeSpan delay)
        {
            var time = SpringSettleTime(ArriveDamping, MediumLow);

            var arrived = _compositor.CreateScalarKeyFrameAnimation();
            InsertSpring(arrived, 0, 1, ArriveDamping, MediumLow, 0, time);
            arrived.Duration = WalletTransferFlight.Slow(TimeSpan.FromSeconds(time));
            arrived.DelayTime = delay;

            _props.StartAnimation("Arrived", arrived);
        }

        /// <summary>
        /// A spring from rest at <paramref name="from"/> to <paramref name="to"/>, sampled into
        /// keyframes starting at <paramref name="start"/> seconds of an animation
        /// <paramref name="total"/> seconds long.
        /// </summary>
        /// <remarks>
        /// The prototype's springs are Compose's, which have an exact answer: unit mass, the
        /// stiffness the square of the natural frequency, and done once within a hundredth of the
        /// target. So they are reproduced rather than approximated - a Composition spring is set
        /// by a Period that no stiffness converts to, and the one picked by eye buzzed where the
        /// prototype wobbles.
        /// </remarks>
        private void InsertSpring(ScalarKeyFrameAnimation animation, float from, float to, float damping, float stiffness, float start, float total)
        {
            const int Samples = 40;

            var natural = MathF.Sqrt(stiffness);
            var decay = damping * natural;
            var damped = natural * MathF.Sqrt(1 - damping * damping);
            var time = SpringSettleTime(damping, stiffness);

            // Linear between samples: a dozen of them to each oscillation is smooth at any speed
            // this runs at.
            var linear = _compositor.CreateLinearEasingFunction();

            for (int i = 1; i <= Samples; i++)
            {
                var t = time * i / Samples;

                // Underdamped, released from rest: the displacement left, as a fraction of the
                // distance to go. Exactly the target at the end, where Compose snaps to it.
                var left = i == Samples ? 0 : MathF.Exp(-decay * t) * (MathF.Cos(damped * t) + decay / damped * MathF.Sin(damped * t));

                animation.InsertKeyFrame((start + t) / total, to + (from - to) * left, linear);
            }
        }

        // When the envelope of an underdamped spring released from rest drops below a hundredth.
        private static float SpringSettleTime(float damping, float stiffness)
        {
            return MathF.Log(100 / MathF.Sqrt(1 - damping * damping)) / (damping * MathF.Sqrt(stiffness));
        }

        /// <summary>
        /// Where the glyph is drawn once the row has been raised, given where XAML puts it.
        /// </summary>
        /// <remarks>
        /// XAML already knows both scales - they are RenderTransforms - and at Lift and Arrived of
        /// one the compositor's own are one, so what it cannot know is only the translation that
        /// takes the amount to the middle of the row. That is in the row's space, which the row's
        /// scale enlarges.
        /// </remarks>
        public Rect Project(Rect glyph, ref double fontSize)
        {
            if (_props == null || _settling)
            {
                return glyph;
            }

            var amount = _cell.AmountText;

            // Layout's, like this.Target.Offset in the expression - not the transformed position.
            var shift = (_cell.ActualHeight / 2 - amount.ActualOffset.Y - amount.ActualHeight / 2 - AmountRaise) * LiftScale;

            fontSize *= AmountScale * LiftScale;
            return new Rect(glyph.X, glyph.Y + shift, glyph.Width, glyph.Height);
        }

        // Whether this raised the row, rather than found it raised already.
        private bool Lift(SelectorItem container, string key)
        {
            if (_key == key && _container == container)
            {
                return false;
            }

            Reset();

            _key = key;
            _container = container;
            _raisedAt = DateTime.UtcNow;

            var visual = ElementCompositionPreview.GetElementVisual(container);
            _compositor = visual.Compositor;

            _props = _compositor.CreatePropertySet();
            _props.InsertScalar("Lift", 1);
            _props.InsertScalar("Press", 0);
            _props.InsertScalar("Arrived", 1);
            _props.InsertScalar("Phase", 0);

            // Above its neighbours while raised, or their backgrounds paint over its edge.
            Canvas.SetZIndex(container, 1);

            LiftContainer(visual);
            LiftAmount();

            _shimmer = CreateShimmer(visual);
            ElementCompositionPreview.SetElementChildVisual(container, _shimmer);

            _clock = CreateClock();
            ElementCompositionPreview.SetElementChildVisual(_cell, _clock);

            return true;
        }

        private void LiftContainer(Visual visual)
        {
            ElementCompositionPreview.SetIsTranslationEnabled(_container, true);

            // The raised scale is a RenderTransform and the compositor only scales down from it:
            // XAML rasterizes text for the transforms it knows of, and a composition scale is not
            // one, so the text would otherwise be a raised bitmap of the row at rest. The two
            // share a centre, so they compose as one scale.
            _container.RenderTransformOrigin = new Point(0.5, 0.5);
            _container.RenderTransform = new ScaleTransform { ScaleX = LiftScale, ScaleY = LiftScale };

            // Grows evenly from its middle, and the press squashes it and pushes it down a little.
            var centre = _compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)");

            var scale = _compositor.CreateExpressionAnimation("Vector3((1 + (s - 1) * p.Lift) / s * (1 + 0.012 * p.Press), (1 + (s - 1) * p.Lift) / s * (1 - 0.04 * p.Press), 1)");
            scale.SetReferenceParameter("p", _props);
            scale.SetScalarParameter("s", LiftScale);

            var translation = _compositor.CreateExpressionAnimation("Vector3(0, 4 * p.Press, 0)");
            translation.SetReferenceParameter("p", _props);

            visual.StartAnimation("CenterPoint", centre);
            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Translation", translation);
        }

        private void LiftAmount()
        {
            var amount = _cell.AmountText;
            var visual = ElementCompositionPreview.GetElementVisual(amount);
            var row = ElementCompositionPreview.GetElementVisual(_cell);

            ElementCompositionPreview.SetIsTranslationEnabled(amount, true);

            // Large as a RenderTransform, for the same reason as the row: the compositor only
            // scales down from it, so the raised amount is rasterized at the size it is shown.
            amount.RenderTransformOrigin = new Point(1, 0.5);
            amount.RenderTransform = new ScaleTransform { ScaleX = AmountScale, ScaleY = AmountScale };

            // Read off the visuals rather than measured here: the row is bound before it is laid
            // out, so the sizes are only right by the time the compositor reads them.
            var centre = _compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X, this.Target.Size.Y * 0.5, 0)");

            var scale = _compositor.CreateExpressionAnimation("Vector3((1 + (s - 1) * p.Lift) / s * (0.5 + 0.5 * p.Arrived), (1 + (s - 1) * p.Lift) / s * (0.5 + 0.5 * p.Arrived), 1)");
            scale.SetReferenceParameter("p", _props);
            scale.SetScalarParameter("s", AmountScale);

            var translation = _compositor.CreateExpressionAnimation("Vector3(0, p.Lift * (row.Size.Y * 0.5 - this.Target.Offset.Y - this.Target.Size.Y * 0.5 - k), 0)");
            translation.SetScalarParameter("k", AmountRaise);
            translation.SetReferenceParameter("p", _props);
            translation.SetReferenceParameter("row", row);

            var opacity = _compositor.CreateExpressionAnimation("Clamp(p.Arrived, 0, 1)");
            opacity.SetReferenceParameter("p", _props);

            visual.StartAnimation("CenterPoint", centre);
            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Translation", translation);
            visual.StartAnimation("Opacity", opacity);
        }

        /// <summary>
        /// A band of blue light running round the row's rounded edge from left to right, fading in
        /// and out as it crosses.
        /// </summary>
        private ShapeVisual CreateShimmer(Visual host)
        {
            const float stroke = 1.5f;

            var radius = (float)_container.CornerRadius.TopLeft;

            var geometry = _compositor.CreateRoundedRectangleGeometry();
            geometry.Offset = new Vector2(stroke / 2);
            geometry.CornerRadius = new Vector2(Math.Max(0, radius - stroke / 2));

            var geometrySize = _compositor.CreateExpressionAnimation("Vector2(host.Size.X - k, host.Size.Y - k)");
            geometrySize.SetReferenceParameter("host", host);
            geometrySize.SetScalarParameter("k", stroke);
            geometry.StartAnimation("Size", geometrySize);

            // Absolute, so the band is half the row wide whatever the row's width, and travels
            // from fully off its left to fully off its right.
            var brush = _compositor.CreateLinearGradientBrush();
            brush.MappingMode = CompositionMappingMode.Absolute;
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(0, Color.FromArgb(0, Blue.R, Blue.G, Blue.B)));
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(0.45f, Color.FromArgb(0x99, Blue.R, Blue.G, Blue.B)));
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(0.6f, Color.FromArgb(0x99, Cyan.R, Cyan.G, Cyan.B)));
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(1, Color.FromArgb(0, Blue.R, Blue.G, Blue.B)));

            var start = _compositor.CreateExpressionAnimation("Vector2(host.Size.X * (2 * p.Phase - 0.75), 0)");
            start.SetReferenceParameter("host", host);
            start.SetReferenceParameter("p", _props);

            var end = _compositor.CreateExpressionAnimation("Vector2(host.Size.X * (2 * p.Phase - 0.25), 0)");
            end.SetReferenceParameter("host", host);
            end.SetReferenceParameter("p", _props);

            brush.StartAnimation("StartPoint", start);
            brush.StartAnimation("EndPoint", end);

            var shape = _compositor.CreateSpriteShape(geometry);
            shape.StrokeBrush = brush;
            shape.StrokeThickness = stroke;

            var visual = _compositor.CreateShapeVisual();
            visual.Shapes.Add(shape);

            var size = _compositor.CreateExpressionAnimation("host.Size");
            size.SetReferenceParameter("host", host);
            visual.StartAnimation("Size", size);

            var opacity = _compositor.CreateExpressionAnimation("p.Lift * Max(Sin(p.Phase * pi), 0)");
            opacity.SetReferenceParameter("p", _props);
            opacity.SetScalarParameter("pi", MathF.PI);
            visual.StartAnimation("Opacity", opacity);

            var phase = _compositor.CreateScalarKeyFrameAnimation();
            phase.InsertKeyFrame(0, 0);
            phase.InsertKeyFrame(1, 1, _compositor.CreateLinearEasingFunction());
            phase.Duration = ShimmerDuration;
            phase.IterationBehavior = AnimationIterationBehavior.Forever;

            _props.StartAnimation("Phase", phase);

            return visual;
        }

        /// <summary>
        /// A clock in a little circle cut out of the avatar's lower right: a rim and two hands
        /// going round.
        /// </summary>
        /// <remarks>
        /// The prototype's, scaled from its 46 pixel avatar to this one.
        /// </remarks>
        private ShapeVisual CreateClock()
        {
            // Size rather than ActualWidth: the row is bound before it is laid out.
            var k = _cell.PhotoElement.Size / 46f;
            var line = 1.6f * k;

            // A shape visual clips to its size and a stroke is centred on its outline, so the
            // visual is sized for the badge's whole extent - the cut, or the ring and half its
            // stroke - rather than for the avatar, which the badge hangs over the corner of.
            var extent = 10 * k + line;
            var centre = new Vector2(extent);

            var visual = _compositor.CreateShapeVisual();
            visual.Size = new Vector2(extent * 2);
            visual.CenterPoint = new Vector3(centre, 0);

            // Hosted by the cell rather than the avatar, which may clip what it hosts, and kept on
            // the avatar's corner wherever layout puts it.
            var offset = _compositor.CreateExpressionAnimation("Vector3(photo.Offset.X + d, photo.Offset.Y + d, 0)");
            offset.SetReferenceParameter("photo", ElementCompositionPreview.GetElementVisual(_cell.PhotoElement));
            offset.SetScalarParameter("d", (46 - 7) * k - extent);
            visual.StartAnimation("Offset", offset);

            // The cut is the row's own background, which is what it is cut out of. Read off the
            // container rather than looked up, and left out where it is not a plain colour.
            if (_container.Background is SolidColorBrush background && background.Color.A == 0xFF)
            {
                visual.Shapes.Add(Disc(centre, 10 * k, background.Color));
            }

            visual.Shapes.Add(Disc(centre, 8 * k, Colors.White));

            var rim = _compositor.CreateEllipseGeometry();
            rim.Center = centre;
            rim.Radius = new Vector2(7 * k);

            var ring = _compositor.CreateSpriteShape(rim);
            ring.StrokeBrush = _compositor.CreateColorBrush(Blue);
            ring.StrokeThickness = line;
            visual.Shapes.Add(ring);

            visual.Shapes.Add(Hand(centre, 4.6f * k, line, 0, MinuteTurn));
            visual.Shapes.Add(Hand(centre, 3.2f * k, line, 120, HourTurn));

            var scale = _compositor.CreateExpressionAnimation("Vector3(p.Lift, p.Lift, 1)");
            scale.SetReferenceParameter("p", _props);
            visual.StartAnimation("Scale", scale);

            return visual;
        }

        private CompositionSpriteShape Disc(Vector2 centre, float radius, Color color)
        {
            var geometry = _compositor.CreateEllipseGeometry();
            geometry.Center = centre;
            geometry.Radius = new Vector2(radius);

            var shape = _compositor.CreateSpriteShape(geometry);
            shape.FillBrush = _compositor.CreateColorBrush(color);

            return shape;
        }

        private CompositionSpriteShape Hand(Vector2 centre, float length, float thickness, float from, TimeSpan turn)
        {
            var geometry = _compositor.CreateLineGeometry();
            geometry.Start = centre;
            geometry.End = centre - new Vector2(0, length);

            var shape = _compositor.CreateSpriteShape(geometry);
            shape.StrokeBrush = _compositor.CreateColorBrush(Blue);
            shape.StrokeThickness = thickness;
            shape.StrokeStartCap = CompositionStrokeCap.Round;
            shape.StrokeEndCap = CompositionStrokeCap.Round;
            shape.CenterPoint = centre;

            var rotation = _compositor.CreateScalarKeyFrameAnimation();
            rotation.InsertKeyFrame(0, from);
            rotation.InsertKeyFrame(1, from + 360, _compositor.CreateLinearEasingFunction());
            rotation.Duration = turn;
            rotation.IterationBehavior = AnimationIterationBehavior.Forever;

            shape.StartAnimation("RotationAngleInDegrees", rotation);
            return shape;
        }

        /// <summary>
        /// The transfer is over, landed or failed: the row sinks back into the list, the clock and
        /// the light go, and the amount settles into its corner - all on one short ease, so they
        /// read as one movement.
        /// </summary>
        /// <remarks>
        /// Not before the stone has landed, and not before the row has been seen raised for a
        /// moment: a transfer refused at once is over while the stone is still in the air, and
        /// sinking then would leave the stone to land on a row that has already finished.
        /// </remarks>
        private void Settle()
        {
            if (_props == null || _settleRequested)
            {
                return;
            }

            _settleRequested = true;

            if (!_awaitingStone)
            {
                StartSettle();
            }
        }

        private void StartSettle()
        {
            if (_settling || _settleTimer != null)
            {
                return;
            }

            // A timer rather than a delay on the compositor: the stone and the glyph change hands
            // as the row starts to sink, and the glyph is XAML.
            var delay = _raisedAt + MinimumRaised - DateTime.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                _settleTimer = new DispatcherTimer { Interval = delay };
                _settleTimer.Tick += SettleTimer_Tick;
                _settleTimer.Start();
                return;
            }

            Sink();
        }

        private void SettleTimer_Tick(object sender, object e)
        {
            StopSettleTimer();
            Sink();
        }

        private void StopSettleTimer()
        {
            if (_settleTimer != null)
            {
                _settleTimer.Tick -= SettleTimer_Tick;
                _settleTimer.Stop();
                _settleTimer = null;
            }
        }

        private void Sink()
        {
            if (_props == null || _settling)
            {
                return;
            }

            _settling = true;

            // The stone goes on the same ease as everything else, and the glyph it was covering
            // comes back underneath it.
            ReleaseStone();

            var lift = _compositor.CreateScalarKeyFrameAnimation();
            lift.InsertKeyFrame(1, 0, Standard());
            lift.Duration = SettleDuration;

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _props.StartAnimation("Lift", lift);
            batch.End();

            _settleBatch = batch;
            batch.Completed += Settle_Completed;
        }

        private void Settle_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= Settle_Completed;
            }

            // Only if nothing else has taken the cell over since: a batch is told apart from a
            // later one by identity, the flags having been reused by then.
            if (_settling && sender == _settleBatch)
            {
                Reset();
            }
        }

        private CubicBezierEasingFunction Standard()
        {
            // FastOutSlowIn.
            return _compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(0.2f, 1));
        }

        private void ReleaseStone()
        {
            _cell.HideGlyph(false);

            var stone = _stone;
            _stone = null;
            stone?.Release();
        }

        /// <summary>
        /// Back to an ordinary row, at once.
        /// </summary>
        private void Reset()
        {
            if (_props == null)
            {
                return;
            }

            // The stone was this row's to keep, and nothing else will let it go.
            StopSettleTimer();
            ReleaseStone();

            var container = ElementCompositionPreview.GetElementVisual(_container);
            container.StopAnimation("CenterPoint");
            container.StopAnimation("Scale");
            container.StopAnimation("Translation");
            container.Scale = Vector3.One;
            container.Properties.InsertVector3("Translation", Vector3.Zero);

            // Together with the composition scales, which leaves the row looking the same: settled,
            // the two had cancelled out.
            _container.ClearValue(UIElement.RenderTransformProperty);
            _container.ClearValue(UIElement.RenderTransformOriginProperty);

            var amount = ElementCompositionPreview.GetElementVisual(_cell.AmountText);
            amount.StopAnimation("CenterPoint");
            amount.StopAnimation("Scale");
            amount.StopAnimation("Translation");
            amount.StopAnimation("Opacity");
            amount.Scale = Vector3.One;
            amount.Opacity = 1;
            amount.Properties.InsertVector3("Translation", Vector3.Zero);

            _cell.AmountText.ClearValue(UIElement.RenderTransformProperty);
            _cell.AmountText.ClearValue(UIElement.RenderTransformOriginProperty);

            Canvas.SetZIndex(_container, 0);

            ElementCompositionPreview.SetElementChildVisual(_container, null);
            ElementCompositionPreview.SetElementChildVisual(_cell, null);

            _props.StopAnimation("Phase");
            _props.StopAnimation("Lift");
            _props.StopAnimation("Press");
            _props.StopAnimation("Arrived");

            _shimmer?.Dispose();
            _clock?.Dispose();

            _shimmer = null;
            _clock = null;
            _props = null;
            _container = null;
            _key = null;
            _settling = false;
            _settleRequested = false;
            _awaitingStone = false;
            _impactScheduled = false;
            _settleBatch = null;
        }
    }
}
