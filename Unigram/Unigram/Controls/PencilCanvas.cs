using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasControl))]
    public class PencilCanvas : Control
    {
        private static Color? ERASING_STROKE = null;
        private static float ERASING_STROKE_THICKNESS = 20;

        private CanvasRenderTarget _renderTarget;

        private ConcurrentDictionary<uint, SmoothPathBuilder> _builders;

        private List<SmoothPathBuilder> _drawing;

        private List<SmoothPathBuilder> _strokes = new List<SmoothPathBuilder>();
        private List<SmoothPathBuilder> _history = new List<SmoothPathBuilder>();

        private PencilCanvasMode _mode;

        private bool _needToCreateSizeDependentResources;
        private bool _needToRedrawInkSurface;

        private CanvasControl _canvas;

        public PencilCanvas()
        {
            DefaultStyleKey = typeof(PencilCanvas);
        }

        protected override void OnApplyTemplate()
        {
            _builders = new ConcurrentDictionary<uint, SmoothPathBuilder>();

            _canvas = (CanvasControl)GetTemplateChild("Canvas");

            _canvas.SizeChanged += OnSizeChanged;

            _canvas.CreateResources += OnCreateResources;
            _canvas.Draw += OnDraw;

            _canvas.PointerPressed += OnPointerPressed;
            _canvas.PointerMoved += OnPointerMoved;
            _canvas.PointerReleased += OnPointerReleased;

            base.OnApplyTemplate();
        }

        public ICanvasResourceCreatorWithDpi Creator => _canvas;

        public PencilCanvasMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        public event EventHandler StrokesChanged;

        public Color Stroke { get; set; }

        public float StrokeThickness { get; set; }

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

                if (_canvas != null)
                {
                    _canvas.Invalidate();
                }
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

            _needToRedrawInkSurface = true;
            _canvas.Invalidate();

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
            _canvas.Invalidate();

            StrokesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SaveState()
        {
            _drawing = _strokes.ToList();
        }

        public void RestoreState()
        {
            _strokes = _drawing?.ToList() ?? new List<SmoothPathBuilder>();
            _history.Clear();

            _canvas.Invalidate();
        }

        public void Invalidate()
        {
            _canvas?.Invalidate();
        }

        #region Resources

        private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            CreateSizeDependentResources();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _needToCreateSizeDependentResources = true;
            _canvas.Invalidate();
        }

        private void CreateSizeDependentResources()
        {
            _renderTarget = new CanvasRenderTarget(_canvas, _canvas.Size);

            _needToCreateSizeDependentResources = false;
            _needToRedrawInkSurface = true;
        }

        #endregion

        #region Drawing

        private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _canvas.CapturePointer(e.Pointer);

            var point = e.GetCurrentPoint(_canvas);
            var erasing = _mode == PencilCanvasMode.Eraser || point.Properties.IsEraser;

            _builders[point.PointerId] = new SmoothPathBuilder(point.Position.ToVector2() / _canvas.Size.ToVector2())
            {
                Stroke = erasing ? ERASING_STROKE : Stroke,
                StrokeThickness = erasing ? ERASING_STROKE_THICKNESS : StrokeThickness
            };
        }

        private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (_builders.TryGetValue(e.Pointer.PointerId, out SmoothPathBuilder _builder))
            {
                _builder.MoveTo(e.GetCurrentPoint(_canvas).Position.ToVector2() / _canvas.Size.ToVector2());
                _canvas.Invalidate();
            }
        }

        private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _canvas.ReleasePointerCapture(e.Pointer);

            if (_builders.TryRemove(e.Pointer.PointerId, out SmoothPathBuilder builder))
            {
                using (var session = _renderTarget.CreateDrawingSession())
                {
                    DrawPath(session, builder, _renderTarget.Size.ToVector2());
                }

                _strokes.Add(builder);
                _history.Clear();

                StrokesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_needToCreateSizeDependentResources)
            {
                CreateSizeDependentResources();
            }

            if (_needToRedrawInkSurface)
            {
                using (var canvas = _renderTarget.CreateDrawingSession())
                {
                    canvas.Clear(Colors.Transparent);

                    foreach (var builder in _strokes)
                    {
                        DrawPath(canvas, builder, _renderTarget.Size.ToVector2());
                    }
                }
            }

            args.DrawingSession.DrawImage(_renderTarget);

            foreach (var builder in _builders.Values)
            {
                DrawPath(args.DrawingSession, builder, sender.Size.ToVector2());
            }
        }

        public static void DrawPath(CanvasDrawingSession canvas, SmoothPathBuilder builder, Vector2 canvasSize)
        {
            var geometry = builder.ToGeometry(canvas, canvasSize);
            var style = new CanvasStrokeStyle();
            style.StartCap = CanvasCapStyle.Round;
            style.EndCap = CanvasCapStyle.Round;
            style.LineJoin = CanvasLineJoin.Round;

            canvas.Blend = builder.Stroke == null ? CanvasBlend.Copy : CanvasBlend.SourceOver;
            canvas.DrawGeometry(geometry, builder.Stroke ?? Colors.Transparent, builder.StrokeThickness, style);
        }

        #endregion
    }

    public enum PencilCanvasMode
    {
        Stroke,
        Eraser
    }

    public sealed class SmoothPathBuilder
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

        public Color? Stroke { get; set; }
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
            //var multiplier = NSMakePoint(imageSize.width / touch.canvasSize.width, imageSize.height / touch.canvasSize.height)
            var multiplier = canvasSize; //_imageSize / canvasSize;

            var builder = new CanvasPathBuilder(resourceCreator);
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
    }
}
