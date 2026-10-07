//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Wallet
{
    /// <summary>
    /// Small four-pointed stars in the stone's own blues, thrown out where a transfer the stone
    /// carried has gone through, and fading as they slow down.
    /// </summary>
    /// <remarks>
    /// The prototype's celebration, star for star. Each one's whole life is worked out as it is
    /// thrown and handed to the compositor as keyframes: the prototype steps its stars every frame,
    /// and their motion - a speed bled away by drag - has an exact answer, so nothing here runs per
    /// frame. In an overlay of its own, above everything in the window, which goes when the last
    /// star does.
    /// </remarks>
    public sealed class WalletSparks
    {
#if DEBUG
        // Fires on a transfer that failed as well, which is what every debug send does while they
        // are spoiled on purpose.
        public const bool DebugCelebrateFailures = true;
#else
        public const bool DebugCelebrateFailures = false;
#endif

        public enum Spread
        {
            // Mostly off to the left, as from the right end of a row.
            AlongRow,

            // All the way round, as from the middle of a card.
            Around
        }

        private static readonly Color[] SparkColors =
        {
            Color.FromArgb(0xFF, 0x0A, 0x3C, 0xE0),
            Color.FromArgb(0xFF, 0x00, 0x57, 0xFF),
            Color.FromArgb(0xFF, 0x1E, 0x7B, 0xFF),
            Color.FromArgb(0xFF, 0x34, 0xA8, 0xFF),
            Color.FromArgb(0xFF, 0x63, 0xD2, 0xFF),
            Color.FromArgb(0xFF, 0x9B, 0xE9, 0xFF)
        };

        private const int Count = 56;

        // How much speed each star bleeds away a second, as e^-drag.
        private const float Drag = 2.4f;

        // Room for the largest star at full size, which its visual is clipped to.
        private const float Box = 16;

        private const int PathSamples = 8;

        private readonly Popup _overlay;

        private WalletSparks(Popup overlay)
        {
            _overlay = overlay;
        }

        /// <summary>
        /// Throws a burst of stars from <paramref name="at"/>, in window coordinates.
        /// </summary>
        public static void Burst(XamlRoot xamlRoot, Vector2 at, Spread spread)
        {
            if (xamlRoot == null)
            {
                return;
            }

            var canvas = new Canvas
            {
                IsHitTestVisible = false
            };

            var overlay = new Popup
            {
                XamlRoot = xamlRoot,
                IsHitTestVisible = false,
                Child = canvas
            };

            overlay.IsOpen = true;

            var compositor = ElementCompositionPreview.GetElementVisual(canvas).Compositor;
            var container = compositor.CreateContainerVisual();

            ElementCompositionPreview.SetElementChildVisual(canvas, container);

            var sparkle = compositor.CreatePathGeometry(new CompositionPath(CreateSparkle()));

            var brushes = new CompositionColorBrush[SparkColors.Length];
            for (int i = 0; i < brushes.Length; i++)
            {
                brushes[i] = compositor.CreateColorBrush(SparkColors[i]);
            }

            var random = new Random();
            var linear = compositor.CreateLinearEasingFunction();
            var fade = compositor.CreateCubicBezierEasingFunction(new Vector2(0.42f, 0), new Vector2(0.58f, 1));

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            for (int i = 0; i < Count; i++)
            {
                Throw(compositor, container, sparkle, brushes[random.Next(brushes.Length)], linear, fade, random, at, spread, i);
            }

            batch.End();

            var burst = new WalletSparks(overlay);
            batch.Completed += burst.Batch_Completed;
        }

        private void Batch_Completed(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= Batch_Completed;
            }

            _overlay.IsOpen = false;
            _overlay.Child = null;
        }

        private static void Throw(Compositor compositor, ContainerVisual container, CompositionPathGeometry sparkle, CompositionColorBrush brush,
            LinearEasingFunction linear, CubicBezierEasingFunction fade, Random random, Vector2 at, Spread spread, int index)
        {
            // The prototype's mix along a row: seven in ten run off to the left, two spread out to
            // the sides on the way, one goes anywhere. Around a card, every one goes anywhere, at
            // the speeds of the first group.
            float angle;
            float speed;

            if (spread == Spread.Around)
            {
                angle = Pick(random, 0, MathF.PI * 2);
                speed = Pick(random, 60, 360);
            }
            else
            {
                switch (index % 10)
                {
                    case <= 6:
                        angle = MathF.PI + Pick(random, -0.3f, 0.3f);
                        speed = Pick(random, 140, 560);
                        break;
                    case 7:
                    case 8:
                        angle = MathF.PI + (random.Next(2) == 0 ? 1 : -1) * Pick(random, 0.5f, 1.4f);
                        speed = Pick(random, 60, 240);
                        break;
                    default:
                        angle = Pick(random, 0, MathF.PI * 2);
                        speed = Pick(random, 40, 160);
                        break;
                }
            }

            var size = Pick(random, 2.2f, 6.5f);
            var life = Pick(random, 0.8f, 1.4f);
            var turn = Pick(random, 0, 90);
            var spin = Pick(random, -180, 180);

            var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed;
            var duration = WalletTransferFlight.Slow(TimeSpan.FromSeconds(life));

            var shape = compositor.CreateSpriteShape(sparkle);
            shape.FillBrush = brush;
            shape.Offset = new Vector2(Box / 2);
            shape.Scale = new Vector2(size);
            shape.RotationAngleInDegrees = turn;

            var visual = compositor.CreateShapeVisual();
            visual.Size = new Vector2(Box);
            visual.Shapes.Add(shape);
            visual.Offset = new Vector3(at - new Vector2(Box / 2), 0);
            visual.Opacity = 0;

            container.Children.InsertAtTop(visual);

            // Where a speed bled away by drag has carried it: v / drag of the way, approached ever
            // more slowly.
            var path = compositor.CreateVector3KeyFrameAnimation();
            for (int i = 1; i <= PathSamples; i++)
            {
                var t = life * i / PathSamples;
                var position = at + velocity / Drag * (1 - MathF.Exp(-Drag * t)) - new Vector2(Box / 2);

                path.InsertKeyFrame((float)i / PathSamples, new Vector3(position, 0), linear);
            }

            path.Duration = duration;

            // Pops in at once, holds, then fades away over the last two thirds.
            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0.125f, 1, linear);
            opacity.InsertKeyFrame(0.35f, 1, linear);
            opacity.InsertKeyFrame(1, 0, fade);
            opacity.Duration = duration;

            // And shrinks to half as it goes, turning.
            var scale = compositor.CreateVector2KeyFrameAnimation();
            scale.InsertKeyFrame(1, new Vector2(size * 0.5f), linear);
            scale.Duration = duration;

            var rotation = compositor.CreateScalarKeyFrameAnimation();
            rotation.InsertKeyFrame(1, turn + spin * life, linear);
            rotation.Duration = duration;

            visual.StartAnimation("Offset", path);
            visual.StartAnimation("Opacity", opacity);
            shape.StartAnimation("Scale", scale);
            shape.StartAnimation("RotationAngleInDegrees", rotation);
        }

        private static float Pick(Random random, float from, float to)
        {
            return from + (float)random.NextDouble() * (to - from);
        }

        /// <summary>
        /// A four-pointed sparkle with concave, softly rounded sides, centred on the origin with a
        /// tip radius of one - the prototype's.
        /// </summary>
        private static CanvasGeometry CreateSparkle()
        {
            const float pinch = 0.12f;

            using var builder = new CanvasPathBuilder(null);

            builder.BeginFigure(new Vector2(0, -1));
            builder.AddQuadraticBezier(new Vector2(pinch, -pinch), new Vector2(1, 0));
            builder.AddQuadraticBezier(new Vector2(pinch, pinch), new Vector2(0, 1));
            builder.AddQuadraticBezier(new Vector2(-pinch, pinch), new Vector2(-1, 0));
            builder.AddQuadraticBezier(new Vector2(-pinch, -pinch), new Vector2(0, -1));
            builder.EndFigure(CanvasFigureLoop.Closed);

            return CanvasGeometry.CreatePath(builder);
        }
    }
}
