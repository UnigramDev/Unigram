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

        private CanvasRenderTarget _renderTarget;

        private ConcurrentDictionary<uint, SmoothPathBuilder> _builders;

        private List<SmoothPathBuilder> _drawing;

        private List<SmoothPathBuilder> _strokes = new();
        private readonly List<SmoothPathBuilder> _history = new();

        private PencilTool _tool;

        // An arrow whose head is still growing: drawn over the ink surface, not into it, until done.
        private SmoothPathBuilder _arrow;
        private TimeSpan? _arrowStart;
        private float _arrowProgress;

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

                _strokes = value?.ToList() ?? new List<SmoothPathBuilder>();
                _history.Clear();

                Canvas?.Invalidate();
            }
        }

        public bool CanUndo => _strokes.Count > 0;
        public bool CanRedo => _history.Count > 0;

        public void Undo()
        {
            var last = _strokes.LastOrDefault();
            if (last == null)
            {
                return;
            }

            _strokes.Remove(last);
            _history.Insert(0, last);

            if (last == _arrow)
            {
                StopArrow();
            }

            _needToRedrawInkSurface = true;
            Canvas.Invalidate();

            StrokesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            var first = _history.FirstOrDefault();
            if (first == null)
            {
                return;
            }

            _history.Remove(first);
            _strokes.Add(first);

            _needToRedrawInkSurface = true;
            Canvas.Invalidate();

            StrokesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SaveState()
        {
            _drawing = _strokes.ToList();
        }

        public void RestoreState()
        {
            StopArrow();

            _strokes = _drawing?.ToList() ?? new List<SmoothPathBuilder>();
            _history.Clear();

            _needToRedrawInkSurface = true;
            Canvas.Invalidate();
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
            Canvas.CapturePointer(e.Pointer);

            var point = e.GetCurrentPoint(Canvas);
            var tool = point.Properties.IsEraser ? PencilTool.Eraser : _tool;

            // Stored relative to the canvas, like the points are: the same stroke is replayed
            // over the preview and the exported file, both at resolutions unrelated to this one.
            _builders[point.PointerId] = new SmoothPathBuilder(point.Position.ToVector2() / Canvas.Size.ToVector2())
            {
                Tool = tool,
                Stroke = PencilTools.HasColor(tool) ? Stroke : null,
                StrokeThickness = tool == PencilTool.Eraser ? EraserThickness : StrokeThickness
            };
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_builders.TryGetValue(e.Pointer.PointerId, out SmoothPathBuilder _builder))
            {
                _builder.MoveTo(e.GetCurrentPoint(Canvas).Position.ToVector2() / Canvas.Size.ToVector2());
                Canvas.Invalidate();
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Canvas.ReleasePointerCapture(e.Pointer);

            if (_builders.TryRemove(e.Pointer.PointerId, out SmoothPathBuilder builder))
            {
                builder.EndFigure(e.GetCurrentPoint(Canvas).Position.ToVector2() / Canvas.Size.ToVector2());

                // A finished arrow's head grows in, so it joins the ink surface only once it has.
                if (builder.Tool == PencilTool.Arrow)
                {
                    StopArrow();

                    _arrow = builder;
                    _arrowStart = null;
                    _arrowProgress = 0;
                    CompositionTarget.Rendering += OnRendering;
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
        }

        private void OnRendering(object sender, object e)
        {
            var time = ((RenderingEventArgs)e).RenderingTime;
            _arrowStart ??= time;

            var t = Math.Min(1, (time - _arrowStart.Value).TotalMilliseconds / ArrowDuration);
            _arrowProgress = (float)(Math.Cos((t + 1) * Math.PI) / 2 + 0.5);

            if (t >= 1)
            {
                using (var session = _renderTarget.CreateDrawingSession())
                {
                    DrawPath(session, _arrow, _renderTarget.Size.ToVector2());
                }

                StopArrow();
            }

            Canvas.Invalidate();
        }

        private void StopArrow()
        {
            if (_arrow != null)
            {
                CompositionTarget.Rendering -= OnRendering;
                _arrow = null;
            }
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_needToCreateSizeDependentResources)
            {
                CreateSizeDependentResources();
            }

            var blur = GetBlurBrush();

            if (_needToRedrawInkSurface)
            {
                _needToRedrawInkSurface = false;

                using (var canvas = _renderTarget.CreateDrawingSession())
                {
                    canvas.Clear(Colors.Transparent);

                    foreach (var builder in _strokes)
                    {
                        if (builder != _arrow)
                        {
                            DrawPath(canvas, builder, _renderTarget.Size.ToVector2(), blur);
                        }
                    }
                }
            }

            args.DrawingSession.DrawImage(_renderTarget);

            if (_arrow != null)
            {
                DrawPath(args.DrawingSession, _arrow, sender.Size.ToVector2(), null, _arrowProgress);
            }

            foreach (var builder in _builders.Values)
            {
                // No arrow head until the stroke is finished.
                DrawPath(args.DrawingSession, builder, sender.Size.ToVector2(), blur, 0);
            }
        }

        /// <summary>
        /// Draws a stroke onto a surface <paramref name="canvasSize"/> big in the session's units.
        /// <paramref name="blur"/> is the blurred image the blur tool paints, in the surface's own
        /// pixels; without one, blur strokes are skipped.
        /// </summary>
        public static void DrawPath(CanvasDrawingSession canvas, SmoothPathBuilder builder, Vector2 canvasSize, ICanvasBrush blur = null, float arrowProgress = 1)
        {
            // Strokes from before there were tools carry no tool, and erased by having no colour.
            var tool = builder.Stroke == null && builder.Tool != PencilTool.Blur
                ? PencilTool.Eraser
                : builder.Tool;

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

                        if (tool == PencilTool.Arrow && arrowProgress > 0)
                        {
                            using var head = builder.ToArrowHead(canvas, canvasSize, width * 10 * arrowProgress);
                            if (head != null)
                            {
                                canvas.DrawGeometry(head, Opaque(color), width, style);
                            }
                        }
                    }
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
