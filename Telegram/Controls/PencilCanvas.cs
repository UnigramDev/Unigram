//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics.Effects;
using Windows.Graphics.Imaging;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public enum PencilTool
    {
        Pen,
        Arrow,
        Marker,
        Neon,
        Blur,
        Eraser
    }

    /// <summary>
    /// What each tool draws with. Weights are the slider's own scale; <see cref="GetThickness"/>
    /// turns one into a stroke width relative to the painting's width.
    /// </summary>
    public static class PencilTools
    {
        public static double GetMinimumWeight(PencilTool tool) => HasColor(tool) ? 0.05 : 0.4;

        public static double GetMaximumWeight(PencilTool tool) => HasColor(tool) ? 1 : 1.75;

        public static double GetDefaultWeight(PencilTool tool) => tool switch
        {
            PencilTool.Pen or PencilTool.Arrow => 0.25,
            PencilTool.Marker or PencilTool.Neon => 0.5,
            _ => 1
        };

        public static Color GetDefaultColor(PencilTool tool) => tool switch
        {
            PencilTool.Arrow => Color.FromArgb(0xFF, 0xFF, 0x8A, 0x00),
            PencilTool.Marker => Color.FromArgb(0xFF, 0xFF, 0xD6, 0x0A),
            PencilTool.Neon => Color.FromArgb(0xFF, 0x34, 0xC7, 0x59),
            _ => Color.FromArgb(0xFF, 0xFF, 0x45, 0x3A)
        };

        public static bool HasColor(PencilTool tool) => tool is not PencilTool.Blur and not PencilTool.Eraser;

        public static float GetThickness(PencilTool tool, double weight)
        {
            // An arrow's line is drawn thinner than a pen's at the same weight, so that its head,
            // which is sized from the weight, stands out.
            var scale = tool switch
            {
                PencilTool.Arrow => 0.5,
                PencilTool.Marker => 1.4,
                PencilTool.Neon => 1.45,
                _ => 1
            };

            return (float)((8 + 90 * weight) / 2048 * scale);
        }
    }

    [TemplatePart(Name = "Canvas", Type = typeof(CanvasControl))]
    public partial class PencilCanvas : Control
    {
        private const double ArrowDuration = 240;
        private const double FillDuration = 450;

        // A press held this long without moving further than FillSlop fills the canvas instead.
        private static readonly TimeSpan FillDelay = TimeSpan.FromMilliseconds(500);
        private const double FillSlop = 6;

        private CanvasRenderTarget _renderTarget;

        private ConcurrentDictionary<uint, SmoothPathBuilder> _builders;

        private List<SmoothPathBuilder> _drawing;

        private List<SmoothPathBuilder> _strokes = new();
        private readonly List<SmoothPathBuilder> _history = new();

        private PencilTool _tool;

        // A stroke still growing in, an arrow's head or a fill's disc: drawn over the ink surface,
        // not into it, until done.
        private SmoothPathBuilder _growing;
        private TimeSpan? _growingStart;
        private float _growingProgress;

        private bool _rendering;

        private DispatcherTimer _fillTimer;
        private uint? _fillPointer;
        private Point _fillOrigin;

        // Two touches move the view instead of drawing, until both have lifted.
        private readonly Dictionary<uint, Point> _touches = new();
        private bool _gesturing;

        // The eyedropper samples a snapshot of the photo with the drawing over it, read back once
        // when picking starts so following the pointer costs nothing.
        private bool _picking;
        private CanvasRenderTarget _pickSnapshot;
        private Color[] _pickPixels;
        private Point? _pickPoint;

        private SoftwareBitmap _blurSource;
        private CanvasBitmap _blurBitmap;
        private GaussianBlurEffect _blurEffect;
        private CanvasImageBrush _blurBrush;

        private bool _needToCreateSizeDependentResources;
        private bool _needToRedrawInkSurface;

        private CanvasControl Canvas;

        public PencilCanvas()
        {
            DefaultStyleKey = typeof(PencilCanvas);
        }

        protected override void OnApplyTemplate()
        {
            _builders = new ConcurrentDictionary<uint, SmoothPathBuilder>();

            Canvas = (CanvasControl)GetTemplateChild(nameof(Canvas));

            Canvas.SizeChanged += OnSizeChanged;

            Canvas.CreateResources += OnCreateResources;
            Canvas.Draw += OnDraw;

            Canvas.PointerPressed += OnPointerPressed;
            Canvas.PointerMoved += OnPointerMoved;
            Canvas.PointerReleased += OnPointerReleased;
            Canvas.PointerCanceled += OnPointerCanceled;

            base.OnApplyTemplate();
        }

        public ICanvasResourceCreatorWithDpi Creator => Canvas;

        public PencilTool Tool
        {
            get => _tool;
            set => _tool = value;
        }

        public event EventHandler StrokesChanged;

        public Color Stroke { get; set; }

        /// <summary>
        /// A fraction of the canvas width, as <see cref="PencilTools.GetThickness"/> returns.
        /// </summary>
        public float StrokeThickness { get; set; }

        /// <summary>
        /// The eraser's width, for a pen's eraser end whatever the current tool is.
        /// </summary>
        public float EraserThickness { get; set; }

        /// <summary>
        /// The image under the canvas, upright, which the blur tool paints a blurred copy of.
        /// </summary>
        public SoftwareBitmap BlurSource
        {
            get => _blurSource;
            set
            {
                _blurSource = value;
                DisposeBlur();
            }
        }

        public IReadOnlyList<SmoothPathBuilder> Strokes
        {
            get => _drawing?.ToList();
            set
            {
                if (value == null)
                {
                    return;
                }

                _drawing = value.ToList();

                _strokes = value.ToList();
                _history.Clear();

                _needToRedrawInkSurface = true;
                Canvas?.Invalidate();
            }
        }

        public bool CanUndo => _strokes.Count > 0;
        public bool CanRedo => _history.Count > 0;

        /// <summary>
        /// Raised while two touches pinch or drag over the canvas, in window coordinates.
        /// </summary>
        public event EventHandler<PencilGestureEventArgs> Gesture;

        /// <summary>
        /// Raised when the eyedropper is released over the canvas, with the colour under it.
        /// </summary>
        public event EventHandler<PencilColorPickedEventArgs> ColorPicked;

        /// <summary>
        /// Canvas units per view pixel, so the eyedropper's loupe keeps one size on screen however
        /// the canvas is scaled.
        /// </summary>
        public float LoupeScale { get; set; } = 1;

        public bool IsPicking => _picking;

        /// <summary>
        /// Turns the next press into picking a colour instead of drawing.
        /// </summary>
        public void BeginPick()
        {
            if (_renderTarget == null)
            {
                return;
            }

            StopFill();

            var snapshot = new CanvasRenderTarget(Canvas, Canvas.Size);
            using (var session = snapshot.CreateDrawingSession())
            {
                session.Clear(Colors.Black);

                // GetBlurBrush makes the photo's bitmap as it makes the blur.
                if (GetBlurBrush() != null && _blurBitmap != null)
                {
                    session.DrawImage(_blurBitmap, new Rect(0, 0, Canvas.Size.Width, Canvas.Size.Height), _blurBitmap.Bounds);
                }

                session.DrawImage(_renderTarget);
            }

            _pickSnapshot?.Dispose();
            _pickSnapshot = snapshot;
            _pickPixels = snapshot.GetPixelColors();
            _pickPoint = null;
            _picking = true;
        }

        public void CancelPick()
        {
            if (_picking)
            {
                EndPick();
                Canvas.Invalidate();
            }
        }

        private void EndPick()
        {
            _picking = false;
            _pickPoint = null;
            _pickPixels = null;

            _pickSnapshot?.Dispose();
            _pickSnapshot = null;
        }

        private Color SamplePick(Point point)
        {
            var width = (int)_pickSnapshot.SizeInPixels.Width;
            var height = (int)_pickSnapshot.SizeInPixels.Height;
            var scale = _pickSnapshot.SizeInPixels.Width / _pickSnapshot.Size.Width;

            var x = Math.Clamp((int)(point.X * scale), 0, width - 1);
            var y = Math.Clamp((int)(point.Y * scale), 0, height - 1);

            return _pickPixels[y * width + x];
        }

        public void Undo()
        {
            var last = _strokes.LastOrDefault();
            if (last == null)
            {
                return;
            }

            StopAnimations();

            _strokes.Remove(last);
            _history.Insert(0, last);

            Redraw();
        }

        public void Redo()
        {
            var first = _history.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            StopAnimations();

            _history.Remove(first);
            _strokes.Add(first);

            Redraw();
        }

        public void SaveState()
        {
            _drawing = _strokes.ToList();
        }

        public void RestoreState()
        {
            StopAnimations();

            _strokes = _drawing?.ToList() ?? new List<SmoothPathBuilder>();
            _history.Clear();

            _needToRedrawInkSurface = true;
            Canvas.Invalidate();
        }

        private void Redraw()
        {
            _needToRedrawInkSurface = true;
            Canvas.Invalidate();

            StrokesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Invalidate()
        {
            Canvas?.Invalidate();
        }

        #region Resources

        private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            // Raised again after a device loss, when everything made on the old device is dead.
            DisposeBlur();
            CreateSizeDependentResources();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _needToCreateSizeDependentResources = true;
            DisposeBlur();
            Canvas.Invalidate();
        }

        private void CreateSizeDependentResources()
        {
            _renderTarget = new CanvasRenderTarget(Canvas, Canvas.Size);

            _needToCreateSizeDependentResources = false;
            _needToRedrawInkSurface = true;
        }

        private ICanvasBrush GetBlurBrush()
        {
            if (_blurBrush != null || _blurSource == null || Canvas.Size.Width == 0)
            {
                return _blurBrush;
            }

            _blurBitmap = CanvasBitmap.CreateFromSoftwareBitmap(Canvas, _blurSource);

            var size = Canvas.Size.ToVector2();
            var scale = new Transform2DEffect
            {
                Source = _blurBitmap,
                TransformMatrix = Matrix3x2.CreateScale(size / _blurBitmap.Size.ToVector2())
            };

            _blurEffect = CreateBlur(scale, size.X);
            _blurBrush = new CanvasImageBrush(Canvas, _blurEffect)
            {
                SourceRectangle = new Rect(0, 0, size.X, size.Y)
            };

            return _blurBrush;
        }

        private void DisposeBlur()
        {
            _blurBrush?.Dispose();
            _blurBrush = null;

            _blurEffect?.Dispose();
            _blurEffect = null;

            _blurBitmap?.Dispose();
            _blurBitmap = null;
        }

        /// <summary>
        /// The copy the blur tool paints, for an image <paramref name="width"/> wide in the units
        /// it will be drawn in.
        /// </summary>
        public static GaussianBlurEffect CreateBlur(IGraphicsEffectSource source, float width)
        {
            return new GaussianBlurEffect
            {
                Source = source,
                BlurAmount = width * 0.025f,
                BorderMode = EffectBorderMode.Hard
            };
        }

        #endregion

        #region Drawing

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(Canvas);

            if (_picking)
            {
                Canvas.CapturePointer(e.Pointer);

                _pickPoint = point.Position;
                Canvas.Invalidate();
                return;
            }

            if (e.Pointer.PointerDeviceType == PointerDeviceType.Touch)
            {
                _touches[point.PointerId] = e.GetCurrentPoint(null).Position;

                // A second finger turns the touch into moving the view: whatever the first one
                // had begun is dropped, not drawn.
                if (_touches.Count >= 2 || _gesturing)
                {
                    if (!_gesturing)
                    {
                        _gesturing = true;
                        StopFill();

                        foreach (var id in _touches.Keys)
                        {
                            _builders.TryRemove(id, out _);
                        }

                        Canvas.Invalidate();
                    }

                    Canvas.CapturePointer(e.Pointer);
                    return;
                }
            }

            Canvas.CapturePointer(e.Pointer);

            var tool = point.Properties.IsEraser ? PencilTool.Eraser : _tool;

            // Stored relative to the canvas, like the points are: the same stroke is replayed
            // over the preview and the exported file, both at resolutions unrelated to this one.
            _builders[point.PointerId] = new SmoothPathBuilder(point.Position.ToVector2() / Canvas.Size.ToVector2())
            {
                Tool = tool,
                Stroke = PencilTools.HasColor(tool) ? Stroke : null,
                StrokeThickness = tool == PencilTool.Eraser ? EraserThickness : StrokeThickness
            };

            StartFill(point.PointerId, e.GetCurrentPoint(null).Position);
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var id = e.Pointer.PointerId;

            // A mouse shows the loupe while hovering; a finger or pen only while pressed.
            if (_picking)
            {
                if (e.Pointer.IsInContact || e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
                {
                    _pickPoint = e.GetCurrentPoint(Canvas).Position;
                    Canvas.Invalidate();
                }

                return;
            }

            if (_touches.TryGetValue(id, out Point previous))
            {
                var position = e.GetCurrentPoint(null).Position;

                if (_gesturing)
                {
                    MoveGesture(id, previous, position);
                    return;
                }

                _touches[id] = position;
            }

            if (_builders.TryGetValue(id, out SmoothPathBuilder builder))
            {
                if (_fillPointer == id)
                {
                    var position = e.GetCurrentPoint(null).Position;
                    var x = position.X - _fillOrigin.X;
                    var y = position.Y - _fillOrigin.Y;

                    if (x * x + y * y > FillSlop * FillSlop)
                    {
                        StopFill();
                    }
                }

                builder.MoveTo(e.GetCurrentPoint(Canvas).Position.ToVector2() / Canvas.Size.ToVector2());
                Canvas.Invalidate();
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Canvas.ReleasePointerCapture(e.Pointer);

            if (_picking)
            {
                var color = SamplePick(e.GetCurrentPoint(Canvas).Position);

                EndPick();
                Canvas.Invalidate();

                ColorPicked?.Invoke(this, new PencilColorPickedEventArgs(color));
                return;
            }

            if (_fillPointer == e.Pointer.PointerId)
            {
                StopFill();
            }

            if (_touches.Remove(e.Pointer.PointerId) && _gesturing)
            {
                _gesturing = _touches.Count > 0;
                return;
            }

            if (_builders.TryRemove(e.Pointer.PointerId, out SmoothPathBuilder builder))
            {
                builder.EndFigure(e.GetCurrentPoint(Canvas).Position.ToVector2() / Canvas.Size.ToVector2());

                // A finished arrow's head grows in, so it joins the ink surface only once it has.
                Commit(builder, builder.Tool == PencilTool.Arrow);
            }
        }

        private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (_fillPointer == e.Pointer.PointerId)
            {
                StopFill();
            }

            if (_touches.Remove(e.Pointer.PointerId) && _gesturing)
            {
                _gesturing = _touches.Count > 0;
            }

            if (_builders.TryRemove(e.Pointer.PointerId, out _))
            {
                Canvas.Invalidate();
            }
        }

        private void Commit(SmoothPathBuilder builder, bool grow)
        {
            if (grow)
            {
                FinishGrowing();

                _growing = builder;
                _growingStart = null;
                _growingProgress = 0;

                StartRendering();
            }
            else
            {
                using (var session = _renderTarget.CreateDrawingSession())
                {
                    DrawPath(session, builder, _renderTarget.Size.ToVector2(), GetBlurBrush());
                }
            }

            _strokes.Add(builder);
            _history.Clear();

            Canvas.Invalidate();
            StrokesChanged?.Invoke(this, EventArgs.Empty);
        }

        #region Fill

        private void StartFill(uint pointerId, Point origin)
        {
            if (_fillTimer == null)
            {
                _fillTimer = new DispatcherTimer { Interval = FillDelay };
                _fillTimer.Tick += OnFillTick;
            }

            _fillPointer = pointerId;
            _fillOrigin = origin;

            _fillTimer.Stop();
            _fillTimer.Start();
        }

        private void StopFill()
        {
            _fillPointer = null;
            _fillTimer?.Stop();
        }

        // The press becomes a fill with the same tool and colour, spreading from where it was held.
        private void OnFillTick(object sender, object e)
        {
            var id = _fillPointer;
            StopFill();

            if (id is not uint pointerId || !_builders.TryRemove(pointerId, out SmoothPathBuilder pressed))
            {
                return;
            }

            Commit(new SmoothPathBuilder(pressed.BeginPoint)
            {
                Tool = pressed.Tool,
                Stroke = pressed.Stroke,
                StrokeThickness = pressed.StrokeThickness,
                IsFill = true
            }, true);
        }

        #endregion

        #region Gesture

        private void MoveGesture(uint id, Point previous, Point position)
        {
            Point? other = null;

            foreach (var touch in _touches)
            {
                if (touch.Key != id)
                {
                    other = touch.Value;
                    break;
                }
            }

            _touches[id] = position;

            if (other is not Point anchor)
            {
                return;
            }

            var before = Distance(previous, anchor);
            var after = Distance(position, anchor);

            var previousCenter = new Point((previous.X + anchor.X) / 2, (previous.Y + anchor.Y) / 2);
            var center = new Point((position.X + anchor.X) / 2, (position.Y + anchor.Y) / 2);

            Gesture?.Invoke(this, new PencilGestureEventArgs(center, before > 0 ? after / before : 1, center.X - previousCenter.X, center.Y - previousCenter.Y));
        }

        private static double Distance(Point a, Point b)
        {
            var x = a.X - b.X;
            var y = a.Y - b.Y;

            return Math.Sqrt(x * x + y * y);
        }

        #endregion

        #region Animations

        private void StartRendering()
        {
            if (!_rendering)
            {
                _rendering = true;
                CompositionTarget.Rendering += OnRendering;
            }
        }

        private void OnRendering(object sender, object e)
        {
            var time = ((RenderingEventArgs)e).RenderingTime;

            if (_growing != null)
            {
                _growingStart ??= time;

                var duration = _growing.IsFill ? FillDuration : ArrowDuration;
                var t = Math.Min(1, (time - _growingStart.Value).TotalMilliseconds / duration);

                _growingProgress = _growing.IsFill
                    ? (float)(1 - Math.Pow(1 - t, 5))
                    : (float)(Math.Cos((t + 1) * Math.PI) / 2 + 0.5);

                if (t >= 1)
                {
                    FinishGrowing();
                }
            }

            if (_growing == null)
            {
                _rendering = false;
                CompositionTarget.Rendering -= OnRendering;
            }

            Canvas.Invalidate();
        }

        private void FinishGrowing()
        {
            if (_growing != null)
            {
                using (var session = _renderTarget.CreateDrawingSession())
                {
                    DrawPath(session, _growing, _renderTarget.Size.ToVector2(), GetBlurBrush());
                }

                _growing = null;
            }
        }

        // Undo and redo redraw from the stroke list, which already holds whatever was growing, so
        // the animation simply ends.
        private void StopAnimations()
        {
            _growing = null;

            if (_rendering)
            {
                _rendering = false;
                CompositionTarget.Rendering -= OnRendering;
            }

            _needToRedrawInkSurface = true;
        }

        #endregion

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_needToCreateSizeDependentResources)
            {
                CreateSizeDependentResources();
            }

            var blur = GetBlurBrush();
            var size = sender.Size.ToVector2();

            if (_needToRedrawInkSurface)
            {
                _needToRedrawInkSurface = false;

                using (var canvas = _renderTarget.CreateDrawingSession())
                {
                    canvas.Clear(Colors.Transparent);

                    foreach (var builder in _strokes)
                    {
                        if (builder != _growing)
                        {
                            DrawPath(canvas, builder, _renderTarget.Size.ToVector2(), blur);
                        }
                    }
                }
            }

            args.DrawingSession.DrawImage(_renderTarget);

            if (_growing != null)
            {
                DrawPath(args.DrawingSession, _growing, size, blur, _growingProgress);
            }

            foreach (var builder in _builders.Values)
            {
                // No arrow head until the stroke is finished.
                DrawPath(args.DrawingSession, builder, size, blur, 0);
            }

            if (_picking && _pickPoint is Point point)
            {
                DrawLoupe(args.DrawingSession, point.ToVector2());
            }
        }

        // A magnified patch seven pixels across, in a ring of the colour under its centre pixel.
        private void DrawLoupe(CanvasDrawingSession session, Vector2 center)
        {
            const float Pixels = 7;

            var scale = LoupeScale;
            var radius = 56 * scale;
            var ring = 6 * scale;
            var outline = 3 * scale;

            var color = SamplePick(new Point(center.X, center.Y));
            var pixel = (float)(_pickSnapshot.Size.Width / _pickSnapshot.SizeInPixels.Width);

            var inner = radius - ring - outline;
            var cell = inner * 2 / Pixels;

            // Snapped to the pixel under the pointer, so the grid lines up with real pixels.
            var source = new Rect(
                Math.Floor(center.X / pixel) * pixel - pixel * 3,
                Math.Floor(center.Y / pixel) * pixel - pixel * 3,
                pixel * Pixels,
                pixel * Pixels);

            using (var clip = CanvasGeometry.CreateCircle(session, center, inner))
            using (session.CreateLayer(1, clip))
            {
                session.DrawImage(_pickSnapshot, new Rect(center.X - inner, center.Y - inner, inner * 2, inner * 2), source, 1, CanvasImageInterpolation.NearestNeighbor);

                var lines = Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF);

                for (int i = 1; i < Pixels; i++)
                {
                    var offset = -inner + i * cell;
                    session.DrawLine(center.X + offset, center.Y - inner, center.X + offset, center.Y + inner, lines, scale);
                    session.DrawLine(center.X - inner, center.Y + offset, center.X + inner, center.Y + offset, lines, scale);
                }

                session.DrawRoundedRectangle(center.X - cell / 2, center.Y - cell / 2, cell, cell, scale, scale, Colors.White, 2 * scale);
            }

            session.DrawCircle(center, radius - ring / 2, Color.FromArgb(255, color.R, color.G, color.B), ring);
            session.DrawCircle(center, radius - ring - outline / 2, Colors.White, outline);
        }

        // Far enough that the disc has covered every corner a little before it stops growing.
        private static float GetFillRadius(Vector2 origin, Vector2 size)
        {
            var corners = Math.Max(
                Math.Max(Vector2.Distance(origin, Vector2.Zero), Vector2.Distance(origin, new Vector2(size.X, 0))),
                Math.Max(Vector2.Distance(origin, new Vector2(0, size.Y)), Vector2.Distance(origin, size)));

            return corners / 0.84f;
        }

        /// <summary>
        /// Draws a stroke onto a surface <paramref name="canvasSize"/> big in the session's units.
        /// <paramref name="blur"/> is the blurred image the blur tool paints, in the surface's own
        /// pixels; without one, blur strokes are skipped.
        /// </summary>
        public static void DrawPath(CanvasDrawingSession canvas, SmoothPathBuilder builder, Vector2 canvasSize, ICanvasBrush blur = null, float progress = 1)
        {
            // Strokes from before there were tools carry no tool, and erased by having no colour.
            var tool = builder.Stroke == null && builder.Tool != PencilTool.Blur
                ? PencilTool.Eraser
                : builder.Tool;

            if (builder.IsFill)
            {
                DrawFill(canvas, tool, builder.Stroke ?? Colors.Transparent, builder.BeginPoint * canvasSize, canvasSize, blur, progress);
                return;
            }

            var width = builder.StrokeThickness * canvasSize.X;
            var color = builder.Stroke ?? Colors.Transparent;

            using var geometry = builder.ToGeometry(canvas, canvasSize);
            using var style = new CanvasStrokeStyle
            {
                StartCap = CanvasCapStyle.Round,
                EndCap = CanvasCapStyle.Round,
                LineJoin = CanvasLineJoin.Round
            };

            switch (tool)
            {
                case PencilTool.Eraser:
                    canvas.Blend = CanvasBlend.Copy;
                    canvas.DrawGeometry(geometry, Colors.Transparent, width, style);
                    canvas.Blend = CanvasBlend.SourceOver;
                    break;

                case PencilTool.Blur:
                    if (blur != null)
                    {
                        // The brush is in the surface's pixels, the geometry in the stroke's.
                        Matrix3x2.Invert(canvas.Transform, out Matrix3x2 inverse);
                        blur.Transform = inverse;

                        using var outline = geometry.Stroke(width, style);
                        canvas.FillGeometry(outline, blur);
                    }
                    break;

                case PencilTool.Marker:
                    // One layer per stroke, so it does not darken where it crosses itself.
                    using (canvas.CreateLayer(0.45f * color.A / 255f))
                    {
                        canvas.DrawGeometry(geometry, Opaque(color), width, style);
                    }
                    break;

                case PencilTool.Neon:
                    DrawNeon(canvas, geometry, style, color, width);
                    break;

                default:
                    using (color.A < 255 ? canvas.CreateLayer(color.A / 255f) : null)
                    {
                        canvas.DrawGeometry(geometry, Opaque(color), width, style);

                        if (tool == PencilTool.Arrow && progress > 0)
                        {
                            using var head = builder.ToArrowHead(canvas, canvasSize, width * 10 * progress);
                            if (head != null)
                            {
                                canvas.DrawGeometry(head, Opaque(color), width, style);
                            }
                        }
                    }
                    break;
            }
        }

        // The whole canvas, or while it grows a disc spreading from where the press was held. Marker
        // and neon fill as plain colour.
        private static void DrawFill(CanvasDrawingSession canvas, PencilTool tool, Color color, Vector2 origin, Vector2 canvasSize, ICanvasBrush blur, float progress)
        {
            using var geometry = progress < 1
                ? CanvasGeometry.CreateCircle(canvas, origin, GetFillRadius(origin, canvasSize) * progress)
                : CanvasGeometry.CreateRectangle(canvas, 0, 0, canvasSize.X, canvasSize.Y);

            switch (tool)
            {
                case PencilTool.Eraser:
                    canvas.Blend = CanvasBlend.Copy;
                    canvas.FillGeometry(geometry, Colors.Transparent);
                    canvas.Blend = CanvasBlend.SourceOver;
                    break;

                case PencilTool.Blur:
                    if (blur != null)
                    {
                        Matrix3x2.Invert(canvas.Transform, out Matrix3x2 inverse);
                        blur.Transform = inverse;

                        canvas.FillGeometry(geometry, blur);
                    }
                    break;

                default:
                    canvas.FillGeometry(geometry, color);
                    break;
            }
        }

        // A white core, a rim tinted almost to white and a glow in the colour, all in proportion to
        // the stroke's width.
        private static void DrawNeon(CanvasDrawingSession canvas, CanvasGeometry geometry, CanvasStrokeStyle style, Color color, float width)
        {
            var opaque = Opaque(color);
            var rim = Color.FromArgb(255,
                (byte)(opaque.R + (255 - opaque.R) * 0.86f),
                (byte)(opaque.G + (255 - opaque.G) * 0.86f),
                (byte)(opaque.B + (255 - opaque.B) * 0.86f));

            using (canvas.CreateLayer(color.A / 255f))
            {
                using (var glow = new CanvasCommandList(canvas))
                {
                    using (var session = glow.CreateDrawingSession())
                    {
                        session.DrawGeometry(geometry, opaque, width * 0.75f, style);
                    }

                    using var blur = new GaussianBlurEffect
                    {
                        Source = glow,
                        BlurAmount = width * 0.12f
                    };

                    canvas.DrawImage(blur);
                }

                canvas.DrawGeometry(geometry, rim, width * 0.44f, style);
                canvas.DrawGeometry(geometry, Colors.White, width * 0.375f, style);
            }
        }

        private static Color Opaque(Color color)
        {
            return Color.FromArgb(255, color.R, color.G, color.B);
        }

        #endregion
    }

    public sealed partial class PencilColorPickedEventArgs : EventArgs
    {
        public PencilColorPickedEventArgs(Color color)
        {
            Color = color;
        }

        public Color Color { get; }
    }

    public sealed partial class PencilGestureEventArgs : EventArgs
    {
        public PencilGestureEventArgs(Point center, double scale, double x, double y)
        {
            Center = center;
            Scale = scale;
            X = x;
            Y = y;
        }

        public Point Center { get; }

        public double Scale { get; }

        public double X { get; }

        public double Y { get; }
    }

    public sealed partial class SmoothPathBuilder
    {
        private List<Vector2> _controlPoints;
        private List<Vector2> _path;

        private Vector2 _beginPoint;

        public SmoothPathBuilder(Vector2 beginPoint)
        {
            _beginPoint = beginPoint;

            _controlPoints = new List<Vector2>();
            _path = new List<Vector2>();
        }

        public PencilTool Tool { get; set; }

        /// <summary>
        /// Covers the whole canvas rather than following a path; <see cref="BeginPoint"/> is
        /// where the press that made it was held.
        /// </summary>
        public bool IsFill { get; set; }

        public Color? Stroke { get; set; }

        /// <summary>
        /// A fraction of the canvas width, not a length: the geometry is normalized the
        /// same way, so both scale together to whatever surface the stroke is drawn on.
        /// </summary>
        public float StrokeThickness { get; set; }

        public Vector2 BeginPoint
        {
            get => _beginPoint;
            set => _beginPoint = value;
        }

        public List<Vector2> Path
        {
            get => _path;
            set => _path = value;
        }

        public void MoveTo(Vector2 point)
        {
            if (_controlPoints.Count < 4)
            {
                _controlPoints.Add(point);
                return;
            }

            var endPoint = new Vector2(
                (_controlPoints[2].X + point.X) / 2,
                (_controlPoints[2].Y + point.Y) / 2);

            _path.Add(_controlPoints[1]);
            _path.Add(_controlPoints[2]);
            _path.Add(endPoint);

            _controlPoints = new List<Vector2> { endPoint, point };
        }

        public void EndFigure(Vector2 point)
        {
            if (_controlPoints.Count > 1)
            {
                for (int i = 0; i < _controlPoints.Count; i++)
                {
                    MoveTo(point);
                }
            }
        }

        public CanvasGeometry ToGeometry(ICanvasResourceCreator resourceCreator, Vector2 canvasSize)
        {
            var multiplier = canvasSize;

            using var builder = new CanvasPathBuilder(resourceCreator);
            builder.BeginFigure(_beginPoint * multiplier);

            for (int i = 0; i < _path.Count; i += 3)
            {
                builder.AddCubicBezier(
                    _path[i] * multiplier,
                    _path[i + 1] * multiplier,
                    _path[i + 2] * multiplier);
            }

            builder.EndFigure(CanvasFigureLoop.Open);

            return CanvasGeometry.CreatePath(builder);
        }

        /// <summary>
        /// Two lines <paramref name="length"/> long swept back from the end of the stroke, or null
        /// when the stroke is too short to have a direction.
        /// </summary>
        public CanvasGeometry ToArrowHead(ICanvasResourceCreator resourceCreator, Vector2 canvasSize, float length)
        {
            var end = (_path.Count > 0 ? _path[^1] : _beginPoint) * canvasSize;
            var from = _beginPoint * canvasSize;

            // The direction over the last stretch, not the last segment, which a hand's final
            // twitch can turn any way.
            var reach = StrokeThickness * canvasSize.X * 4;

            for (int i = _path.Count - 4; i >= 0; i -= 3)
            {
                var point = _path[i] * canvasSize;
                if (Vector2.Distance(point, end) >= reach)
                {
                    from = point;
                    break;
                }
            }

            if (Vector2.Distance(from, end) < 1)
            {
                return null;
            }

            var angle = MathF.Atan2(end.Y - from.Y, end.X - from.X);
            var spread = MathF.PI * 0.85f;

            using var builder = new CanvasPathBuilder(resourceCreator);
            builder.BeginFigure(end + new Vector2(MathF.Cos(angle - spread), MathF.Sin(angle - spread)) * length);
            builder.AddLine(end);
            builder.AddLine(end + new Vector2(MathF.Cos(angle + spread), MathF.Sin(angle + spread)) * length);
            builder.EndFigure(CanvasFigureLoop.Open);

            return CanvasGeometry.CreatePath(builder);
        }
    }
}
