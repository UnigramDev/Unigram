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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using Telegram.Entities;
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
        Eraser,

        /// <summary>
        /// Not drawn with: the colour and size given to new text.
        /// </summary>
        Text,

        /// <summary>
        /// Not drawn with either: the colour and weight given to new shapes.
        /// </summary>
        Shape
    }

    /// <summary>
    /// What each tool draws with. Weights are the slider's own scale; <see cref="GetThickness"/>
    /// turns one into a stroke width relative to the painting's width.
    /// </summary>
    public static class PencilTools
    {
        public static double GetMinimumWeight(PencilTool tool) => tool == PencilTool.Text ? 0.5 : HasColor(tool) ? 0.05 : 0.4;

        public static double GetMaximumWeight(PencilTool tool) => tool == PencilTool.Text ? 2 : HasColor(tool) ? 1 : 1.75;

        public static double GetDefaultWeight(PencilTool tool) => tool switch
        {
            PencilTool.Pen or PencilTool.Arrow or PencilTool.Shape => 0.25,
            PencilTool.Marker or PencilTool.Neon => 0.5,
            _ => 1
        };

        public static Color GetDefaultColor(PencilTool tool) => tool switch
        {
            PencilTool.Arrow => Color.FromArgb(0xFF, 0xFF, 0x8A, 0x00),
            PencilTool.Marker => Color.FromArgb(0xFF, 0xFF, 0xD6, 0x0A),
            PencilTool.Neon => Color.FromArgb(0xFF, 0x34, 0xC7, 0x59),
            PencilTool.Text => Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF),
            _ => Color.FromArgb(0xFF, 0xFF, 0x45, 0x3A)
        };

        public static bool HasColor(PencilTool tool) => tool is not PencilTool.Blur and not PencilTool.Eraser;

        public static float GetThickness(PencilTool tool, double weight)
        {
            return (float)((8 + 90 * weight) / 2048 * GetScale(tool));
        }

        /// <summary>
        /// The weight a stroke of <paramref name="thickness"/> was drawn at.
        /// </summary>
        public static double GetWeight(PencilTool tool, float thickness)
        {
            return (thickness * 2048 / GetScale(tool) - 8) / 90;
        }

        // An arrow's line is drawn thinner than a pen's at the same weight, so that its head,
        // which is sized from the weight, stands out.
        private static double GetScale(PencilTool tool) => tool switch
        {
            PencilTool.Arrow => 0.5,
            PencilTool.Marker => 1.4,
            PencilTool.Neon => 1.45,
            _ => 1
        };
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

        // A pen's or a marker's stroke is matched against the known shapes whenever the hand
        // pauses, for its first seconds. Points are in the canvas' pixels.
        private const double RecognitionWindow = 3000;
        private const double RecognitionFade = 250;
        private static readonly TimeSpan RecognitionDelay = TimeSpan.FromMilliseconds(150);

        private static readonly Stopwatch _clock = Stopwatch.StartNew();

        private uint? _recognizing;
        private readonly List<Vector2> _recognitionPoints = new();
        private double _recognitionStart;
        private DispatcherTimer _recognitionTimer;
        private RecognizedShape _recognized;
        private RecognizedShape _preview;
        private double _recognizedChanged;

        // The eyedropper samples a snapshot of the photo with the drawing over it, read back once
        // when picking starts so following the pointer costs nothing.
        private bool _picking;
        private CanvasRenderTarget _pickSnapshot;
        private Color[] _pickPixels;
        private Point? _pickPoint;

        private SoftwareBitmap _blurSource;
        private CanvasBitmap _blurBitmap;
        private CanvasRenderTarget _blurred;
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

        /// <summary>
        /// Raised when a stroke or fill is finished, before <see cref="StrokesChanged"/>.
        /// </summary>
        public event EventHandler StrokeAdded;

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

        public int StrokeCount => _strokes.Count;

        /// <summary>
        /// Forgets what <see cref="Redo"/> would bring back, when something other than a stroke
        /// has been done since.
        /// </summary>
        public void ClearRedo()
        {
            _history.Clear();
        }

        /// <summary>
        /// Raised while two touches pinch or drag over the canvas, in window coordinates.
        /// </summary>
        public event EventHandler<PencilGestureEventArgs> Gesture;

        /// <summary>
        /// Raised when a stroke is let go of as a shape, which takes its place: the stroke itself
        /// is not kept.
        /// </summary>
        public event EventHandler<PencilShapeRecognizedEventArgs> ShapeRecognized;

        /// <summary>
        /// Raised when a press starts a stroke.
        /// </summary>
        public event EventHandler StrokeStarted;

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
        /// Turns the next press into picking a colour instead of drawing, from the strokes over
        /// <paramref name="underlay"/>, what lies under the canvas laid out at its size, or else
        /// over the photo the blur tool uses.
        /// </summary>
        public void BeginPick(SoftwareBitmap underlay = null)
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

                if (underlay != null)
                {
                    using var bitmap = CanvasBitmap.CreateFromSoftwareBitmap(Canvas, underlay);
                    session.DrawImage(bitmap, new Rect(0, 0, Canvas.Size.Width, Canvas.Size.Height), bitmap.Bounds);
                }
                // GetBlurBrush makes the photo's bitmap as it makes the blur.
                else if (GetBlurBrush() != null && _blurBitmap != null)
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

            // Blurred once: a brush over the effect itself would blur again under every stroke,
            // each frame, over an area that grows with the stroke.
            _blurred = new CanvasRenderTarget(Canvas, Canvas.Size);

            using (var scale = new Transform2DEffect { Source = _blurBitmap, TransformMatrix = Matrix3x2.CreateScale(size / _blurBitmap.Size.ToVector2()) })
            using (var effect = CreateBlur(scale, size.X))
            using (var session = _blurred.CreateDrawingSession())
            {
                session.DrawImage(effect);
            }

            _blurBrush = new CanvasImageBrush(Canvas, _blurred)
            {
                SourceRectangle = new Rect(0, 0, size.X, size.Y)
            };

            return _blurBrush;
        }

        private void DisposeBlur()
        {
            _blurBrush?.Dispose();
            _blurBrush = null;

            _blurred?.Dispose();
            _blurred = null;

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

            // Only the left button draws: the right one is the objects' menu, and the side ones
            // are back and forward, which a capture here would keep from the window.
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && !point.Properties.IsLeftButtonPressed)
            {
                return;
            }

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
                        StopRecognition();

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

            StrokeStarted?.Invoke(this, EventArgs.Empty);

            // Stored relative to the canvas, like the points are: the same stroke is replayed
            // over the preview and the exported file, both at resolutions unrelated to this one.
            _builders[point.PointerId] = new SmoothPathBuilder(point.Position.ToVector2() / Canvas.Size.ToVector2())
            {
                Tool = tool,
                Stroke = PencilTools.HasColor(tool) ? Stroke : null,
                StrokeThickness = tool == PencilTool.Eraser ? EraserThickness : StrokeThickness
            };

            StartFill(point.PointerId, e.GetCurrentPoint(null).Position);

            if (tool is PencilTool.Pen or PencilTool.Marker && ShapeRecognized != null)
            {
                StartRecognition(point.PointerId, point.Position.ToVector2());
            }
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

                var current = e.GetCurrentPoint(Canvas).Position.ToVector2();

                builder.MoveTo(current / Canvas.Size.ToVector2());
                Canvas.Invalidate();

                if (_recognizing == id)
                {
                    AddRecognitionPoint(current);
                }
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

                if (_recognizing == e.Pointer.PointerId)
                {
                    var shape = _recognized;
                    StopRecognition();

                    if (shape != null)
                    {
                        Canvas.Invalidate();

                        var weight = PencilTools.GetWeight(builder.Tool, builder.StrokeThickness);
                        ShapeRecognized?.Invoke(this, new PencilShapeRecognizedEventArgs(shape, builder.Stroke ?? Colors.Black, (float)weight));
                        return;
                    }
                }

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

            if (_recognizing == e.Pointer.PointerId)
            {
                StopRecognition();
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
            StrokeAdded?.Invoke(this, EventArgs.Empty);
            StrokesChanged?.Invoke(this, EventArgs.Empty);
        }

        #region Recognition

        private void StartRecognition(uint pointerId, Vector2 point)
        {
            StopRecognition();

            _recognizing = pointerId;
            _recognitionStart = _clock.Elapsed.TotalMilliseconds;
            _recognitionPoints.Add(point);
        }

        private void StopRecognition()
        {
            _recognizing = null;
            _recognitionPoints.Clear();
            _recognitionTimer?.Stop();

            _recognized = null;
            _preview = null;
        }

        // Points closer than a few pixels on screen add nothing. A quick move puts recognition off
        // until the hand slows down again.
        private void AddRecognitionPoint(Vector2 point)
        {
            if (_clock.Elapsed.TotalMilliseconds - _recognitionStart > RecognitionWindow)
            {
                if (_recognized != null)
                {
                    Canvas.Invalidate();
                }

                StopRecognition();
                return;
            }

            var distance = Vector2.Distance(point, _recognitionPoints[_recognitionPoints.Count - 1]);
            if (distance < 5 * LoupeScale)
            {
                return;
            }

            _recognitionPoints.Add(point);

            if (_recognitionTimer == null)
            {
                _recognitionTimer = new DispatcherTimer { Interval = RecognitionDelay };
                _recognitionTimer.Tick += OnRecognitionTick;
            }

            if (distance > 6 * LoupeScale)
            {
                _recognitionTimer.Stop();
                _recognitionTimer.Start();
            }
            else if (!_recognitionTimer.IsEnabled)
            {
                _recognitionTimer.Start();
            }
        }

        private void OnRecognitionTick(object sender, object e)
        {
            _recognitionTimer.Stop();

            if (_recognizing == null || _recognitionPoints.Count < ShapeRecognizer.MinimumPoints)
            {
                return;
            }

            var shape = ShapeRecognizer.Recognize(_recognitionPoints);

            // A shape fades in over the stroke when one is found and out when it is lost.
            if ((shape != null) != (_recognized != null))
            {
                _recognizedChanged = _clock.Elapsed.TotalMilliseconds;
            }

            _recognized = shape;
            _preview = shape ?? _preview;

            Canvas.Invalidate();
        }

        private void DrawRecognition(CanvasDrawingSession session, SmoothPathBuilder builder, Vector2 size, ICanvasBrush blur)
        {
            var t = Math.Min(1, (_clock.Elapsed.TotalMilliseconds - _recognizedChanged) / RecognitionFade);
            var eased = 1 - Math.Pow(1 - t, 5);
            var alpha = (float)(_recognized != null ? eased : 1 - eased);

            if (t < 1)
            {
                Canvas.Invalidate();
            }

            using (session.CreateLayer(1 - 0.5f * alpha))
            {
                DrawPath(session, builder, size, blur, 0);
            }

            if (_preview == null || alpha <= 0)
            {
                return;
            }

            var weight = PencilTools.GetWeight(builder.Tool, builder.StrokeThickness);
            var entity = _preview.ToEntity(size.X / PaintEntityRenderer.Reference, builder.Stroke ?? Colors.Black, (float)weight);

            var previous = session.Transform;
            session.Transform = Matrix3x2.CreateScale(size.X / PaintEntityRenderer.Reference)
                * Matrix3x2.CreateRotation(entity.Rotation)
                * Matrix3x2.CreateTranslation(_preview.Center)
                * previous;

            using (session.CreateLayer(0.5f * alpha))
            {
                PaintEntityRenderer.DrawShape(session, entity);
            }

            session.Transform = previous;
        }

        #endregion

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

            foreach (var pair in _builders)
            {
                if (pair.Key == _recognizing && _preview != null)
                {
                    DrawRecognition(args.DrawingSession, pair.Value, size, blur);
                }
                else
                {
                    // No arrow head until the stroke is finished.
                    DrawPath(args.DrawingSession, pair.Value, size, blur, 0);
                }
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

                        // Stroked with the brush, not filled as an outline: widening the path on the
                        // CPU costs more with every point, and the stroke being drawn is redrawn
                        // whole each frame.
                        canvas.DrawGeometry(geometry, blur, width, style);
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

    public sealed partial class PencilShapeRecognizedEventArgs : EventArgs
    {
        public PencilShapeRecognizedEventArgs(RecognizedShape shape, Color color, float weight)
        {
            Shape = shape;
            Color = color;
            Weight = weight;
        }

        public RecognizedShape Shape { get; }

        public Color Color { get; }

        public float Weight { get; }
    }

    /// <summary>
    /// A shape found in a stroke, in the stroke's own pixels.
    /// </summary>
    public sealed partial class RecognizedShape
    {
        public PaintShapeKind Kind { get; init; }

        public Vector2 Center { get; init; }

        /// <summary>
        /// Half the width and the height.
        /// </summary>
        public Vector2 Size { get; init; }

        public float Rotation { get; init; }

        public Vector2 ArrowStart { get; init; }

        public Vector2 ArrowBend { get; init; }

        public Vector2 ArrowTip { get; init; }

        /// <summary>
        /// The shape as an object around its centre, <paramref name="pixelsPerUnit"/> of these
        /// pixels to a unit of the renderer's at a scale of one.
        /// </summary>
        public PaintEntity ToEntity(float pixelsPerUnit, Color color, float weight)
        {
            var entity = new PaintEntity
            {
                Type = PaintEntityType.Shape,
                Shape = Kind,
                Color = color,
                ShapeWeight = Math.Clamp(weight, 0.05f, 1),
                ShapeSize = Size / pixelsPerUnit,
                Rotation = Rotation,
                ArrowStart = (ArrowStart - Center) / pixelsPerUnit,
                ArrowBend = (ArrowBend - Center) / pixelsPerUnit,
                ArrowTip = (ArrowTip - Center) / pixelsPerUnit
            };

            entity.ArrowHead = 16 * PaintEntityRenderer.GetShapeThickness(entity);
            return entity;
        }
    }

    /// <summary>
    /// Tells a hand-drawn circle, rectangle, star or arrow by comparing its stroke with templates of
    /// each, the $1 recognizer's way. The templates were recorded through exactly these steps, so
    /// none of them may change: a stroke is resampled, turned by its indicative angle (one way, not
    /// the other), centred and stretched to a square, then compared at its best turn.
    /// </summary>
    public static class ShapeRecognizer
    {
        public const int MinimumPoints = 8;

        private const int PointsCount = 48;
        private const double SquareSize = 250;
        private const double AngleRange = Math.PI / 2;
        private const double AnglePrecision = Math.PI / 45;
        private const double Threshold = 0.8;

        private static readonly double HalfDiagonal = Math.Sqrt(SquareSize * SquareSize * 2) / 2;

        // Its kinds are numbered as PaintShapeKind; any other number is a template of what is no
        // shape, which a match turns down.
        private sealed class Template
        {
            public int Type;
            public double[] X;
            public double[] Y;
        }

        private static Template[] _templates;

        // Records with no header: a type, a point count, two offsets above 64, then the points.
        private static Template[] LoadTemplates()
        {
            var templates = new List<Template>();

            try
            {
                var bytes = File.ReadAllBytes(Path.Combine(AppPaths.InstalledPath, "Assets", "shapes.dat"));
                var offset = 0;

                while (bytes.Length - offset > 5)
                {
                    var type = bytes[offset];
                    var count = bytes[offset + 1];
                    var ox = bytes[offset + 2] - 64;
                    var oy = bytes[offset + 3] - 64;

                    offset += 4;

                    if (bytes.Length - offset < count * 2)
                    {
                        break;
                    }

                    var template = new Template
                    {
                        Type = type,
                        X = new double[count],
                        Y = new double[count]
                    };

                    for (int i = 0; i < count; i++)
                    {
                        template.X[i] = bytes[offset++] - ox - 127;
                        template.Y[i] = bytes[offset++] - oy - 127;
                    }

                    templates.Add(template);
                }
            }
            catch
            {
                // Without its templates nothing is recognized; drawing goes on as it was.
            }

            return templates.ToArray();
        }

        public static RecognizedShape Recognize(IReadOnlyList<Vector2> stroke)
        {
            _templates ??= LoadTemplates();

            if (stroke.Count < MinimumPoints || _templates.Length == 0)
            {
                return null;
            }

            var x = new List<double>(stroke.Count);
            var y = new List<double>(stroke.Count);

            foreach (var point in stroke)
            {
                x.Add(point.X);
                y.Add(point.Y);
            }

            Resample(x, y, out double[] rx, out double[] ry);

            if (rx.Length < 2)
            {
                return null;
            }

            var nx = (double[])rx.Clone();
            var ny = (double[])ry.Clone();

            Centroid(nx, ny, out double cx, out double cy);
            Rotate(nx, ny, Math.Atan2(cy - ny[0], cx - nx[0]), cx, cy);

            Centroid(nx, ny, out cx, out cy);

            for (int i = 0; i < nx.Length; i++)
            {
                nx[i] -= cx;
                ny[i] -= cy;
            }

            if (!Scale(nx, ny))
            {
                return null;
            }

            Centroid(nx, ny, out cx, out cy);

            var best = double.MaxValue;
            var type = -1;

            foreach (var template in _templates)
            {
                var distance = DistanceAtBestAngle(nx, ny, cx, cy, template);
                if (distance < best)
                {
                    best = distance;
                    type = template.Type;
                }
            }

            if (1 - best / HalfDiagonal < Threshold || type < 0 || type > (int)PaintShapeKind.Arrow)
            {
                return null;
            }

            return Construct((PaintShapeKind)type, rx, ry);
        }

        // Evenly spaced along the stroke's length.
        private static void Resample(List<double> x, List<double> y, out double[] rx, out double[] ry)
        {
            var length = 0.0;

            for (int i = 1; i < x.Count; i++)
            {
                length += Distance(x[i - 1], y[i - 1], x[i], y[i]);
            }

            var interval = length / (PointsCount - 1);
            var outX = new List<double>(PointsCount) { x[0] };
            var outY = new List<double>(PointsCount) { y[0] };

            var walked = 0.0;

            for (int i = 1; i < x.Count; i++)
            {
                var current = Distance(x[i - 1], y[i - 1], x[i], y[i]);

                if (walked + current >= interval && current > 0)
                {
                    var t = (interval - walked) / current;
                    var px = x[i - 1] + t * (x[i] - x[i - 1]);
                    var py = y[i - 1] + t * (y[i] - y[i - 1]);

                    outX.Add(px);
                    outY.Add(py);

                    x.Insert(i, px);
                    y.Insert(i, py);

                    walked = 0;
                }
                else
                {
                    walked += current;
                }
            }

            if (outX.Count == PointsCount - 1)
            {
                outX.Add(x[x.Count - 1]);
                outY.Add(y[y.Count - 1]);
            }

            rx = outX.ToArray();
            ry = outY.ToArray();
        }

        private static void Centroid(double[] x, double[] y, out double cx, out double cy)
        {
            cx = 0;
            cy = 0;

            for (int i = 0; i < x.Length; i++)
            {
                cx += x[i];
                cy += y[i];
            }

            cx /= x.Length;
            cy /= x.Length;
        }

        private static void Rotate(double[] x, double[] y, double radians, double cx, double cy)
        {
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            for (int i = 0; i < x.Length; i++)
            {
                var dx = x[i] - cx;
                var dy = y[i] - cy;

                x[i] = dx * cos - dy * sin + cx;
                y[i] = dx * sin + dy * cos + cy;
            }
        }

        // Each axis on its own, to the template's square; a straight line cannot be.
        private static bool Scale(double[] x, double[] y)
        {
            Bounds(x, y, out double left, out double top, out double right, out double bottom);

            var width = right - left;
            var height = bottom - top;

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            for (int i = 0; i < x.Length; i++)
            {
                x[i] *= SquareSize / width;
                y[i] *= SquareSize / height;
            }

            return true;
        }

        private static void Bounds(double[] x, double[] y, out double left, out double top, out double right, out double bottom)
        {
            left = right = x[0];
            top = bottom = y[0];

            for (int i = 1; i < x.Length; i++)
            {
                left = Math.Min(left, x[i]);
                right = Math.Max(right, x[i]);
                top = Math.Min(top, y[i]);
                bottom = Math.Max(bottom, y[i]);
            }
        }

        // A golden section search for the turn that brings the stroke closest to the template.
        private static double DistanceAtBestAngle(double[] x, double[] y, double cx, double cy, Template template)
        {
            var phi = 0.5 * (-1 + Math.Sqrt(5));
            var from = -AngleRange;
            var to = AngleRange;

            var x1 = phi * from + (1 - phi) * to;
            var f1 = DistanceAtAngle(x, y, cx, cy, template, x1);

            var x2 = (1 - phi) * from + phi * to;
            var f2 = DistanceAtAngle(x, y, cx, cy, template, x2);

            while (Math.Abs(to - from) > AnglePrecision)
            {
                if (f1 < f2)
                {
                    to = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = phi * from + (1 - phi) * to;
                    f1 = DistanceAtAngle(x, y, cx, cy, template, x1);
                }
                else
                {
                    from = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = (1 - phi) * from + phi * to;
                    f2 = DistanceAtAngle(x, y, cx, cy, template, x2);
                }
            }

            return Math.Min(f1, f2);
        }

        private static double DistanceAtAngle(double[] x, double[] y, double cx, double cy, Template template, double radians)
        {
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            var count = Math.Min(x.Length, template.X.Length);
            var distance = 0.0;

            for (int i = 0; i < count; i++)
            {
                var px = (x[i] - cx) * cos - (y[i] - cy) * sin + cx;
                var py = (x[i] - cx) * sin + (y[i] - cy) * cos + cy;

                distance += Distance(template.X[i], template.Y[i], px, py);
            }

            return distance / x.Length;
        }

        private static double Distance(double x1, double y1, double x2, double y2)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        // The first sharp turn, more than 18 degrees, from a quarter of the way in.
        private static int FindCorner(double[] x, double[] y)
        {
            for (int i = Math.Max(1, x.Length / 4); i < x.Length - 1; i++)
            {
                var p12 = Distance(x[i - 1], y[i - 1], x[i], y[i]);
                var p13 = Distance(x[i - 1], y[i - 1], x[i + 1], y[i + 1]);
                var p23 = Distance(x[i], y[i], x[i + 1], y[i + 1]);

                var angle = Math.Acos((p12 * p12 + p13 * p13 - p23 * p23) / (2 * p12 * p13)) / Math.PI * 180;
                if (angle > 18)
                {
                    return i;
                }
            }

            return -1;
        }

        private static RecognizedShape Construct(PaintShapeKind kind, double[] x, double[] y)
        {
            if (kind == PaintShapeKind.Arrow)
            {
                // Drawn from its start to its tip, then the head: the tip is the first sharp turn.
                var tip = FindCorner(x, y);
                if (tip <= 0)
                {
                    return null;
                }

                if (tip > 10)
                {
                    tip -= 2;
                }

                var start = new Vector2((float)x[0], (float)y[0]);
                var bend = new Vector2((float)x[tip / 2], (float)y[tip / 2]);
                var end = new Vector2((float)x[tip], (float)y[tip]);

                return new RecognizedShape
                {
                    Kind = kind,
                    Center = (Vector2.Min(start, Vector2.Min(bend, end)) + Vector2.Max(start, Vector2.Max(bend, end))) / 2,
                    ArrowStart = start,
                    ArrowBend = bend,
                    ArrowTip = end
                };
            }

            Bounds(x, y, out double left, out double top, out double right, out double bottom);

            var center = new Vector2((float)(left + right) / 2, (float)(top + bottom) / 2);
            var half = new Vector2((float)(right - left) / 2, (float)(bottom - top) / 2);
            var rotation = 0f;

            if (kind == PaintShapeKind.Circle)
            {
                half = new Vector2((half.X + half.Y) / 2);
            }
            else if (kind == PaintShapeKind.Star)
            {
                // Turned so that a point faces the stroke's farthest one, which is a tip.
                var farthest = 0;
                var reach = 0.0;

                for (int i = 0; i < x.Length; i++)
                {
                    var distance = Distance(center.X, center.Y, x[i], y[i]);
                    if (distance > reach)
                    {
                        reach = distance;
                        farthest = i;
                    }
                }

                half = new Vector2((float)reach);
                rotation = (float)(Math.Atan2(y[farthest] - center.Y, x[farthest] - center.X) + Math.PI / 2);
            }

            return new RecognizedShape
            {
                Kind = kind,
                Center = center,
                Size = half,
                Rotation = rotation
            };
        }
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

            // A click, or a move too short to make a curve, is a dot: Direct2D draws no caps on a
            // figure without segments, so it gets one, and a length, as a zero-length one draws
            // nothing either. A ten-thousandth of the canvas is under a pixel at any size drawn.
            if (_path.Count == 0)
            {
                if (point == _beginPoint)
                {
                    point += new Vector2(0.0001f, 0);
                }

                _path.Add(_beginPoint);
                _path.Add(point);
                _path.Add(point);
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
