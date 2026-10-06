//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;

namespace Telegram.Views.Wallet
{
    /// <summary>
    /// What the send transition's landing sites share: the press the stone gives them, the amount
    /// popping in, and the clock they show while the transfer is under way.
    /// </summary>
    internal static class WalletTransferVisuals
    {
        // The prototype's springs: Spring.StiffnessMediumLow, the press's damping ratio and the
        // amount's. The press goes down for PressDown before it is let go. They read as a shake
        // only while the wallet list's add transition was still moving the row under the stone,
        // which is why the list has none.
        private const float MediumLow = 400;
        private const float PressDamping = 0.3f;
        private const float ArriveDamping = 0.5f;
        private static readonly TimeSpan PressDown = TimeSpan.FromMilliseconds(90);

        private static readonly TimeSpan MinuteTurn = WalletTransferFlight.Slow(TimeSpan.FromSeconds(1));
        private static readonly TimeSpan HourTurn = WalletTransferFlight.Slow(TimeSpan.FromSeconds(6));

        // The prototype's TgButton.
        public static readonly Color Blue = Color.FromArgb(0xFF, 0x22, 0x9A, 0xF0);

        // FastOutSlowIn.
        public static CubicBezierEasingFunction Standard(Compositor compositor)
        {
            return compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(0.2f, 1));
        }

        /// <summary>
        /// The press, for a property going from 0 to 1 and back: down on a short ease from
        /// <paramref name="delay"/>, then back on the prototype's spring(0.3, MediumLow), which
        /// overshoots into a stretch and wobbles out over most of a second.
        /// </summary>
        public static ScalarKeyFrameAnimation CreatePress(Compositor compositor, TimeSpan delay)
        {
            var down = (float)PressDown.TotalSeconds;
            var back = SpringSettleTime(PressDamping, MediumLow);
            var total = down + back;

            var press = compositor.CreateScalarKeyFrameAnimation();
            press.InsertKeyFrame(down / total, 1, Standard(compositor));
            InsertSpring(compositor, press, 1, 0, PressDamping, MediumLow, down, total);
            press.Duration = WalletTransferFlight.Slow(TimeSpan.FromSeconds(total));
            press.DelayTime = delay;

            return press;
        }

        /// <summary>
        /// The prototype's spring(0.5, MediumLow), from 0 to 1, starting after <paramref name="delay"/>.
        /// </summary>
        public static ScalarKeyFrameAnimation CreateArrive(Compositor compositor, TimeSpan delay)
        {
            var time = SpringSettleTime(ArriveDamping, MediumLow);

            var arrived = compositor.CreateScalarKeyFrameAnimation();
            InsertSpring(compositor, arrived, 0, 1, ArriveDamping, MediumLow, 0, time);
            arrived.Duration = WalletTransferFlight.Slow(TimeSpan.FromSeconds(time));
            arrived.DelayTime = delay;

            return arrived;
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
        private static void InsertSpring(Compositor compositor, ScalarKeyFrameAnimation animation, float from, float to, float damping, float stiffness, float start, float total)
        {
            const int Samples = 40;

            var natural = MathF.Sqrt(stiffness);
            var decay = damping * natural;
            var damped = natural * MathF.Sqrt(1 - damping * damping);
            var time = SpringSettleTime(damping, stiffness);

            // Linear between samples: a dozen of them to each oscillation is smooth at any speed
            // this runs at.
            var linear = compositor.CreateLinearEasingFunction();

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
        /// A clock in a little circle: a rim and two hands going round. The prototype's, drawn for
        /// its 46 pixel avatar and scaled by <paramref name="k"/>.
        /// </summary>
        /// <param name="ink">The rim and the hands.</param>
        /// <param name="face">The disc behind them; none if null.</param>
        /// <param name="cut">The circle cut out around it, in what it is cut out of; none if null.</param>
        /// <param name="centre">Where the clock's middle is in the returned visual, which is also
        /// its centre point.</param>
        /// <remarks>
        /// A shape visual clips to its size and a stroke is centred on its outline, so the visual
        /// is sized for the badge's whole extent - the cut, or the ring and half its stroke.
        /// </remarks>
        public static ShapeVisual CreateClock(Compositor compositor, float k, Color ink, Color? face, Color? cut, out Vector2 centre)
        {
            var line = 1.6f * k;

            var extent = 10 * k + line;
            centre = new Vector2(extent);

            var visual = compositor.CreateShapeVisual();
            visual.Size = new Vector2(extent * 2);
            visual.CenterPoint = new Vector3(centre, 0);

            if (cut is Color color)
            {
                visual.Shapes.Add(Disc(compositor, centre, 10 * k, color));
            }

            if (face is Color background)
            {
                visual.Shapes.Add(Disc(compositor, centre, 8 * k, background));
            }

            var rim = compositor.CreateEllipseGeometry();
            rim.Center = centre;
            rim.Radius = new Vector2(7 * k);

            var ring = compositor.CreateSpriteShape(rim);
            ring.StrokeBrush = compositor.CreateColorBrush(ink);
            ring.StrokeThickness = line;
            visual.Shapes.Add(ring);

            visual.Shapes.Add(Hand(compositor, centre, 4.6f * k, line, 0, MinuteTurn, ink));
            visual.Shapes.Add(Hand(compositor, centre, 3.2f * k, line, 120, HourTurn, ink));

            return visual;
        }

        private static CompositionSpriteShape Disc(Compositor compositor, Vector2 centre, float radius, Color color)
        {
            var geometry = compositor.CreateEllipseGeometry();
            geometry.Center = centre;
            geometry.Radius = new Vector2(radius);

            var shape = compositor.CreateSpriteShape(geometry);
            shape.FillBrush = compositor.CreateColorBrush(color);

            return shape;
        }

        private static CompositionSpriteShape Hand(Compositor compositor, Vector2 centre, float length, float thickness, float from, TimeSpan turn, Color ink)
        {
            var geometry = compositor.CreateLineGeometry();
            geometry.Start = centre;
            geometry.End = centre - new Vector2(0, length);

            var shape = compositor.CreateSpriteShape(geometry);
            shape.StrokeBrush = compositor.CreateColorBrush(ink);
            shape.StrokeThickness = thickness;
            shape.StrokeStartCap = CompositionStrokeCap.Round;
            shape.StrokeEndCap = CompositionStrokeCap.Round;
            shape.CenterPoint = centre;

            var rotation = compositor.CreateScalarKeyFrameAnimation();
            rotation.InsertKeyFrame(0, from);
            rotation.InsertKeyFrame(1, from + 360, compositor.CreateLinearEasingFunction());
            rotation.Duration = turn;
            rotation.IterationBehavior = AnimationIterationBehavior.Forever;

            shape.StartAnimation("RotationAngleInDegrees", rotation);
            return shape;
        }
    }
}
