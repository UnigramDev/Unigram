//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Composition.Interactions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls
{
    public partial class QrShareEmoji
    {
        public QrShareEmoji(EmojiType type, AnimatedImageSource source)
        {
            Type = type;
            Source = source;
        }

        public EmojiType Type { get; }

        public AnimatedImageSource Source { get; }
    }

    // The backdrop of the QR share screen: a gradient that recolours with a ring wave spreading from the card,
    // and a field of emoji or gift symbols drifting on it. The items are sprites sharing one visual surface per
    // distinct image, rasterized from hidden elements in this grid, and placed every frame while there are any.
    public partial class QrShareBackground : Grid
    {
        private const float WaveDuration = 0.6f;

        // The edge squeeze: near an edge, distances to it are compressed exponentially, so items shrink and
        // fade towards it instead of being cut.
        private const float SqueezeBand = 90;

        private readonly Compositor _compositor;
        private readonly ContainerVisual _root;
        private readonly SpriteVisual _base;
        private readonly ContainerVisual _under;
        private readonly SpriteVisual _wave;
        private readonly SpriteVisual _ring;
        private readonly ContainerVisual _patterns;

        private CompositionGradientBrush _baseBrush;

        private CompositionGradientBrush _waveBrush;
        private CompositionScopedBatch _waveBatch;
        private CompositionEllipseGeometry _waveEllipse;

        private Vector2 _origin;
        private Vector2 _waveOrigin;
        private Vector2 _size;

        public QrShareBackground()
        {
            // Hit-testable, so the field can be dragged around: whatever sits on top of it takes its own
            // clicks first.
            Background = new SolidColorBrush(Colors.Transparent);

            _compositor = BootStrapper.Current.Compositor;

            _base = _compositor.CreateSpriteVisual();
            _base.RelativeSizeAdjustment = Vector2.One;

            // A pattern the wave is replacing: under the new backdrop, which covers it as the circle grows.
            _under = _compositor.CreateContainerVisual();
            _under.RelativeSizeAdjustment = Vector2.One;

            _wave = _compositor.CreateSpriteVisual();
            _wave.RelativeSizeAdjustment = Vector2.One;

            _ring = _compositor.CreateSpriteVisual();
            _ring.Opacity = 0;

            _patterns = _compositor.CreateContainerVisual();
            _patterns.RelativeSizeAdjustment = Vector2.One;

            _root = _compositor.CreateContainerVisual();
            _root.RelativeSizeAdjustment = Vector2.One;
            _root.Children.InsertAtTop(_base);
            _root.Children.InsertAtTop(_under);
            _root.Children.InsertAtTop(_wave);
            _root.Children.InsertAtTop(_patterns);
            _root.Children.InsertAtTop(_ring);

            ElementCompositionPreview.SetElementChildVisual(this, _root);

            SizeChanged += OnSizeChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCanceled += OnPointerCanceled;
            PointerCaptureLost += OnPointerCanceled;
        }

        public DataTemplate EmojiTemplate { get; set; }

        // The QR card's centre at rest: the gift gradient is centred on it and the pattern thins out away
        // from it.
        public Vector2 Origin
        {
            get => _origin;
            set
            {
                if (_origin == value)
                {
                    return;
                }

                _origin = value;
                UpdateBrush(_baseBrush);
                UpdateBrush(_waveBrush);

                // Only where the nodes are placed reads it, every frame.
                if (!_rendering)
                {
                    Tick();
                }
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _size = e.NewSize.ToVector2();

            UpdateBrush(_baseBrush);
            UpdateBrush(_waveBrush);
            Relayout();
        }

        #region Backdrop

        // The wave spreads from waveOrigin, or the backdrop changes at once when there is none.
        public void SetBackdrop(Color center, Color edge, bool radial, Vector2? waveOrigin)
        {
            var brush = CreateBrush(center, edge, radial);

            EndWaveSwap();

            if (_waveBrush != null)
            {
                // A wave is still running: its target becomes the base the new one spreads over.
                _baseBrush = _waveBrush;
                _base.Brush = _waveBrush;

                _waveBrush = null;
                _wave.Brush = null;
                _wave.Clip = null;
                _waveBatch = null;
                _waveEllipse = null;
            }

            if (waveOrigin == null || _baseBrush == null || _size.X == 0 || !PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                _baseBrush = brush;
                _base.Brush = brush;
                return;
            }

            _waveBrush = brush;
            _wave.Brush = brush;
            _waveOrigin = waveOrigin.Value;
            _waveStart = Now();
            _waveId++;

            var maxRadius = MaxWaveRadius();

            var ellipse = _waveEllipse = _compositor.CreateEllipseGeometry();
            ellipse.Center = _waveOrigin;
            ellipse.Radius = Vector2.Zero;

            _wave.Clip = _compositor.CreateGeometricClip(ellipse);

            var linear = _compositor.CreateLinearEasingFunction();

            var radius = _compositor.CreateVector2KeyFrameAnimation();
            var scale = _compositor.CreateVector3KeyFrameAnimation();
            var opacity = _compositor.CreateScalarKeyFrameAnimation();

            // The front starts fast and glides, so the ring is still visible as it crosses the far corners.
            // Sampled, because no cubic bezier has this shape.
            for (int i = 0; i <= 16; i++)
            {
                var t = i / 16f;
                var e = WaveEase(t);

                radius.InsertKeyFrame(t, new Vector2(maxRadius * e), linear);
                scale.InsertKeyFrame(t, new Vector3(e, e, 1), linear);
                opacity.InsertKeyFrame(t, Math.Clamp((1 - t) * 1.6f, 0, 1), linear);
            }

            radius.Duration =
                scale.Duration =
                opacity.Duration = TimeSpan.FromSeconds(WaveDuration);

            var tint = QrShareColors.MixOklab(center, Colors.White, 0.55f);
            var ring = _compositor.CreateRadialGradientBrush();
            ring.ColorStops.Add(_compositor.CreateColorGradientStop(0.80f, Color.FromArgb(0, tint.R, tint.G, tint.B)));
            ring.ColorStops.Add(_compositor.CreateColorGradientStop(0.89f, Color.FromArgb(19, tint.R, tint.G, tint.B)));
            ring.ColorStops.Add(_compositor.CreateColorGradientStop(0.96f, Color.FromArgb(58, tint.R, tint.G, tint.B)));
            ring.ColorStops.Add(_compositor.CreateColorGradientStop(0.99f, Color.FromArgb(97, tint.R, tint.G, tint.B)));
            ring.ColorStops.Add(_compositor.CreateColorGradientStop(1.00f, Color.FromArgb(0, tint.R, tint.G, tint.B)));

            _ring.Brush = ring;
            _ring.Size = new Vector2(maxRadius * 2);
            _ring.Offset = new Vector3(_waveOrigin - new Vector2(maxRadius), 0);
            _ring.CenterPoint = new Vector3(maxRadius, maxRadius, 0);

            _waveBatch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            _waveBatch.Completed += OnWaveCompleted;

            ellipse.StartAnimation("Radius", radius);
            _ring.StartAnimation("Scale", scale);
            _ring.StartAnimation("Opacity", opacity);

            _waveBatch.End();
        }

        private void OnWaveCompleted(object sender, CompositionBatchCompletedEventArgs args)
        {
            if (sender is CompositionScopedBatch batch)
            {
                batch.Completed -= OnWaveCompleted;
            }

            // A newer wave took over: it owns the visuals now.
            if (sender != _waveBatch)
            {
                return;
            }

            _baseBrush = _waveBrush;
            _base.Brush = _waveBrush;

            _waveBrush = null;
            _waveBatch = null;
            _waveEllipse = null;
            _wave.Brush = null;
            _wave.Clip = null;

            EndWaveSwap();
        }

        // The wave has covered everything, or been cut short: the pattern it brought shows whole, and the one
        // it replaced goes.
        private void EndWaveSwap()
        {
            if (_leaving != null && _leaving.Front)
            {
                Remove(_leaving);
                _leaving = null;

                UpdateRendering();
            }

            if (_field != null && _field.Front)
            {
                _field.Front = false;
                _field.Start = float.NegativeInfinity;
                _field.Visual.Clip = null;
            }
        }

        private float MaxWaveRadius()
        {
            var x = MathF.Max(_waveOrigin.X, _size.X - _waveOrigin.X);
            var y = MathF.Max(_waveOrigin.Y, _size.Y - _waveOrigin.Y);

            return MathF.Sqrt(x * x + y * y) + 60;
        }

        private static float WaveEase(float t)
        {
            return 1 - MathF.Pow(1 - t, 2.4f);
        }

        private CompositionGradientBrush CreateBrush(Color center, Color edge, bool radial)
        {
            CompositionGradientBrush brush = radial
                ? _compositor.CreateRadialGradientBrush()
                : _compositor.CreateLinearGradientBrush();

            brush.MappingMode = CompositionMappingMode.Absolute;
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(0, center));
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(radial ? 0.55f : 0.5f, QrShareColors.MixOklab(center, edge, 0.5f)));
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(1, edge));

            UpdateBrush(brush);
            return brush;
        }

        private void UpdateBrush(CompositionGradientBrush brush)
        {
            if (brush is CompositionRadialGradientBrush radialBrush)
            {
                var radius = _size.Length() * 0.62f;

                radialBrush.EllipseCenter = _origin;
                radialBrush.EllipseRadius = new Vector2(radius);
            }
            else if (brush is CompositionLinearGradientBrush linearBrush)
            {
                // 18 degrees off vertical: light in the top-left corner, darkest in the bottom-right one.
                var angle = MathFEx.ToRadians(18);
                var sin = MathF.Sin(angle);
                var cos = MathF.Cos(angle);
                var length = _size.X * sin + _size.Y * cos;

                linearBrush.StartPoint = Vector2.Zero;
                linearBrush.EndPoint = new Vector2(sin, cos) * length;
            }
        }

        #endregion

        #region Pattern

        // One pattern on screen: the hidden elements its images are rasterized from, and its nodes. A new one
        // pops in while the one before shrinks away, so two can be alive at once.
        private sealed class Field
        {
            public readonly List<FrameworkElement> Sources = new();
            public readonly List<CompositionSurfaceBrush> Brushes = new();

            // The same images as solid silhouettes, for the shine to brighten them with: built the first time
            // the band reaches one.
            public readonly List<CompositionBrush> LitBrushes = new();
            public CompositionBrush CoreBrush;
            public Color LitColor = Colors.White;

            public ContainerVisual Visual;

            public Node[] Nodes = Array.Empty<Node>();
            public int Count;

            public IList<QrShareEmoji> Emoji;
            public bool Gift;

            // The lattice repeats every Width x Height, so nodes can wrap around it as the field drifts.
            public float Width;
            public float Height;

            public float Start;
            public bool Leaving;
            public bool Front;
            public bool Alone;
        }

        private struct Node
        {
            public SpriteVisual Sprite;
            public byte Kind;
            public bool Visible;

            // The lattice point with its jitter, the size its spacing was planned for, and the size drawn.
            public float X;
            public float Y;
            public float Size;
            public float Drawn;

            public float Angle;
            public float Alpha;
            public float Delay;

            public float OrbitRadius;
            public float OrbitSpeed;
            public float OrbitPhase;
            public float Phase;

            public float TwinkleSpeed;
            public float TwinklePhase;

            public int Source;
            public float Mass;

            // The shine's light over the item, and for symbols its metallic core.
            public SpriteVisual Lit;
            public SpriteVisual Core;

            // What the sprites were last given, mirrored here: reading a composition property back is a call
            // across the ABI, and most frames would write the same value again.
            public float Opacity;
            public float LitOpacity;
            public float CoreOpacity;

            // The colour wave's kick: a spring per node for an offset, a tilt and a scale pulse.
            public int Hit;
            public bool Springing;
            public float SpringX;
            public float SpringY;
            public float SpringVX;
            public float SpringVY;
            public float SpringR;
            public float SpringVR;
            public float SpringS;
            public float SpringVS;

            // The shine's push, followed through two cascaded filters so it rises and settles softly.
            public float PushO1;
            public float PushO2;
            public float PushL1;
            public float PushL2;
        }

        private const byte KindEmoji = 0;
        private const byte KindSymbol = 1;
        private const byte KindModel = 2;

        private const float Shrink = 0.18f;
        private const float GrowAt = 0.135f;
        private const float Grow = 0.315f;
        private const float Spread = 0.3f;

        // The idle drift: about 6 px/s up and to the left, the heading wandering slowly around that.
        private const float DriftSpeed = 6.2f;
        private static readonly float DriftAngle = MathF.Atan2(-5.5f, -3.5f);

        private Field _field;
        private Field _leaving;

        private readonly long _clockStart = Stopwatch.GetTimestamp();
        private float _lastTick = -1;
        private float _waveStart;
        private float _driftX;
        private float _driftY;
        private bool _rendering;

        private int _waveId;
        private float _waveProgress = 1;

        // This frame's MaxWaveRadius and how far the front has got.
        private float _waveRadius = 1;
        private float _waveFront;

        // Dragging the field: the finger's offset and then a glide that eases out, while the idle motion
        // stops under the finger and picks up again a moment after it lets go.
        private const float GlideTau = 0.35f;
        private const float MaxFling = 2600;

        private float _panX;
        private float _panY;
        private float _panBaseX;
        private float _panBaseY;
        private Point _downPoint;
        private bool _panning;
        private bool _panMoved;

        private bool _gliding;
        private float _glideStart;
        private float _glideFromX;
        private float _glideFromY;
        private float _glideVX;
        private float _glideVY;

        // Touch and touchpad pans go to a tracker, which carries their inertia too. It never gets the mouse,
        // which is dragged by hand above.
        private InteractionTracker _tracker;
        private VisualInteractionSource _interactionSource;
        private WeakInteractionTrackerOwner _trackerOwner;
        private Vector3 _trackerPosition;
        private bool _trackerAttached;
        private bool _tracking;

        private float _idle = 1;
        private float _idleResumeAt;
        private float _motion;

        // The last few pointer positions, for the velocity at release.
        private readonly float[] _trackT = new float[8];
        private readonly float[] _trackX = new float[8];
        private readonly float[] _trackY = new float[8];
        private int _trackCount;

        // The shine: a soft band of light sweeping over the pattern as if it lay on a cylinder, the first one a
        // moment after the code has built and then one after the other, each at a slightly different angle.
        private const float ShineWidth = 61;
        private const float ShineDuration = 4;
        private const float ShineAngle = 193;

        private readonly Random _random = new();

        private float _sweepStart = float.NaN;
        private float _sweepDX;
        private float _sweepDY;
        private float _shineCenter;
        private float _shineAmplitude = 1;
        private bool _shining;

        public void SetEmojiPattern(IList<QrShareEmoji> emoji, bool animated, bool withWave)
        {
            var field = new Field
            {
                Emoji = emoji
            };

            foreach (var item in emoji)
            {
                AddSource(field, new ContentControl
                {
                    Content = item,
                    ContentTemplate = EmojiTemplate,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                    IsTabStop = false
                }, 64);
            }

            Show(field, animated, withWave);
        }

        public void SetGiftPattern(IClientService clientService, UpgradedGift gift, Color ink, bool animated, bool withWave)
        {
            var field = new Field
            {
                Gift = true,
                LitColor = QrShareColors.Mix(QrShareColors.Brighter(ink), Colors.White, 0.24f)
            };

            AddSource(field, new AnimatedImage
            {
                Source = DelayedFileSource.FromSticker(clientService, gift.Symbol.Sticker),
                ReplacementColor = new SolidColorBrush(Color.FromArgb(0xFF, ink.R, ink.G, ink.B)),
                FrameSize = new Size(48, 48),
                DecodeFrameType = DecodePixelType.Logical,
                AutoPlay = false,
                IsViewportAware = false
            }, 48);

            AddSource(field, new AnimatedImage
            {
                Source = new DelayedFileSource(clientService, gift.Model.Sticker),
                FrameSize = new Size(96, 96),
                DecodeFrameType = DecodePixelType.Logical,
                AutoPlay = false,
                IsViewportAware = false
            }, 96);

            Show(field, animated, withWave);
        }

        public void ClearPattern(bool animated, bool withWave)
        {
            Show(null, animated, withWave);
        }

        private void AddSource(Field field, FrameworkElement element, float size)
        {
            element.Width = size;
            element.Height = size;
            element.Opacity = 0;
            element.IsHitTestVisible = false;
            element.HorizontalAlignment = HorizontalAlignment.Left;
            element.VerticalAlignment = VerticalAlignment.Top;

            Children.Add(element);
            field.Sources.Add(element);

            var surface = _compositor.CreateVisualSurface();
            surface.SourceVisual = ElementComposition.GetElementVisual(element);
            surface.SourceSize = new Vector2(size);

            var brush = _compositor.CreateSurfaceBrush(surface);
            brush.Stretch = CompositionStretch.Fill;

            field.Brushes.Add(brush);
        }

        private void Show(Field field, bool animated, bool withWave)
        {
            animated &= PowerSavingPolicy.AreSmoothTransitionsEnabled && _size.X > 0;

            var now = Now();
            var old = _field;

            // With a wave running, the patterns swap exactly at its front, both at full size: the new one is
            // clipped to the circle, and the old one goes under the new backdrop, which covers it as it grows.
            var front = animated && withWave && _waveEllipse != null;

            // Only one pattern leaves at a time: one already on its way out goes at once.
            Remove(_leaving);
            _leaving = null;

            if (old != null)
            {
                if (front)
                {
                    old.Leaving = true;
                    old.Front = true;

                    _patterns.Children.Remove(old.Visual);
                    _under.Children.InsertAtTop(old.Visual);

                    _leaving = old;
                }
                else if (animated)
                {
                    // One that came in with a wave still running shrinks away whole rather than stay at the front.
                    old.Leaving = true;
                    old.Front = false;
                    old.Start = now;
                    old.Visual.Clip = null;

                    _leaving = old;
                }
                else
                {
                    Remove(old);
                }
            }

            _field = field;

            if (field != null)
            {
                field.Start = animated ? now : float.NegativeInfinity;
                field.Front = front;
                field.Alone = old == null;

                field.Visual = _compositor.CreateContainerVisual();
                field.Visual.RelativeSizeAdjustment = Vector2.One;
                _patterns.Children.InsertAtTop(field.Visual);

                if (front)
                {
                    field.Visual.Clip = _compositor.CreateGeometricClip(_waveEllipse);
                }

                Layout(field);
            }

            UpdateRendering();
            Tick();
        }

        private void Remove(Field field)
        {
            if (field == null)
            {
                return;
            }

            DisposeNodes(field);

            if (field.Visual != null)
            {
                if (field.Visual.Parent is ContainerVisual parent)
                {
                    parent.Children.Remove(field.Visual);
                }

                field.Visual.Dispose();
                field.Visual = null;
            }

            foreach (var element in field.Sources)
            {
                Children.Remove(element);
            }

            foreach (var brush in field.Brushes)
            {
                if (brush.Surface is CompositionVisualSurface surface)
                {
                    surface.Dispose();
                }

                brush.Dispose();
            }

            foreach (var brush in field.LitBrushes)
            {
                brush?.Dispose();
            }

            field.CoreBrush?.Dispose();
            field.CoreBrush = null;

            field.Sources.Clear();
            field.Brushes.Clear();
            field.LitBrushes.Clear();
        }

        private static void DisposeNodes(Field field)
        {
            for (int i = 0; i < field.Count; i++)
            {
                ref var node = ref field.Nodes[i];

                node.Sprite?.Dispose();
                node.Lit?.Dispose();
                node.Core?.Dispose();
                node = default;
            }

            field.Count = 0;
        }

        private void Relayout()
        {
            if (_field != null)
            {
                Layout(_field);
            }

            // A pattern on its way out keeps the layout it left with; a new size just ends it.
            Remove(_leaving);
            _leaving = null;

            UpdateRendering();
            Tick();
        }

        private void Layout(Field field)
        {
            field.Visual.Children.RemoveAll();
            DisposeNodes(field);

            if (_size.X == 0 || _size.Y == 0)
            {
                return;
            }

            if (field.Gift)
            {
                LayoutGift(field);
            }
            else if (field.Emoji?.Count > 0)
            {
                LayoutEmoji(field);
            }
        }

        private void LayoutEmoji(Field field)
        {
            // Hex-staggered lattice, odd rows shifted by half a cell, every node filled: left empty at random,
            // a node reads as a missing emoji rather than as spacing. Each node's hash picks its size, tilt and
            // motion; the emoji repeat along the lattice's diagonals.
            const float dx = 86;
            const float dy = 70;

            var margin = SqueezeBand * 1.3f;

            var cols = (int)MathF.Ceiling((_size.X + 2 * margin) / dx);
            var rows = (int)MathF.Ceiling((_size.Y + 2 * margin) / (2 * dy)) * 2;

            field.Width = cols * dx;
            field.Height = rows * dy;

            var count = field.Emoji.Count;

            for (int r = 0; r < rows; r++)
            {
                var shift = (r & 1) == 0 ? 0 : dx / 2;

                for (int col = 0; col < cols; col++)
                {
                    var hh = LatticeHash(r, col);
                    var big = (r & 1) == 0;
                    var size = 38 * (big ? 1.3f : 0.9f) * (0.92f + 0.16f * (float)hh);
                    var q = col - (r - (r & 1)) / 2;

                    var source = Wrap(q - r, count);

                    ref var node = ref AddNode(field, field.Brushes[source]);
                    node.Kind = KindEmoji;
                    node.Source = source;
                    node.Mass = 1 + MathF.Max(0, size / 24 - 1) * 0.8f;
                    node.X = col * dx + shift;
                    node.Y = r * dy;
                    node.Size = size;

                    // The largest tier draws a tenth smaller; spacing and placement keep the original size.
                    node.Drawn = big ? size * 0.9f : size;
                    node.Angle = ((float)hh - 0.5f) * 26;
                    node.Alpha = 1;
                    node.Delay = Fract(hh * 59.3) * Spread;
                    node.Phase = (float)(hh * 2 * Math.PI);

                    SetOrbit(ref node, hh, 3, 10);
                }
            }
        }

        // Symbols on a jittered 44 x 38 lattice and the gift's model on one four times coarser, so that both
        // repeat over the same tile and the field wraps without a seam.
        private const float SymbolDx = 44;
        private const float SymbolDy = 38;
        private const float ModelDx = SymbolDx * 4;
        private const float ModelDy = SymbolDy * 4;

        private int _symbolCols;
        private int _symbolRows;

        private void LayoutGift(Field field)
        {
            var symbol = field.Brushes[0];
            var model = field.Brushes[1];

            var margin = SqueezeBand * 1.3f;

            _symbolCols = (int)MathF.Ceiling((_size.X + 2 * margin) / ModelDx) * 4;
            _symbolRows = (int)MathF.Ceiling((_size.Y + 2 * margin) / (2 * ModelDy)) * 8;

            field.Width = _symbolCols * SymbolDx;
            field.Height = _symbolRows * SymbolDy;

            // Each symbol is sized by its tier and kept clear of the models and of its neighbours with a fixed
            // gap: the smaller of two that would touch shrinks to fit, or goes.
            for (int r = 0; r < _symbolRows; r++)
            {
                for (int col = 0; col < _symbolCols; col++)
                {
                    if (!SymbolCandidate(r, col, out float x, out float y, out double hh))
                    {
                        continue;
                    }

                    var fit = SymbolFit(r, col, x, y, hh);
                    if (fit <= 0)
                    {
                        continue;
                    }

                    var h2 = Fract(hh * 7.31);
                    var h5 = Fract(hh * 41.9);
                    var size = 21 * SymbolSizeK * (0.55f + 0.75f * h5 * h5) * fit;

                    ref var node = ref AddNode(field, symbol);
                    node.Kind = KindSymbol;
                    node.Source = 0;
                    node.Mass = 1 + MathF.Max(0, size / 15 - 1) * 0.8f;
                    node.X = x;
                    node.Y = y;
                    node.Size = size;
                    node.Drawn = size;
                    node.Angle = (h2 - 0.5f) * 70;

                    // 0.37 is the ink's own opacity. A share of the symbols is paler at rest, which gives the
                    // field some depth.
                    node.Alpha = 0.37f * (Fract(hh * 61.37) < 0.45f ? 0.67f : 1f);
                    node.Delay = Fract(hh * 53.1) * Spread;
                    node.TwinkleSpeed = 0.35f + 0.55f * h2;
                    node.TwinklePhase = (float)(hh * 2 * Math.PI);

                    SetOrbit(ref node, hh, 2, 7);
                }
            }

            var modelCols = _symbolCols / 4;
            var modelRows = _symbolRows / 4;

            for (int r = 0; r < modelRows; r++)
            {
                var shift = (r & 1) == 0 ? 0 : ModelDx / 2;

                for (int col = 0; col < modelCols; col++)
                {
                    var hh = ModelHash(r, col);

                    ref var node = ref AddNode(field, model);
                    node.Kind = KindModel;
                    node.Source = 1;
                    node.X = col * ModelDx + shift + (Fract(hh * 3.7) - 0.5f) * 76;
                    node.Y = r * ModelDy + (Fract(hh * 5.3) - 0.5f) * 64;
                    node.Size = 62 + 32 * Fract(hh * 11.9);
                    node.Drawn = node.Size;
                    node.Mass = 1 + MathF.Max(0, node.Size / 40 - 1) * 0.8f;
                    node.Angle = (Fract(hh * 17.3) - 0.5f) * 22;
                    node.Alpha = 1;
                    node.Delay = Fract(hh * 53.1) * Spread;
                    node.Phase = (float)(hh * 2 * Math.PI);

                    SetOrbit(ref node, hh, 5, 12);
                }
            }
        }

        private ref Node AddNode(Field field, CompositionSurfaceBrush brush)
        {
            if (field.Count == field.Nodes.Length)
            {
                Array.Resize(ref field.Nodes, Math.Max(64, field.Nodes.Length * 2));
            }

            ref var node = ref field.Nodes[field.Count++];
            node = default;

            node.Sprite = _compositor.CreateSpriteVisual();
            node.Sprite.Brush = brush;
            node.Sprite.IsVisible = false;
            node.Opacity = 1;

            field.Visual.Children.InsertAtTop(node.Sprite);
            return ref node;
        }

        // Every node also circles its own point slowly: radius in [min, max], a period of 9 to 20 s, either way.
        private static void SetOrbit(ref Node node, double hh, float min, float max)
        {
            var period = 9 + 11 * Fract(hh * 37.1);

            node.OrbitRadius = min + (max - min) * Fract(hh * 31.7);
            node.OrbitSpeed = (Fract(hh * 43.3) < 0.5f ? -1 : 1) * 2 * MathF.PI / period;
            node.OrbitPhase = (float)(hh * 6.283);
        }

        private const float SymbolSizeK = 1.1f;
        private const float SymbolMaxK = 1.25f;
        private const float SymbolGap = 3.5f;

        private double SymbolHash(int r, int c)
        {
            return LatticeHash(Wrap(r, _symbolRows), Wrap(c, _symbolCols));
        }

        private double ModelHash(int r, int c)
        {
            return LatticeHash(Wrap(r, _symbolRows / 4), Wrap(c, _symbolCols / 4));
        }

        private bool SymbolCandidate(int r, int col, out float x, out float y, out double hh)
        {
            hh = SymbolHash(r, col);
            x = col * SymbolDx + ((r & 1) == 0 ? 0 : SymbolDx / 2) + (Fract(hh * 3.1) - 0.5f) * 38;
            y = r * SymbolDy + (Fract(hh * 6.7) - 0.5f) * 32;

            if (Fract(hh * 23.17) <= 0.39f)
            {
                return false;
            }

            return !NearModel(x, y, SymbolMaxRadius(hh) + SymbolOrbitMax(hh));
        }

        private static float SymbolMaxRadius(double hh)
        {
            var h5 = Fract(hh * 41.9);
            return 21 * SymbolSizeK * (0.55f + 0.75f * h5 * h5) / 2 * SymbolMaxK;
        }

        private static float SymbolOrbitMax(double hh)
        {
            return 2 + 5 * Fract(hh * 31.7);
        }

        // 1 at full size, 0.6 to 1 when shrunk to keep the gap to a bigger neighbour, 0 when dropped.
        private float SymbolFit(int r, int col, float x, float y, double hh)
        {
            var radius = SymbolMaxRadius(hh);
            var orbit = SymbolOrbitMax(hh);
            var fit = 1f;

            for (int rr = r - 2; rr <= r + 2; rr++)
            {
                for (int cc = col - 2; cc <= col + 2; cc++)
                {
                    if (rr == r && cc == col)
                    {
                        continue;
                    }

                    if (!SymbolCandidate(rr, cc, out float x2, out float y2, out double h2))
                    {
                        continue;
                    }

                    var radius2 = SymbolMaxRadius(h2);
                    var limit = radius + radius2 + orbit + SymbolOrbitMax(h2) + SymbolGap;
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(x2, y2));

                    if (distance >= limit)
                    {
                        continue;
                    }

                    // The bigger symbol stays (ties go to the larger hash); the smaller one shrinks or goes.
                    if (radius2 > radius || (radius2 == radius && h2 > hh))
                    {
                        var room = distance - radius2 - orbit - SymbolOrbitMax(h2) - SymbolGap;
                        var f = room / radius;

                        if (f < 0.6f)
                        {
                            return 0;
                        }

                        fit = MathF.Min(fit, f);
                    }
                }
            }

            return fit;
        }

        private bool NearModel(float x, float y, float extent)
        {
            var rc = (int)MathF.Round(y / ModelDy);

            for (int r = rc - 1; r <= rc + 1; r++)
            {
                var shift = (r & 1) == 0 ? 0 : ModelDx / 2;
                var cc = (int)MathF.Round((x - shift) / ModelDx);

                for (int col = cc - 1; col <= cc + 1; col++)
                {
                    var hh = ModelHash(r, col);
                    var mx = col * ModelDx + shift + (Fract(hh * 3.7) - 0.5f) * 76;
                    var my = r * ModelDy + (Fract(hh * 5.3) - 0.5f) * 64;
                    var side = 62 + 32 * Fract(hh * 11.9);

                    // The model's artwork covers about a third of its box.
                    var limit = side * 0.35f + extent + 5 + 7 * Fract(hh * 31.7) + SymbolGap;

                    if (Vector2.DistanceSquared(new Vector2(x, y), new Vector2(mx, my)) < limit * limit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Motion

        private float Now()
        {
            return (float)((Stopwatch.GetTimestamp() - _clockStart) / (double)Stopwatch.Frequency);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachTracker(true);
            UpdateRendering();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            AttachTracker(false);
            SetRendering(false);
        }

        private void AttachTracker(bool attach)
        {
            if (_trackerAttached == attach)
            {
                return;
            }

            _trackerAttached = attach;
            _tracking = false;

            if (_tracker == null)
            {
                _interactionSource = VisualInteractionSource.Create(ElementComposition.GetElementVisual(this));
                _interactionSource.ManipulationRedirectionMode = VisualInteractionSourceRedirectionMode.CapableTouchpadOnly;
                _interactionSource.PositionXSourceMode = InteractionSourceMode.EnabledWithInertia;
                _interactionSource.PositionYSourceMode = InteractionSourceMode.EnabledWithInertia;
                _interactionSource.PositionXChainingMode = InteractionChainingMode.Never;
                _interactionSource.PositionYChainingMode = InteractionChainingMode.Never;

                // On by default, which locks a pan that starts mostly straight to its axis.
                _interactionSource.IsPositionXRailsEnabled = false;
                _interactionSource.IsPositionYRailsEnabled = false;

                _trackerOwner = new WeakInteractionTrackerOwner();

                _tracker = InteractionTracker.CreateWithOwner(_compositor, _trackerOwner);
                _tracker.InteractionSources.Add(_interactionSource);

                // Never reached, and near enough to zero for a float to keep sub-pixel steps.
                _tracker.MinPosition = new Vector3(-100000, -100000, 0);
                _tracker.MaxPosition = new Vector3(100000, 100000, 0);

                // Matches the mouse's glide: the velocity falls by e every GlideTau, and no fling goes further
                // than one at MaxFling would.
                _tracker.PositionInertiaDecayRate = new Vector3(1 - MathF.Exp(-1 / GlideTau));
                _tracker.ConfigurePositionXInertiaModifiers(new[] { FlingReach("X") });
                _tracker.ConfigurePositionYInertiaModifiers(new[] { FlingReach("Y") });
            }

            if (attach)
            {
                _trackerOwner.ValuesChanged += OnTrackerValuesChanged;
                _trackerOwner.InteractingStateEntered += OnTrackerInteractingStateEntered;
                _trackerOwner.InertiaStateEntered += OnTrackerInertiaStateEntered;
                _trackerOwner.IdleStateEntered += OnTrackerIdleStateEntered;
            }
            else
            {
                _trackerOwner.ValuesChanged -= OnTrackerValuesChanged;
                _trackerOwner.InteractingStateEntered -= OnTrackerInteractingStateEntered;
                _trackerOwner.InertiaStateEntered -= OnTrackerInertiaStateEntered;
                _trackerOwner.IdleStateEntered -= OnTrackerIdleStateEntered;
            }
        }

        private InteractionTrackerInertiaModifier FlingReach(string axis)
        {
            var reach = _compositor.CreateExpressionAnimation($"this.Target.Position.{axis} + Clamp(this.Target.NaturalRestingPosition.{axis} - this.Target.Position.{axis}, -reach, reach)");
            reach.SetScalarParameter("reach", MaxFling * GlideTau);

            var modifier = InteractionTrackerInertiaRestingValue.Create(_compositor);
            modifier.Condition = _compositor.CreateExpressionAnimation("true");
            modifier.RestingValue = reach;

            return modifier;
        }

        // The tracker scrolls: its position grows as the content moves left and up.
        private void OnTrackerValuesChanged(InteractionTracker sender, InteractionTrackerValuesChangedArgs args)
        {
            _panX -= args.Position.X - _trackerPosition.X;
            _panY -= args.Position.Y - _trackerPosition.Y;
            _trackerPosition = args.Position;
        }

        private void OnTrackerInteractingStateEntered(InteractionTracker sender, InteractionTrackerInteractingStateEnteredArgs args)
        {
            _tracking = true;
            _gliding = false;
        }

        private void OnTrackerInertiaStateEntered(InteractionTracker sender, InteractionTrackerInertiaStateEnteredArgs args)
        {
            _tracking = true;
        }

        private void OnTrackerIdleStateEntered(InteractionTracker sender, InteractionTrackerIdleStateEnteredArgs args)
        {
            _tracking = false;
        }

        private void UpdateRendering()
        {
            SetRendering(IsLoaded && (_field?.Count > 0 || _leaving?.Count > 0));
        }

        private void SetRendering(bool value)
        {
            if (_rendering == value)
            {
                return;
            }

            _rendering = value;
            _lastTick = -1;

            // A static event: it would keep this control alive for as long as the app runs.
            if (value)
            {
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        private void OnRendering(object sender, object e)
        {
            Tick();
        }

        private void Tick()
        {
            var now = Now();
            var dt = _lastTick < 0 ? 0 : MathF.Min(0.05f, now - _lastTick);
            _lastTick = now;

            var held = _panning || _gliding || _tracking;
            if (held)
            {
                _idleResumeAt = now + 0.25f;
            }

            var idleTarget = held || now < _idleResumeAt ? 0 : 1;
            var tau = idleTarget < _idle ? 0.07f : 0.45f;

            if (dt > 0)
            {
                _idle += (idleTarget - _idle) * (1 - MathF.Exp(-dt / tau));
            }

            var idleDt = dt * _idle;
            _motion += idleDt;

            if (idleDt > 0)
            {
                var heading = DriftAngle + 1.2f * MathF.Sin(0.025f * _motion) + 0.6f * MathF.Sin(0.016f * _motion + 1);

                _driftX += MathF.Cos(heading) * DriftSpeed * idleDt;
                _driftY += MathF.Sin(heading) * DriftSpeed * idleDt;
            }

            if (_gliding)
            {
                var t = now - _glideStart;
                var f = GlideTau * (1 - MathF.Exp(-t / GlideTau));

                _panX = _glideFromX + _glideVX * f;
                _panY = _glideFromY + _glideVY * f;

                if (t > GlideTau * 8)
                {
                    _gliding = false;
                }
            }

            _waveProgress = _waveBrush != null ? Math.Clamp((now - _waveStart) / WaveDuration, 0, 1) : 1;
            _waveRadius = MaxWaveRadius();
            _waveFront = _waveRadius * WaveEase(_waveProgress);

            UpdateSweep(now);

            if (_leaving != null && !Update(_leaving, now, dt))
            {
                Remove(_leaving);
                _leaving = null;

                UpdateRendering();
            }

            if (_field != null)
            {
                Update(_field, now, dt);
            }
        }

        private void UpdateSweep(float now)
        {
            if (float.IsNaN(_sweepStart))
            {
                // After the code's intro, with a beat to spare.
                _sweepStart = now + 0.92f + 1.5f;
                SetSweepAngle();
            }

            // Emoji alone take a softer light, and less often.
            _shineAmplitude = _field?.Emoji != null ? 0.7f : 1;

            var period = (ShineDuration + 0.2f) * (_field?.Emoji != null ? 1.5f : 1);

            while (now >= _sweepStart + period)
            {
                _sweepStart += period;
                SetSweepAngle();
            }

            var p = (now - _sweepStart) / ShineDuration;

            _shining = p >= 0 && p < 1;

            if (_shining)
            {
                var e = 0.5f - 0.5f * MathF.Cos(MathF.PI * p);
                var extent = (MathF.Abs(_sweepDX) * _size.X + MathF.Abs(_sweepDY) * _size.Y) / 2 + ShineWidth * 3;

                _shineCenter = -extent + 2 * extent * e;
            }
        }

        private void SetSweepAngle()
        {
            var angle = MathFEx.ToRadians(ShineAngle + ((float)_random.NextDouble() - 0.5f) * 12);

            _sweepDX = MathF.Cos(angle);
            _sweepDY = MathF.Sin(angle);
        }

        // The band at a point: the soft light, the narrow metallic core inside it, and the signed distance to
        // the band's centre in widths, which the push reads. The band bows a little, and moves as if over a
        // cylinder lying along it: fast through the middle, slower, narrower and dimmer towards the edges.
        private void Shine(float x, float y, out float light, out float core, out float distance, out float envelope)
        {
            light = core = distance = envelope = 0;

            if (!_shining)
            {
                return;
            }

            const float curve = 0.15f;
            const float cylinder = 0.7f;
            const float edgeDim = 0.3f;
            const float halfPi = MathF.PI / 2;

            var w = _size.X;
            var h = _size.Y;

            var projection = (x - w / 2) * _sweepDX + (y - h / 2) * _sweepDY;
            var extent = MathF.Max(1, (MathF.Abs(_sweepDX) * w + MathF.Abs(_sweepDY) * h) / 2);

            var across = (x - w / 2) * -_sweepDY + (y - h / 2) * _sweepDX;
            var u = across / MathF.Max(1, (MathF.Abs(_sweepDY) * w + MathF.Abs(_sweepDX) * h) / 2);
            projection -= curve * w * u * u;

            var flat = (projection - _shineCenter) / ShineWidth;
            var tp = MathF.Asin(Math.Clamp(projection / extent, -1, 1));
            var tc = Math.Clamp(_shineCenter / extent * halfPi, -halfPi * 1.4f, halfPi * 1.4f);
            var curved = (tp - tc) / (ShineWidth / extent * halfPi);

            distance = flat + (curved - flat) * cylinder;
            envelope = 1 - cylinder * edgeDim * (1 - MathF.Cos(Math.Clamp(tp, -halfPi, halfPi)));

            if (distance > 4 || distance < -4)
            {
                return;
            }

            var dc = MathF.Abs(distance / 0.3f);
            if (dc < 4)
            {
                core = envelope * _shineAmplitude * MathF.Exp(-0.5f * dc * MathF.Sqrt(dc));
            }

            var dl = MathF.Abs(distance);
            light = envelope * _shineAmplitude * 0.55f * MathF.Exp(-0.5f * dl * MathF.Sqrt(dl));
        }

        // Places every node of a field for this frame. False once a leaving field has nothing left on screen.
        private bool Update(Field field, float now, float dt)
        {
            var margin = SqueezeBand * 1.3f;
            var elapsed = now - field.Start;
            var alive = false;

            // The latest any slot can start; past it plus the pop, every slot of a field that stays is at 1. A
            // field shown without animation starts at minus infinity, where the per-slot sums come out NaN.
            var latest = Spread;
            var popped = field.Front || (!field.Leaving && (float.IsNegativeInfinity(field.Start) || elapsed - latest >= (field.Alone ? Grow : GrowAt + Grow)));

            // The nodes' own motion runs on the idle clock, which stands still while the field is held.
            var motion = _motion;

            for (int i = 0; i < field.Count; i++)
            {
                ref var node = ref field.Nodes[i];
                var orbit = node.OrbitSpeed * motion + node.OrbitPhase;
                var x = node.X + _driftX + _panX + node.OrbitRadius * MathF.Cos(orbit);
                var y = node.Y + _driftY + _panY + node.OrbitRadius * MathF.Sin(orbit);

                if (node.Kind == KindEmoji)
                {
                    y += MathF.Sin(0.45f * motion + node.Phase) * 3;
                }
                else if (node.Kind == KindModel)
                {
                    y += MathF.Sin(0.35f * motion + node.Phase) * 3;
                }

                // Wrap around the tile, whose edges are well outside the screen.
                x -= field.Width * MathF.Floor((x + margin) / field.Width);
                y -= field.Height * MathF.Floor((y + margin) / field.Height);

                Kick(ref node, x, y, dt);

                x += node.SpringX;
                y += node.SpringY;

                var pop = 1f;

                if (!popped)
                {
                    pop = PopScale(elapsed - node.Delay, field.Leaving, field.Alone);
                }

                if (pop > 0 || !field.Leaving)
                {
                    alive = true;
                }

                if (pop <= 0.01f || !Place(ref x, ref y, node.Size, node.Kind == KindSymbol, out float scale, out float alpha))
                {
                    if (node.Visible)
                    {
                        node.Visible = false;
                        node.Sprite.IsVisible = false;

                        HideOverlay(node.Lit, ref node.LitOpacity);
                        HideOverlay(node.Core, ref node.CoreOpacity);
                    }

                    continue;
                }

                var angle = node.Angle + node.SpringR;
                var opacity = alpha * node.Alpha;

                scale *= pop * (1 + node.SpringS);

                Shine(x, y, out float light, out float core, out float distance, out float envelope);

                // The band pushes what it passes along with it, ahead nudged forward and behind settled back,
                // never far enough for neighbours to touch.
                if (node.Kind != KindEmoji)
                {
                    var push = 0f;
                    var d = distance / 1.1f;

                    if (envelope > 0.001f && d < 4 && d > -4)
                    {
                        push = (node.Kind == KindModel ? 0.5f : 1) * SymbolGap / 2 / 0.607f * d * MathF.Exp(-0.5f * d * d) * envelope;
                    }

                    Follow(ref node, push, light, dt);

                    x += _sweepDX * node.PushO2;
                    y += _sweepDY * node.PushO2;

                    if (node.Kind == KindSymbol)
                    {
                        angle += 1.5f * node.PushL2;
                    }
                }

                // What the light does: emoji and models are washed towards white, symbols get a copy in the
                // light's own colour plus a white flash at the band's core.
                float lit;
                float flash = 0;

                if (node.Kind == KindEmoji)
                {
                    scale *= (1 + 0.03f * MathF.Sin(1.3f * motion + node.Phase * 2)) * (1 + 0.08f * light);
                    angle += 4 * MathF.Sin(0.3f * motion + node.Phase);
                    lit = opacity * 0.55f * light;
                }
                else if (node.Kind == KindSymbol)
                {
                    var twinkle = 0.5f + 0.5f * MathF.Sin(now * node.TwinkleSpeed + node.TwinklePhase);
                    twinkle *= twinkle;

                    scale *= 1 + 0.06f * light + 0.1f * twinkle;
                    opacity *= 0.85f + 0.15f * twinkle;

                    lit = light > 0.01f ? alpha * MathF.Min(1, light * 0.46f) : 0;
                    flash = core > 0.01f ? alpha * MathF.Min(1, core) : 0;
                }
                else
                {
                    scale *= (1 + 0.03f * MathF.Sin(1.3f * motion + node.Phase * 2)) * (1 + 0.05f * light);
                    lit = opacity * 0.45f * light;
                }

                if (!node.Visible)
                {
                    node.Visible = true;
                    node.Sprite.IsVisible = true;
                    node.Sprite.Size = new Vector2(node.Drawn);
                }

                // Scale and turn about the item's centre, then place it: one matrix rather than four properties.
                var radians = angle * MathF.PI / 180;
                var cos = MathF.Cos(radians) * scale;
                var sin = MathF.Sin(radians) * scale;
                var half = node.Drawn / 2;

                var transform = new Matrix4x4(
                    cos, sin, 0, 0,
                    -sin, cos, 0, 0,
                    0, 0, 1, 0,
                    x - (cos - sin) * half, y - (sin + cos) * half, 0, 1);

                node.Sprite.TransformMatrix = transform;

                if (Changed(node.Opacity, opacity))
                {
                    node.Opacity = opacity;
                    node.Sprite.Opacity = opacity;
                }

                // The silhouettes are only looked up once there is light to draw.
                if (lit > OverlayMin)
                {
                    UpdateOverlay(field, ref node.Lit, ref node.LitOpacity, node.Sprite, LitBrush(field, node.Source), node.Drawn, transform, lit);
                }
                else
                {
                    HideOverlay(node.Lit, ref node.LitOpacity);
                }

                if (flash > OverlayMin && node.Kind == KindSymbol)
                {
                    UpdateOverlay(field, ref node.Core, ref node.CoreOpacity, node.Lit ?? node.Sprite, CoreBrush(field), node.Drawn, transform, flash);
                }
                else
                {
                    HideOverlay(node.Core, ref node.CoreOpacity);
                }
            }

            return alive;
        }

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (_field == null || _field.Count == 0)
            {
                return;
            }

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch && _interactionSource != null)
            {
                try
                {
                    _interactionSource.TryRedirectForManipulation(e.GetCurrentPoint(this));
                }
                catch
                {
                    // Not a contact that can be redirected any more.
                }

                return;
            }

            if (!CapturePointer(e.Pointer))
            {
                return;
            }

            // A glide in progress is caught where it is.
            _gliding = false;
            _panning = true;
            _panMoved = false;
            _panBaseX = _panX;
            _panBaseY = _panY;
            _downPoint = e.GetCurrentPoint(this).Position;
            _trackCount = 0;

            Track(_downPoint);
            e.Handled = true;
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_panning)
            {
                return;
            }

            var point = e.GetCurrentPoint(this).Position;
            var dx = (float)(point.X - _downPoint.X);
            var dy = (float)(point.Y - _downPoint.Y);

            Track(point);

            if (!_panMoved && dx * dx + dy * dy > 8 * 8)
            {
                _panMoved = true;
            }

            if (_panMoved)
            {
                _panX = _panBaseX + dx;
                _panY = _panBaseY + dy;
            }

            e.Handled = true;
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (!_panning)
            {
                return;
            }

            Track(e.GetCurrentPoint(this).Position);
            ReleasePointerCapture(e.Pointer);

            _panning = false;

            if (_panMoved && Velocity(out float vx, out float vy))
            {
                var v = MathF.Sqrt(vx * vx + vy * vy);
                if (v > MaxFling)
                {
                    vx *= MaxFling / v;
                    vy *= MaxFling / v;
                }

                _glideFromX = _panX;
                _glideFromY = _panY;
                _glideVX = vx;
                _glideVY = vy;
                _glideStart = Now();
                _gliding = true;
            }

            e.Handled = true;
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            _panning = false;
        }

        private void Track(Point point)
        {
            if (_trackCount == _trackT.Length)
            {
                Array.Copy(_trackT, 1, _trackT, 0, _trackCount - 1);
                Array.Copy(_trackX, 1, _trackX, 0, _trackCount - 1);
                Array.Copy(_trackY, 1, _trackY, 0, _trackCount - 1);
                _trackCount--;
            }

            _trackT[_trackCount] = Now();
            _trackX[_trackCount] = (float)point.X;
            _trackY[_trackCount] = (float)point.Y;
            _trackCount++;
        }

        // Over the last 100 ms of movement, so a pause before letting go means no fling.
        private bool Velocity(out float vx, out float vy)
        {
            vx = vy = 0;

            if (_trackCount < 2)
            {
                return false;
            }

            var last = _trackCount - 1;
            var first = last;

            while (first > 0 && _trackT[last] - _trackT[first - 1] <= 0.1f)
            {
                first--;
            }

            var span = _trackT[last] - _trackT[first];
            if (span <= 0.005f || Now() - _trackT[last] > 0.1f)
            {
                return false;
            }

            vx = (_trackX[last] - _trackX[first]) / span;
            vy = (_trackY[last] - _trackY[first]) / span;
            return true;
        }

        // The colour wave kicks every item once as its front passes: outwards from where it started, with a
        // little tilt and a pulse, lighter items further; a spring brings it back.
        private void Kick(ref Node node, float x, float y, float dt)
        {
            if (_waveProgress < 1 && node.Hit != _waveId)
            {
                var dx = x - _waveOrigin.X;
                var dy = y - _waveOrigin.Y;
                var d = MathF.Sqrt(dx * dx + dy * dy);

                if (d <= _waveFront)
                {
                    var inverse = 1 / node.Mass;

                    dx = d > 1 ? dx / d : 0;
                    dy = d > 1 ? dy / d : -1;

                    node.Hit = _waveId;
                    node.Springing = true;
                    node.SpringVX += dx * 360 * inverse;
                    node.SpringVY += dy * 360 * inverse;
                    node.SpringVR += (dx >= 0 ? 1 : -1) * 180 * inverse;
                    node.SpringVS += 1.8f;
                }
            }

            if (!node.Springing || dt <= 0)
            {
                return;
            }

            const float k = 220;
            var c = 2 * 0.62f * MathF.Sqrt(k);

            // Two substeps keep the spring stable at any frame rate.
            for (int i = 0; i < 2; i++)
            {
                var h = dt / 2;

                node.SpringVX += (-k * node.SpringX - c * node.SpringVX) * h;
                node.SpringX += node.SpringVX * h;
                node.SpringVY += (-k * node.SpringY - c * node.SpringVY) * h;
                node.SpringY += node.SpringVY * h;
                node.SpringVR += (-k * node.SpringR - c * node.SpringVR) * h;
                node.SpringR += node.SpringVR * h;
                node.SpringVS += (-k * node.SpringS - c * node.SpringVS) * h;
                node.SpringS += node.SpringVS * h;
            }

            var motion = MathF.Abs(node.SpringX) + MathF.Abs(node.SpringY) + MathF.Abs(node.SpringVX) + MathF.Abs(node.SpringVY);
            if (motion < 0.05f && MathF.Abs(node.SpringR) + MathF.Abs(node.SpringVR) < 0.02f && MathF.Abs(node.SpringS) + MathF.Abs(node.SpringVS) < 0.0005f)
            {
                node.Springing = false;
                node.SpringX = node.SpringY = node.SpringVX = node.SpringVY = 0;
                node.SpringR = node.SpringVR = node.SpringS = node.SpringVS = 0;
            }
        }

        // Picks up in about 360 ms and lets go in about 780.
        private static void Follow(ref Node node, float offset, float light, float dt)
        {
            var rising = MathF.Abs(offset) > MathF.Abs(node.PushO2) || light > node.PushL2;
            var tau = MathF.Max(0.01f, (rising ? 0.36f : 0.78f) / 3.4f / 2);
            var k = 1 - MathF.Exp(-dt / tau);

            node.PushO1 += (offset - node.PushO1) * k;
            node.PushO2 += (node.PushO1 - node.PushO2) * k;
            node.PushL1 += (light - node.PushL1) * k;
            node.PushL2 += (node.PushL1 - node.PushL2) * k;
        }

        private CompositionBrush LitBrush(Field field, int source)
        {
            while (field.LitBrushes.Count <= source)
            {
                field.LitBrushes.Add(null);
            }

            return field.LitBrushes[source] ??= Silhouette(field.Brushes[source], field.Gift && source == 0 ? field.LitColor : Colors.White);
        }

        private CompositionBrush CoreBrush(Field field)
        {
            return field.CoreBrush ??= Silhouette(field.Brushes[0], Colors.White);
        }

        private CompositionBrush Silhouette(CompositionBrush shape, Color color)
        {
            var brush = _compositor.CreateMaskBrush();
            brush.Source = _compositor.CreateColorBrush(color);
            brush.Mask = shape;

            return brush;
        }

        // An overlay drawn right over its item with the same transform, created the first time it is needed.
        private void UpdateOverlay(Field field, ref SpriteVisual overlay, ref float current, Visual below, CompositionBrush brush, float size, Matrix4x4 transform, float opacity)
        {
            if (overlay == null)
            {
                overlay = _compositor.CreateSpriteVisual();
                overlay.Brush = brush;
                overlay.Size = new Vector2(size);

                field.Visual.Children.InsertAbove(overlay, below);
            }
            else if (current <= 0)
            {
                overlay.IsVisible = true;
            }

            overlay.TransformMatrix = transform;

            if (Changed(current, opacity))
            {
                current = opacity;
                overlay.Opacity = opacity;
            }
        }

        // An overlay's opacity is 0 here exactly while it is hidden.
        private static void HideOverlay(SpriteVisual overlay, ref float current)
        {
            if (current > 0)
            {
                current = 0;
                overlay.IsVisible = false;
            }
        }

        private const float OverlayMin = 0.004f;

        // Half a step of 8-bit alpha: anything smaller cannot show.
        private static bool Changed(float current, float value)
        {
            return MathF.Abs(current - value) >= 0.5f / 255;
        }

        // Old content shrinks away, new content pops in with a small overshoot; p is the time since the slot
        // started. New slots wait for the old ones to shrink, unless there were none.
        private static float PopScale(float p, bool leaving, bool alone)
        {
            if (leaving)
            {
                return p <= 0 ? 1 : MathF.Max(0, 1 - p / Shrink);
            }

            var q = alone ? p : p - GrowAt;
            if (q <= 0)
            {
                return 0;
            }
            else if (q >= Grow)
            {
                return 1;
            }

            const float c1 = 1.70158f;
            const float c3 = c1 + 1;

            var u = q / Grow - 1;
            return 1 + c3 * u * u * u + c1 * u * u;
        }

        // Applies the radial shaping around the card and the edge squeeze to a lattice point: the point moves,
        // and the item's scale and opacity come back. False when there is nothing left to draw.
        private bool Place(ref float x, ref float y, float size, bool symbol, out float scale, out float alpha)
        {
            var w = _size.X;
            var h = _size.Y;
            var m = SqueezeBand * 1.3f;

            scale = 0;
            alpha = 0;

            if (x < -m || x > w + m || y < -m || y > h + m)
            {
                return false;
            }

            // r is 1 where the ray from the card centre meets the edge of the screen.
            var dx = x - _origin.X;
            var dy = y - _origin.Y;
            var r = 0f;

            if (dx > 0.5f)
            {
                r = MathF.Max(r, dx / MathF.Max(1, w - _origin.X));
            }
            else if (dx < -0.5f)
            {
                r = MathF.Max(r, -dx / MathF.Max(1, _origin.X));
            }

            if (dy > 0.5f)
            {
                r = MathF.Max(r, dy / MathF.Max(1, h - _origin.Y));
            }
            else if (dy < -0.5f)
            {
                r = MathF.Max(r, -dy / MathF.Max(1, _origin.Y));
            }

            r = MathF.Min(1, r);

            var radialScale = 1 - 0.22f * Smooth(0.55f, 1, r);
            var radialAlpha = 1 - 0.2f * Smooth(0.8f, 1, r);

            var k = 1f;

            if (x < SqueezeBand && x <= w - x)
            {
                var g = MathF.Exp(x / SqueezeBand - 1);
                x = SqueezeBand * g;
                k = g;
            }
            else if (w - x < SqueezeBand)
            {
                var g = MathF.Exp((w - x) / SqueezeBand - 1);
                x = w - SqueezeBand * g;
                k = g;
            }

            if (y < SqueezeBand && y <= h - y)
            {
                var g = MathF.Exp(y / SqueezeBand - 1);
                y = SqueezeBand * g;
                k = MathF.Min(k, g);
            }
            else if (h - y < SqueezeBand)
            {
                var g = MathF.Exp((h - y) / SqueezeBand - 1);
                y = h - SqueezeBand * g;
                k = MathF.Min(k, g);
            }

            alpha = radialAlpha * Smooth(0.08f, 0.25f, k);

            if (alpha <= 0.004f)
            {
                return false;
            }

            var sized = size * radialScale;
            var factor = sized < 10 ? 10 / MathF.Max(1, size) : radialScale;

            // Never a speck: emoji and models keep at least 9 px however hard the edge squeezes them. Symbols
            // get no floor, as their squeezed positions would come closer than their sizes allow.
            scale = symbol
                ? k * factor
                : MathF.Max(k * factor, MathF.Min(size, 9) / MathF.Max(1, size));

            var half = size * scale / 2;
            var edge = MathF.Min(MathF.Min(x - half, w - x - half), MathF.Min(y - half, h - y - half));
            var ue = Math.Clamp(edge / 12, 0, 1);

            alpha *= ue * ue * (3 - 2 * ue);
            return alpha > 0.004f;
        }

        private static double LatticeHash(int r, int c)
        {
            unchecked
            {
                var h = (ulong)((long)r * (long)0x9E3779B97F4A7C15 ^ (long)c * (long)0xC2B2AE3D27D4EB4F);
                h ^= h >> 31;
                h *= 0xBF58476D1CE4E5B9;
                h ^= h >> 27;
                h *= 0x94D049BB133111EB;
                h ^= h >> 31;

                return (h >> 11) / (double)(1L << 53);
            }
        }

        private static float Fract(double x)
        {
            return (float)(x - Math.Floor(x));
        }

        private static int Wrap(int value, int count)
        {
            return ((value % count) + count) % count;
        }

        private static float Smooth(float a, float b, float x)
        {
            var t = Math.Clamp((x - a) / (b - a), 0, 1);
            return t * t * (3 - 2 * t);
        }

        #endregion
    }

    public static class QrShareColors
    {
        // The gift backdrop "Black".
        public static readonly Color DefaultTop = Color.FromArgb(0xFF, 0x36, 0x37, 0x38);
        public static readonly Color DefaultBottom = Deepen(Color.FromArgb(0xFF, 0x0E, 0x0F, 0x0F));
        public static readonly Color DefaultQr = ContrastMatchedQr(DefaultTop, DefaultBottom);

        // The contrast on the white card of the blue the QR colour was first tuned with, which every
        // backdrop's QR colour is matched to.
        private const double QrContrast = 4.4;

        // The same hue, lit: 20 lighter in L*, a little more saturated.
        public static Color Brighter(Color ink)
        {
            ToLab(ink, out double l, out double a, out double b);

            var lighter = FromLab(Math.Min(97, l + 20), a, b);

            var max = Math.Max(lighter.R, Math.Max(lighter.G, lighter.B)) / 255.0;
            var min = Math.Min(lighter.R, Math.Min(lighter.G, lighter.B)) / 255.0;

            if (max <= 0 || max == min)
            {
                return lighter;
            }

            var saturation = (max - min) / max;
            var target = Math.Min(1, saturation * 1.15);
            var newMin = max * (1 - target);

            byte Stretch(byte component)
            {
                var value = component / 255.0;
                return ToByte(newMin + (value - min) / (max - min) * (max - newMin));
            }

            return Color.FromArgb(0xFF, Stretch(lighter.R), Stretch(lighter.G), Stretch(lighter.B));
        }

        // A plain blend of the components, as the tuned values were picked with.
        public static Color Mix(Color x, Color y, float t)
        {
            return Color.FromArgb(
                (byte)(x.A + (y.A - x.A) * t),
                (byte)(x.R + (y.R - x.R) * t),
                (byte)(x.G + (y.G - x.G) * t),
                (byte)(x.B + (y.B - x.B) * t));
        }

        // A gift's edge colour, a hair deeper so a full-screen radial has some depth.
        public static Color Deepen(Color color)
        {
            ToLab(color, out double l, out double a, out double b);
            return FromLab(Math.Max(0, l - 3), a * 1.03, b * 1.03);
        }

        // The pattern colour as is; only when it is too close to the backdrop's centre is it nudged darker,
        // by as little as it takes.
        public static Color SymbolInk(Color pattern, Color center)
        {
            var color = Color.FromArgb(0xFF, pattern.R, pattern.G, pattern.B);

            if (Contrast(color, center) < 1.15)
            {
                ToLab(color, out double l, out double a, out double b);

                for (int i = 0; i < 40 && Contrast(color, center) < 1.15; i++)
                {
                    l = Math.Max(0, l - 2);
                    color = FromLab(l, a, b);
                }
            }

            return color;
        }

        // The backdrop's hue at the contrast the default has on the white card. On dark backdrops the code
        // never gets lighter than the edge.
        public static Color ContrastMatchedQr(Color center, Color edge)
        {
            ToOklab(edge, out double eL, out double ea, out double eb);
            ToOklab(center, out _, out double ma, out double mb);

            var lCap = eL < 0.45 ? eL - 0.03 : 2.0;
            var edgeC = Math.Sqrt(ea * ea + eb * eb);

            double h;
            double c;

            if (edgeC < 0.03)
            {
                var a = ma * 0.4 + ea * 0.6;
                var b = mb * 0.4 + eb * 0.6;

                h = Math.Atan2(b, a);
                c = Math.Min(0.012, Math.Sqrt(a * a + b * b));
            }
            else
            {
                h = Math.Atan2(eb, ea);
                c = Math.Min(edgeC * 1.25 + 0.02, 0.20);
            }

            double lo = 0.2;
            double hi = 0.9;
            double red, green, blue;

            for (int i = 0; i < 12; i++)
            {
                var mid = (lo + hi) / 2;
                FitChroma(mid, c, h, out red, out green, out blue);

                if (ContrastOnWhite(red, green, blue) > QrContrast)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            var lightness = (lo + hi) / 2;
            if (lightness > lCap)
            {
                lightness = Math.Max(0.05, lCap);
            }

            FitChroma(lightness, c, h, out red, out green, out blue);
            return Color.FromArgb(0xFF, ToByte(Gamma(Clamp01(red))), ToByte(Gamma(Clamp01(green))), ToByte(Gamma(Clamp01(blue))));
        }

        public static Color MixOklab(Color x, Color y, float t)
        {
            if (t <= 0)
            {
                return x;
            }
            else if (t >= 1)
            {
                return y;
            }

            ToLms(x, out double l1, out double m1, out double s1);
            ToLms(y, out double l2, out double m2, out double s2);

            var l = l1 + (l2 - l1) * t;
            var m = m1 + (m2 - m1) * t;
            var s = s1 + (s2 - s1) * t;

            LmsToLinear(l, m, s, out double red, out double green, out double blue);

            var alpha = (byte)(x.A + (y.A - x.A) * t);
            return Color.FromArgb(alpha, ToByte(Gamma(Clamp01(red))), ToByte(Gamma(Clamp01(green))), ToByte(Gamma(Clamp01(blue))));
        }

        private static void FitChroma(double l, double c, double h, out double red, out double green, out double blue)
        {
            var cos = Math.Cos(h);
            var sin = Math.Sin(h);

            OklabToLinear(l, c * cos, c * sin, out red, out green, out blue);

            if (InGamut(red, green, blue))
            {
                return;
            }

            double lo = 0;
            double hi = c;

            for (int i = 0; i < 14; i++)
            {
                var mid = (lo + hi) / 2;
                OklabToLinear(l, mid * cos, mid * sin, out red, out green, out blue);

                if (InGamut(red, green, blue))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            OklabToLinear(l, lo * cos, lo * sin, out red, out green, out blue);
        }

        private static bool InGamut(double red, double green, double blue)
        {
            const double e = 1e-4;
            return red >= -e && red <= 1 + e && green >= -e && green <= 1 + e && blue >= -e && blue <= 1 + e;
        }

        private static double ContrastOnWhite(double red, double green, double blue)
        {
            var y = 0.2126 * Clamp01(red) + 0.7152 * Clamp01(green) + 0.0722 * Clamp01(blue);
            return 1.05 / (y + 0.05);
        }

        private static double Contrast(Color x, Color y)
        {
            var lx = Luminance(x);
            var ly = Luminance(y);

            return (Math.Max(lx, ly) + 0.05) / (Math.Min(lx, ly) + 0.05);
        }

        private static double Luminance(Color color)
        {
            return 0.2126 * Linear(color.R / 255.0) + 0.7152 * Linear(color.G / 255.0) + 0.0722 * Linear(color.B / 255.0);
        }

        private static void ToLms(Color color, out double l, out double m, out double s)
        {
            var red = Linear(color.R / 255.0);
            var green = Linear(color.G / 255.0);
            var blue = Linear(color.B / 255.0);

            l = Math.Pow(0.4122214708 * red + 0.5363325363 * green + 0.0514459929 * blue, 1 / 3.0);
            m = Math.Pow(0.2119034982 * red + 0.6806995451 * green + 0.1073969566 * blue, 1 / 3.0);
            s = Math.Pow(0.0883024619 * red + 0.2817188376 * green + 0.6299787005 * blue, 1 / 3.0);
        }

        private static void ToOklab(Color color, out double l, out double a, out double b)
        {
            ToLms(color, out double l1, out double m1, out double s1);

            l = 0.2104542553 * l1 + 0.7936177850 * m1 - 0.0040720468 * s1;
            a = 1.9779984951 * l1 - 2.4285922050 * m1 + 0.4505937099 * s1;
            b = 0.0259040371 * l1 + 0.7827717662 * m1 - 0.8086757660 * s1;
        }

        private static void OklabToLinear(double l, double a, double b, out double red, out double green, out double blue)
        {
            LmsToLinear(
                l + 0.3963377774 * a + 0.2158037573 * b,
                l - 0.1055613458 * a - 0.0638541728 * b,
                l - 0.0894841775 * a - 1.2914855480 * b,
                out red, out green, out blue);
        }

        private static void LmsToLinear(double l, double m, double s, out double red, out double green, out double blue)
        {
            l = l * l * l;
            m = m * m * m;
            s = s * s * s;

            red = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
            green = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
            blue = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        }

        // CIE L*a*b* under D65, as the colour tables these values were tuned on use it.
        private static void ToLab(Color color, out double l, out double a, out double b)
        {
            var red = Linear(color.R / 255.0) * 100;
            var green = Linear(color.G / 255.0) * 100;
            var blue = Linear(color.B / 255.0) * 100;

            var x = (red * 0.4124 + green * 0.3576 + blue * 0.1805) / 95.047;
            var y = (red * 0.2126 + green * 0.7152 + blue * 0.0722) / 100.0;
            var z = (red * 0.0193 + green * 0.1192 + blue * 0.9505) / 108.883;

            x = PivotXyz(x);
            y = PivotXyz(y);
            z = PivotXyz(z);

            l = Math.Max(0, 116 * y - 16);
            a = 500 * (x - y);
            b = 200 * (y - z);
        }

        private static Color FromLab(double l, double a, double b)
        {
            var fy = (l + 16) / 116;
            var fx = a / 500 + fy;
            var fz = fy - b / 200;

            var x = 95.047 * Unpivot(fx);
            var y = 100.0 * (l > 8 ? fy * fy * fy : l / 903.3);
            var z = 108.883 * Unpivot(fz);

            var red = (x * 3.2406 + y * -1.5372 + z * -0.4986) / 100;
            var green = (x * -0.9689 + y * 1.8758 + z * 0.0415) / 100;
            var blue = (x * 0.0557 + y * -0.2040 + z * 1.0570) / 100;

            return Color.FromArgb(0xFF, ToByte(Gamma(Clamp01(red))), ToByte(Gamma(Clamp01(green))), ToByte(Gamma(Clamp01(blue))));
        }

        private static double PivotXyz(double component)
        {
            return component > 0.008856 ? Math.Pow(component, 1 / 3.0) : (903.3 * component + 16) / 116;
        }

        private static double Unpivot(double component)
        {
            var cube = component * component * component;
            return cube > 0.008856 ? cube : (116 * component - 16) / 903.3;
        }

        private static double Linear(double component)
        {
            return component <= 0.04045 ? component / 12.92 : Math.Pow((component + 0.055) / 1.055, 2.4);
        }

        private static double Gamma(double component)
        {
            return component <= 0.0031308 ? 12.92 * component : 1.055 * Math.Pow(component, 1 / 2.4) - 0.055;
        }

        private static double Clamp01(double value)
        {
            return value < 0 ? 0 : value > 1 ? 1 : value;
        }

        private static byte ToByte(double component)
        {
            return (byte)Math.Round(component * 255);
        }
    }
}
