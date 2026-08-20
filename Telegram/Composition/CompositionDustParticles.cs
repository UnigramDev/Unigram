//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace Telegram.Composition
{
    /// <summary>
    /// The faithful shape of the effect: the snapshot is cut into a grid and every tile leaves on
    /// its own, with its own delay, direction and speed.
    /// </summary>
    /// <remarks>
    /// Costs one sprite, one clip and three animations per tile — several thousand composition
    /// objects for a large bubble, built on the UI thread. <see cref="CompositionDustLayers"/> is
    /// the cheap alternative; this one is what it is judged against.
    ///
    /// Sharing the animations between tiles that leave at the same moment was tried and reverted:
    /// it means quantizing the departure times, and even at half a frame the burst stops reading as
    /// a bubble turning into dust and starts looking scattered from the first frame. The
    /// per-particle randomness is the effect, not an implementation detail.
    /// </remarks>
    public partial class CompositionDustParticles : CompositionDustVisual
    {
        public static int MaxParticles = 800;
        public static float MinParticleSize = 4;

        public static double SweepDuration = 260;
        public static double ParticleDuration = 560;
        public static double ParticleJitter = 140;

        public static float DriftX = 72;
        public static float DriftY = -28;
        public static float DriftSpread = 36;

        public static float ParticleScale = 0.2f;

        public CompositionDustParticles(UIElement host)
            : base(host)
        {
        }

        protected override void Build(Compositor compositor, ContainerVisual burst, CompositionSurfaceBrush source, Vector2 size, Vector3 origin, int direction)
        {
            var step = Math.Max(MinParticleSize, MathF.Sqrt(size.X * size.Y / MaxParticles));

            var columns = Math.Max(1, (int)MathF.Ceiling(size.X / step));
            var rows = Math.Max(1, (int)MathF.Ceiling(size.Y / step));

            var width = size.X / columns;
            var height = size.Y / rows;

            // One easing shared by every particle: the burst is fast at the start and settles.
            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.6f), new Vector2(0.2f, 1));

            for (int i = 0; i < columns; i++)
            {
                // The wave crosses the bubble from one edge to the other and a particle does not
                // move at all until it arrives. That is what reads as disintegrating rather than as
                // fading out.
                var column = direction < 0 ? columns - i - 1 : i;
                var wave = column / (double)columns * SweepDuration;

                for (int j = 0; j < rows; j++)
                {
                    var x = i * width;
                    var y = j * height;

                    // Each particle is the whole bubble cut down to its own tile by a clip, so the
                    // snapshot is realized once and sampled by one brush. Cutting it with a
                    // per-particle brush offset instead would depend on how a surface brush sizes a
                    // frozen surface, which is not something this relies on anywhere else.
                    var clip = compositor.CreateInsetClip();
                    clip.LeftInset = x;
                    clip.TopInset = y;
                    clip.RightInset = size.X - x - width;
                    clip.BottomInset = size.Y - y - height;

                    var particle = compositor.CreateSpriteVisual();
                    particle.Brush = source;
                    particle.Size = size;
                    particle.Offset = origin;
                    particle.Clip = clip;
                    particle.CenterPoint = new Vector3(x + width / 2, y + height / 2, 0);

                    var delay = TimeSpan.FromMilliseconds(wave + _random.NextDouble() * ParticleJitter);
                    var duration = TimeSpan.FromMilliseconds(ParticleDuration);

                    var drift = compositor.CreateVector3KeyFrameAnimation();
                    drift.InsertKeyFrame(1, origin + new Vector3(direction * (DriftX + Next(DriftSpread)), DriftY + Next(DriftSpread), 0), easing);
                    drift.Duration = duration;
                    drift.DelayTime = delay;

                    var fade = compositor.CreateScalarKeyFrameAnimation();
                    fade.InsertKeyFrame(0, 1);
                    fade.InsertKeyFrame(1, 0);
                    fade.Duration = duration;
                    fade.DelayTime = delay;

                    var shrink = compositor.CreateVector3KeyFrameAnimation();
                    shrink.InsertKeyFrame(1, new Vector3(ParticleScale, ParticleScale, 1), easing);
                    shrink.Duration = duration;
                    shrink.DelayTime = delay;

                    particle.StartAnimation("Offset", drift);
                    particle.StartAnimation("Opacity", fade);
                    particle.StartAnimation("Scale", shrink);

                    burst.Children.InsertAtTop(particle);
                }
            }
        }
    }
}
