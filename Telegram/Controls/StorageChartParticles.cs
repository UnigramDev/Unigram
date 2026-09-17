//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.UI.Composition;

namespace Telegram.Controls
{
    // The icons drifting out through StorageChart's ring.
    //
    // Ported from CacheChart, which rasterizes one SVG per sector and stamps it around the arc every
    // 7 degrees inside the sector's own draw pass - so the whole field is redrawn on the UI thread
    // every frame. Here the glyph is a TextBlock rasterized once into a frozen
    // CompositionVisualSurface and every particle is a SpriteVisual sharing that one brush, so the
    // UI thread does nothing once the field exists.
    //
    // Each particle's motion is a loop with a period of its own, and it is baked into key frames
    // rather than expressed. The first cut gave every particle three ExpressionAnimations - offset,
    // opacity and scale, each a nested tree of Mod, Lerp, Sin and Clamp over a shared clock - and
    // that turned out to be the single biggest thing the compositor was doing: 52 particles, 156
    // trees, every frame. A key frame track is a lerp between two cached values. Nothing here is
    // expressed any more except the one fade on the container that holds the whole field down until
    // the chart has loaded.
    //
    // The cost is that the key frames bake the ring's geometry in, so the field is rebuilt when that
    // changes. It only has to be right in the state where the particles are visible at all, which is
    // the settled loaded ring, and that geometry does not move.
    //
    // The arithmetic is lifted out of CacheChart.Sector.drawParticles.
    // One sector's slice of the ring, and the icon that belongs to it.
    public struct StorageChartBand
    {
        public float From;
        public float To;
        public CompositionSurfaceBrush Brush;
        public Vector2 Size;
    }

    public sealed partial class StorageChartParticles
    {
        // Samples per loop for the two curved tracks. The offset track is exactly linear in t and
        // takes four frames however long it is.
        private const int Steps = 24;

        // Half the gap the wrap key frames sit either side of, as a fraction of the loop.
        private const float Edge = .0005f;

        private const float Sqrt2 = 1.4142136f;

        // A trip is ten seconds at unit speed.
        private const float Period = 10;

        // A band with room for fewer than this many gets none at all. One or two icons adrift in a
        // narrow sector read as a mistake rather than as a field.
        private const int MinParticles = 2;

        private readonly Compositor _compositor;
        private readonly ContainerVisual _container;
        private readonly CompositionEasingFunction _linear;

        private IList<StorageChartBand> _bands;
        private int _count;

        private Vector2 _center;
        private float _inner;
        private float _outer;
        private float _glyph;

        public StorageChartParticles(Compositor compositor, ContainerVisual container, CompositionPropertySet state)
        {
            _compositor = compositor;
            _container = container;
            _linear = compositor.CreateLinearEasingFunction();

            // The one expression left. The field is held down until the chart has nearly finished
            // loading, and the curve is steep enough that it is still invisible at three quarters.
            // S.Particles is the chart hiding it while the bands it was built from are out of date.
            // On the container, so it multiplies through every particle for the price of one instead
            // of sitting inside all of them.
            var fade = compositor.CreateExpressionAnimation(
                "Max(0, (1 - S.Loading) / 0.75 - 0.75) * (1 - S.Complete) * S.Particles");

            fade.SetReferenceParameter("S", state);

            container.StartAnimation("Opacity", fade);
        }

        // The band the particles travel through and the size to draw the glyph at, baked into the
        // key frames - so setting it rebuilds the field.
        public void SetRing(Vector2 center, float inner, float outer, float glyph)
        {
            if (_center == center && _inner == inner && _outer == outer && _glyph == glyph)
            {
                return;
            }

            _center = center;
            _inner = inner;
            _outer = outer;
            _glyph = glyph;

            Rebuild();
        }

        // One band per sector, each with the icon for its category, which a single glyph shared by
        // the whole ring never was.
        //
        // A band's particles are placed on a grid of `step` degrees and only on the steps that fall
        // strictly inside it. Clipping each particle to its sector's path, which is how the original
        // affords to overshoot, is not available here - nothing clips angularly - so a particle past
        // the edge would be the wrong icon over the next category.
        public void SetBands(IList<StorageChartBand> bands)
        {
            _bands = bands;

            Rebuild();
        }

        public int Count
        {
            get => _count;
            set
            {
                if (_count == value)
                {
                    return;
                }

                _count = value;
                Rebuild();
            }
        }

        private void Rebuild()
        {
            var previous = new List<Visual>();

            foreach (var child in _container.Children)
            {
                previous.Add(child);
            }

            // Unparent before disposing: a visual that is still in a collection when it goes is a
            // use-after-free waiting for the next commit.
            _container.Children.RemoveAll();

            foreach (var child in previous)
            {
                child.Dispose();
            }

            if (_bands == null || _count <= 0 || _outer <= 0)
            {
                return;
            }

            var step = 360f / _count;

            // Half a glyph's diagonal, as an angle at the middle of the ring.
            //
            // A particle is a square sprite centred on its step, so one placed closer than this to
            // the edge of its band hangs over the separator and into the next category - wearing the
            // wrong icon. Clipping each particle to its sector's path would cut one at the edge in
            // half and settle it, but nothing clips angularly here, so the band is narrowed instead
            // and the particles keep clear of the edge.
            var inset = _glyph * Sqrt2 / 2 / ((_inner + _outer) / 2) * 180 / (float)Math.PI;

            foreach (var band in _bands)
            {
                if (band.Brush == null)
                {
                    continue;
                }

                var from = (int)Math.Ceiling((band.From + inset) / step);
                var to = (int)Math.Floor((band.To - inset) / step);

                // Which also settles the small sectors: once both edges are kept clear there is
                // nothing left in a narrow one to put a particle on.
                if (to - from + 1 < MinParticles)
                {
                    continue;
                }

                for (int i = from; i <= to; i++)
                {
                    _container.Children.InsertAtTop(CreateParticle(i * step, band));
                }
            }
        }

