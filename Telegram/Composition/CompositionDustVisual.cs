//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Navigation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Composition
{
    public enum MessageDustEffect
    {
        Disabled,
        Particles,
        Layers
    }

    /// <summary>
    /// The send-off a deleted message gets: the bubble comes apart and blows away.
    /// </summary>
    /// <remarks>
    /// The snapshot is a frozen <see cref="CompositionVisualSurface"/> rather than a
    /// RenderTargetBitmap. It needs no readback, and it captures the composition child visuals that
    /// carry animated stickers, videos and custom emoji, which RenderTargetBitmap leaves blank.
    /// Whatever the burst is cut into then samples that one surface, so it costs a single
    /// realization of the bubble however many pieces there are.
    ///
    /// <see cref="Capture"/> and <see cref="Play"/> are separate calls because they cannot happen on
    /// the same frame: a surface captures at the commit that <i>follows</i> the freeze, so by the
    /// time the list has been told to drop the container it is already too late and the texture
    /// comes back empty. Holding the list back for that one frame is
    /// <see cref="Collections.SynchronizedList{T}"/>'s job.
    /// </remarks>
    public abstract partial class CompositionDustVisual
    {
        /// <summary>
        /// Multiplier on the display scale for the snapshot raster. 1 is a faithful capture, one
        /// texel per device pixel, and it is the one allocation here measured in megabytes.
        /// </summary>
        public static float SnapshotScale = 1;

        protected static readonly Random _random = new();

        private readonly UIElement _host;

        private readonly Dictionary<long, Snapshot> _snapshots = new();

        // The completion handler is reachable from the batch only through a managed delegate, so
        // nothing outside this set keeps a burst alive long enough to clean itself up.
        private readonly HashSet<Burst> _bursts = new();

        private ContainerVisual _root;

        protected CompositionDustVisual(UIElement host)
        {
            _host = host;
        }

        public static CompositionDustVisual Create(MessageDustEffect effect, UIElement host)
        {
            return effect switch
            {
                MessageDustEffect.Particles => new CompositionDustParticles(host),
                MessageDustEffect.Layers => new CompositionDustLayers(host),
                _ => null
            };
        }

        /// <summary>
        /// False while the effect is still setting itself up. Refusing to capture is the right
        /// answer then: the row would be held back for a frame to feed a burst that has nothing to
        /// draw with.
        /// </summary>
        public virtual bool IsReady => true;

        /// <summary>
        /// Takes the snapshot the burst is cut from. The pixels are whatever the compositor draws on
        /// its next pass, and the source is free to die after that.
        /// </summary>
        public bool Capture(long id, FrameworkElement source)
        {
            if (!IsReady)
            {
                return false;
            }

            var size = source.ActualSize;
            var scale = source.XamlRoot?.RasterizationScale ?? 0;

            if (size.X < 1 || size.Y < 1 || scale < 1)
            {
                return false;
            }

            var compositor = BootStrapper.Current.Compositor;

            var surface = compositor.CreateVisualSurface();
            surface.SourceVisual = ElementComposition.GetElementVisual(source);
            surface.SourceOffset = Vector2.Zero;
            surface.SourceSize = size;

            if (!surface.TryGetPartner(out var partner))
            {
                return false;
            }

            partner.SetStretch(CompositionStretch.Fill);
            // A visual surface is a raster, and without this dwm picks the size: the burst would
            // sample an upscale on a scaled display.
            partner.SetRealizationSize(size * (float)scale * SnapshotScale);
            partner.Freeze();

            Discard(id);
            _snapshots[id] = new Snapshot(surface, size);
            return true;
        }

        /// <summary>
        /// Blows the snapshot away from <paramref name="origin"/>, which is where the bubble sits in
        /// the host's coordinates. The source is never read again.
        /// </summary>
        public bool Play(long id, Vector2 origin, bool rightToLeft)
        {
            if (!_snapshots.TryGetValue(id, out Snapshot snapshot))
            {
                return false;
            }

            // Handed over to the burst, which owns it from here on.
            _snapshots.Remove(id);

            var compositor = BootStrapper.Current.Compositor;
            var root = _root ??= CreateRoot(compositor);

            var burst = compositor.CreateContainerVisual();
            root.Children.InsertAtTop(burst);

            var brush = compositor.CreateSurfaceBrush(snapshot.Surface);
            brush.Stretch = CompositionStretch.Fill;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            Build(compositor, burst, brush, snapshot.Size, new Vector3(origin, 0), rightToLeft ? -1 : 1);

            batch.End();

            _bursts.Add(new Burst(this, root, burst, batch, snapshot.Surface));
            return true;
        }

        protected abstract void Build(Compositor compositor, ContainerVisual burst, CompositionSurfaceBrush source, Vector2 size, Vector3 origin, int direction);

        /// <summary>
        /// Drops the snapshots of a removal that will never be animated, which is the only way one
        /// is left behind: playing a burst hands its snapshot over to it.
        /// </summary>
        public void Clear()
        {
            foreach (var snapshot in _snapshots.Values)
            {
                snapshot.Surface.Dispose();
            }

            _snapshots.Clear();
        }

        /// <summary>
        /// Takes down whatever is still in the air. A view that is about to show a different chat
        /// must not keep painting the last one's messages over it.
        /// </summary>
        public void Stop()
        {
            if (_bursts.Count > 0)
            {
                foreach (var burst in _bursts.ToArray())
                {
                    burst.Stop();
                }
            }

            Clear();
        }

        private void Discard(long id)
        {
            if (_snapshots.TryGetValue(id, out Snapshot snapshot))
            {
                snapshot.Surface.Dispose();
                _snapshots.Remove(id);
            }
        }

        protected static float Next(float amount)
        {
            return (float)(_random.NextDouble() * 2 - 1) * amount;
        }

        private ContainerVisual CreateRoot(Compositor compositor)
        {
            var root = compositor.CreateContainerVisual();
            root.RelativeSizeAdjustment = Vector2.One;

            // Particles that outrun the viewport must not paint over the header or the composer.
            root.Clip = compositor.CreateInsetClip();

            ElementComposition.SetElementChildVisual(_host, root);
            return root;
        }

        private readonly struct Snapshot
        {
            public readonly CompositionVisualSurface Surface;
            public readonly Vector2 Size;

            public Snapshot(CompositionVisualSurface surface, Vector2 size)
            {
                Surface = surface;
                Size = size;
            }
        }

        private sealed partial class Burst
        {
            private readonly CompositionDustVisual _owner;
            private readonly ContainerVisual _parent;
            private readonly ContainerVisual _visual;
            private readonly CompositionScopedBatch _batch;
            private readonly CompositionVisualSurface _surface;

            public Burst(CompositionDustVisual owner, ContainerVisual parent, ContainerVisual visual, CompositionScopedBatch batch, CompositionVisualSurface surface)
            {
                _owner = owner;
                _parent = parent;
                _visual = visual;
                _batch = batch;
                _surface = surface;

                batch.Completed += OnCompleted;
            }

            private void OnCompleted(object sender, CompositionBatchCompletedEventArgs e)
            {
                Stop();
            }

            // Detaches first, so the batch completing later finds nothing left to take down twice.
            public void Stop()
            {
                _batch.Completed -= OnCompleted;

                _parent.Children.Remove(_visual);
                _visual.Dispose();
                _surface.Dispose();

                _owner._bursts.Remove(this);
            }
        }
    }
}
