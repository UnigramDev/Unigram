//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Navigation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Composition
{
    /// <summary>
    /// The cheap shape of the effect: the snapshot is drawn a handful of times, each copy masked
    /// down to a scattered subset of its pixels, and each copy moves as a unit.
    /// </summary>
    /// <remarks>
    /// A burst is <see cref="Layers"/> sprites and four animations each, whatever the size of the
    /// bubble — against the several thousand objects <see cref="CompositionDustParticles"/> builds.
    /// The pixels are as fine, since the masks are per-pixel; what it cannot have is per-pixel
    /// motion, so the number of distinct trajectories a burst has is the layer count.
    ///
    /// The masks are scattered with a horizontal bias, so layer i sits around x = i / layers and
    /// animating them in order is what recreates the wave crossing the bubble. They are stretched
    /// to fit whatever bubble they mask, which is free and which the eye cannot catch out on noise,
    /// so one set serves every message in the app.
    ///
    /// No effect graph: <see cref="CompositionMaskBrush"/> is a first-class composition brush, so
    /// this is a source and a mask and nothing else.
    /// </remarks>
    public partial class CompositionDustLayers : CompositionDustVisual
    {
        public static int Layers = 16;

        public static double SweepDuration = 260;
        public static double LayerDuration = 560;

        public static float DriftX = 72;
        public static float DriftY = -28;

        // A layer moves as a unit, so all the spread there can be is between layers. Every one of
        // these is re-rolled per burst, or the same message would come apart the same way twice.
        private const float SpeedMin = 0.45f;
        private const float SpeedRange = 1.3f;
        private const float Swing = 1.2f;
        private const float ScaleMin = 0.72f;
        private const float ScaleRange = 0.62f;
        private const float Tumble = 14f;
        private const float StretchMin = 0.8f;
        private const float StretchRange = 0.5f;

        private const int MaskSize = 256;

        // A brush belongs to the compositor that created it and every window has its own, so these
        // are per view. What is shared is the encoded noise behind them, which is the whole cost.
        [ThreadStatic]
        private static CompositionSurfaceBrush[] _masks;
        [ThreadStatic]
        private static int _preparedFor;
        [ThreadStatic]
        private static bool _preparing;

        // The streams have to outlive the call: LoadedImageSurface reads them asynchronously.
        [ThreadStatic]
        private static List<IRandomAccessStream> _streams;

        public CompositionDustLayers(UIElement host)
            : base(host)
        {
            Prepare();
        }

        // Encoding the masks is asynchronous, so an effect created by the very deletion it is meant
        // to animate is not ready in time. The instance is built when the view is, which is early
        // enough that this has always come back by the time anything is deleted.
        public override bool IsReady => _masks != null;

        /// <summary>
        /// The masks depend only on the layer count, so this runs once per view unless the count is
        /// changed from the diagnostics page.
        /// </summary>
        public static void Prepare()
        {
            if (!_preparing && _preparedFor != Layers)
            {
                _ = PrepareAsync();
            }
        }

        protected override void Build(Compositor compositor, ContainerVisual burst, CompositionSurfaceBrush source, Vector2 size, Vector3 origin, int direction)
        {
            var masks = _masks;
            if (masks == null)
            {
                return;
            }

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.6f), new Vector2(0.2f, 1));

            for (int i = 0; i < masks.Length; i++)
            {
                var index = direction < 0 ? masks.Length - i - 1 : i;

                var brush = compositor.CreateMaskBrush();
                brush.Source = source;
                brush.Mask = masks[index];

                var layer = compositor.CreateSpriteVisual();
                layer.Brush = brush;
                layer.Size = size;
                layer.Offset = origin;
                layer.CenterPoint = new Vector3(size.X / 2, size.Y / 2, 0);

                // Jittered inside its own slot, so the wave is not a metronome.
                var delay = TimeSpan.FromMilliseconds((i + _random.NextDouble()) / masks.Length * SweepDuration);
                var duration = TimeSpan.FromMilliseconds(LayerDuration * (StretchMin + _random.NextDouble() * StretchRange));

                var speed = SpeedMin + (float)_random.NextDouble() * SpeedRange;
                var swing = (float)(_random.NextDouble() * 2 - 1);

                // Scaling about the centre is the only spread a layer can have of its own: above 1
                // its pixels move apart, below 1 they close in, and each travels by its distance
                // from the middle rather than with the layer.
                var scale = ScaleMin + (float)_random.NextDouble() * ScaleRange;

                var drift = compositor.CreateVector3KeyFrameAnimation();
                drift.InsertKeyFrame(1, origin + new Vector3(
                    direction * DriftX * speed,
                    DriftY * speed + swing * Math.Abs(DriftY) * Swing, 0), easing);
                drift.Duration = duration;
                drift.DelayTime = delay;

                var fade = compositor.CreateScalarKeyFrameAnimation();
                fade.InsertKeyFrame(0, 1);
                fade.InsertKeyFrame(1, 0);
                fade.Duration = duration;
                fade.DelayTime = delay;

                var stretch = compositor.CreateVector3KeyFrameAnimation();
                stretch.InsertKeyFrame(1, new Vector3(scale, scale, 1), easing);
                stretch.Duration = duration;
                stretch.DelayTime = delay;

                var tumble = compositor.CreateScalarKeyFrameAnimation();
                tumble.InsertKeyFrame(1, swing * Tumble, easing);
                tumble.Duration = duration;
                tumble.DelayTime = delay;

                layer.StartAnimation("Offset", drift);
                layer.StartAnimation("Opacity", fade);
                layer.StartAnimation("Scale", stretch);
                layer.StartAnimation("RotationAngleInDegrees", tumble);

                burst.Children.InsertAtTop(layer);
            }
        }

        private static async Task PrepareAsync()
        {
            _preparing = true;

            var compositor = BootStrapper.Current.Compositor;

            var count = Layers;
            var streams = await EncodeAsync(count);

            var brushes = new CompositionSurfaceBrush[count];
            var clones = _streams ??= new List<IRandomAccessStream>();

            for (int i = 0; i < count; i++)
            {
                // A clone reads the same bytes through a cursor of its own, so two views loading the
                // set at the same time do not move each other along.
                var stream = streams[i].CloneStream();
                stream.Seek(0);

                clones.Add(stream);

                var brush = compositor.CreateSurfaceBrush(LoadedImageSurface.StartLoadFromStream(stream));
                brush.Stretch = CompositionStretch.Fill;

                brushes[i] = brush;
            }

            _masks = brushes;
            _preparedFor = count;
            _preparing = false;

            // The count moved again while this was encoding.
            Prepare();
        }

        private static readonly object _encodingLock = new();
        private static Task<IRandomAccessStream[]> _encoding;
        private static int _encodedFor;

        /// <summary>
        /// The encoded masks, which are the same for every view and so are produced once per
        /// session unless the layer count is changed from the diagnostics page.
        /// </summary>
        private static Task<IRandomAccessStream[]> EncodeAsync(int count)
        {
            // Views run on threads of their own, so two of them can ask for this at once.
            lock (_encodingLock)
            {
                if (_encoding == null || _encodedFor != count)
                {
                    _encodedFor = count;
                    _encoding = EncodeMasksAsync(count);
                }

                return _encoding;
            }
        }

        // One pass over MaskSize^2, once. Each pixel belongs to exactly one layer, biased towards
        // that layer's column, so layers leaving in order read as a wave.
        private static async Task<IRandomAccessStream[]> EncodeMasksAsync(int count)
        {
            var masks = new byte[count][];

            for (int i = 0; i < count; i++)
            {
                masks[i] = new byte[MaskSize * MaskSize * 4];
            }

            for (int y = 0; y < MaskSize; y++)
            {
                for (int x = 0; x < MaskSize; x++)
                {
                    var bias = x / (double)MaskSize * 0.7 + _random.NextDouble() * 0.3;
                    var layer = Math.Min(count - 1, (int)(bias * count));

                    var offset = (y * MaskSize + x) * 4;

                    // Premultiplied white: only the alpha is used, but a surface brush samples all
                    // four channels.
                    masks[layer][offset + 0] = 255;
                    masks[layer][offset + 1] = 255;
                    masks[layer][offset + 2] = 255;
                    masks[layer][offset + 3] = 255;
                }
            }

            var streams = new IRandomAccessStream[count];

            for (int i = 0; i < count; i++)
            {
                streams[i] = await EncodeAsync(masks[i]);
            }

            return streams;
        }

        private static async Task<IRandomAccessStream> EncodeAsync(byte[] pixels)
        {
            var stream = new InMemoryRandomAccessStream();

            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, MaskSize, MaskSize, 96, 96, pixels);
            await encoder.FlushAsync();

            return stream;
        }
    }
}