        private SpriteVisual CreateParticle(float angle, StorageChartBand band)
        {
            var radians = angle * (float)Math.PI / 180;
            var direction = new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians));

            // Both of these are Math.sin of an angle in degrees fed to a function that wants
            // radians. It is meant as a hash, not as a curve, so it is copied as written.
            var speed = 1f + ((float)Math.Sin(angle * 2000) + 1) * .25f;
            var jitter = .8f + ((float)Math.Sin(angle) + 1) * .25f;

            // Upstream t is ((time + 100) * speed) % 1, so at time zero the particle is already
            // this far along. With one loop per particle, that is simply where its track starts.
            var phase = Frac(100 * speed);

            var sprite = _compositor.CreateSpriteVisual();
            sprite.Brush = band.Brush;
            sprite.Size = band.Size;
            sprite.AnchorPoint = new Vector2(.5f);

            var offset = _compositor.CreateVector3KeyFrameAnimation();
            var opacity = _compositor.CreateScalarKeyFrameAnimation();
            var scale = _compositor.CreateVector3KeyFrameAnimation();

            // t runs phase -> 1, jumps to 0, then runs 0 -> phase. The jump is the particle leaving
            // the outside of the ring and reappearing on the inside; left to a linear segment it
            // would fly back across the whole band over one sample instead.
            var wrap = 1 - phase;
            var size = .75f * jitter * _glyph / Math.Max(band.Size.X, band.Size.Y);

            for (int s = 0; s <= Steps; s++)
            {
                var progress = (float)s / Steps;
                var t = Frac(phase + progress);

                opacity.InsertKeyFrame(progress, Alpha(t), _linear);
                scale.InsertKeyFrame(progress, Size(size, t), _linear);
            }

            if (wrap > Edge && wrap < 1 - Edge)
            {
                opacity.InsertKeyFrame(wrap - Edge, Alpha(1), _linear);
                opacity.InsertKeyFrame(wrap + Edge, Alpha(0), _linear);

                scale.InsertKeyFrame(wrap - Edge, Size(size, 1), _linear);
                scale.InsertKeyFrame(wrap + Edge, Size(size, 0), _linear);

                // The travel is linear in t, so the only frames it needs are the ends of the two
                // runs either side of the jump.
                offset.InsertKeyFrame(0, Position(phase, direction), _linear);
                offset.InsertKeyFrame(wrap - Edge, Position(1, direction), _linear);
                offset.InsertKeyFrame(wrap + Edge, Position(0, direction), _linear);
                offset.InsertKeyFrame(1, Position(phase, direction), _linear);
            }
            else
            {
                offset.InsertKeyFrame(0, Position(0, direction), _linear);
                offset.InsertKeyFrame(1, Position(1, direction), _linear);
            }

            // Each particle loops on a clock of its own, which is what a per-particle speed amounts
            // to once the shared unbounded time is gone.
            var duration = TimeSpan.FromSeconds(Period / speed);

            foreach (var animation in new KeyFrameAnimation[] { offset, opacity, scale })
            {
                animation.Duration = duration;
                animation.IterationBehavior = AnimationIterationBehavior.Forever;
            }

            sprite.StartAnimation("Offset", offset);
            sprite.StartAnimation("Opacity", opacity);
            sprite.StartAnimation("Scale", scale);

            return sprite;
        }

        // The trip starts a glyph's diagonal inside the ring and ends one outside it, so a particle
        // is already moving when it crosses into the band.
        private float Radius(float t)
        {
            var from = _inner - _glyph * Sqrt2;
            var to = _outer + _glyph * Sqrt2;

            return from + (to - from) * t;
        }

        private Vector3 Position(float t, Vector2 direction)
        {
            var r = Radius(t);

            return new Vector3(_center.X + r * direction.X, _center.Y + r * direction.Y, 0);
        }

        private float Alpha(float t)
        {
            // The bell over the trip, times the breath. Nothing here fades a particle at the ring's
            // edges any more - the chart puts a real geometric clip on the container, and fading a
            // whole sprite by how much of it is outside always leaves some of it showing.
            return Clamp(.65f * (1 - 1.75f * Math.Abs(t - .5f)) * Breathe(t));
        }

        private static Vector3 Size(float size, float t)
        {
            var s = size * Breathe(t);

            return new Vector3(s, s, 1);
        }

        private static float Breathe(float t)
        {
            return 1 + .25f * ((float)Math.Sin(t * Math.PI) - 1);
        }

        private static float Clamp(float value)
        {
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }

        private static float Frac(float value)
        {
            var f = value - (float)Math.Floor(value);
            return f < 0 ? f + 1 : f;
        }
    }
}
