//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Linq;
using System.Numerics;
using Telegram.AI;
using Telegram.Native.AI;
using Telegram.Navigation;
using Windows.Devices.Input;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    public partial class ImageTextSelection : Control
    {
        private static readonly CoreCursor _defaultCursor = new(CoreCursorType.Arrow, 1);
        private static readonly CoreCursor _selectCursor = new(CoreCursorType.IBeam, 1);

        private ulong _expandSelectionDeadline;

        private bool _selectionPressed;
        private Point _selectionStartPoint;

        private bool _templateApplied;

        private RecognizedTextSelectionManager _selection;

        private PathGeometry _geometry;

        private Path Highlight;
        private Border Overlay;

        public ImageTextSelection()
        {
            DefaultStyleKey = typeof(ImageTextSelection);

            SizeChanged += OnSizeChanged;

            DoubleTapped += OnDoubleTapped;
            Tapped += OnTapped;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ImageTextSelectionAutomationPeer(this);
        }

        protected override void OnApplyTemplate()
        {
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Highlight = GetTemplateChild(nameof(Highlight)) as Path;
            Highlight.Data = _geometry = new PathGeometry
            {
                FillRule = FillRule.Nonzero
            };

            _templateApplied = true;

            if (_recognizedText != null)
            {
                UpdateRecognizedText(_recognizedText, _imageSize);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateOverlay();
            UpdateTextSelection(_selection?.Selection);

            if (_showSkeleton)
            {
                ShowSkeleton();
            }
        }

        private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (_selection != null && e.PointerDeviceType == PointerDeviceType.Mouse)
            {
                var point = e.GetPosition(this);
                if (_selection.IsPointWithinText(point))
                {
                    _expandSelectionDeadline = Logger.TickCount + BootStrapper.Current.UISettings.DoubleClickTime;
                    _selection.ExpandSelection(point, true);
                }
            }
        }

        private void OnTapped(object sender, TappedRoutedEventArgs e)
        {
            // If a double tap is followed by a single tap, then it's a triple tap (duh)
            if (_selection != null && e.PointerDeviceType == PointerDeviceType.Mouse && Logger.TickCount < _expandSelectionDeadline)
            {
                var point = e.GetPosition(this);
                if (_selection.IsPointWithinText(point))
                {
                    _expandSelectionDeadline = Logger.TickCount + BootStrapper.Current.UISettings.DoubleClickTime;
                    _selection.ExpandSelection(point, false);
                }
            }
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed || _selection == null)
            {
                return;
            }

            e.Handled = true;

            _selectionStartPoint = e.GetCurrentPoint(this).Position;
            _selectionPressed = _selection.IsPointWithinText(_selectionStartPoint);

            if (_selectionPressed)
            {
                CapturePointer(e.Pointer);
                UpdateTextSelection(e);

                Focus(FocusState.Pointer);
            }
            else
            {
                _selection.ClearSelection();
            }
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            e.Handled = true;

            if (_selection != null)
            {
                UpdateTextSelection(e);
            }
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            e.Handled = true;

            _selectionPressed = false;
            ReleasePointerCapture(e.Pointer);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            e.Handled = true;

            if (_selectionPressed)
            {
                Window.Current.CoreWindow.PointerCursor = _selectCursor;
            }
            else
            {
                Window.Current.CoreWindow.PointerCursor = _defaultCursor;
            }
        }

        private void UpdateTextSelection(PointerRoutedEventArgs e)
        {
            var startPoint = _selectionStartPoint;
            var endPoint = e.GetCurrentPoint(this).Position;

            if (_selectionPressed && Logger.TickCount > _expandSelectionDeadline)
            {
                _selection.SelectTextBetween(startPoint, endPoint);
            }

            if (_selectionPressed || _selection.IsPointWithinText(endPoint))
            {
                Window.Current.CoreWindow.PointerCursor = _selectCursor;
            }
            else
            {
                Window.Current.CoreWindow.PointerCursor = _defaultCursor;
            }
        }

        private void OnSelectionChanged(object sender, RecognizedTextSelectionChangedEventArgs e)
        {
            UpdateTextSelection(e.NewSelection);

            SelectedText = e.NewSelection?.Text ?? string.Empty;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateTextSelection(RecognizedTextSelection selection)
        {
            _geometry.Figures.Clear();

            if (selection == null)
            {
                return;
            }

            foreach (var boundingBox in selection.BoundingBoxes)
            {
                var bb = boundingBox.Scale(_selection.Scale);

                var figure = new PathFigure();
                figure.StartPoint = bb.TopLeft.ToPoint();
                figure.Segments.Add(new LineSegment { Point = bb.TopRight.ToPoint() });
                figure.Segments.Add(new LineSegment { Point = bb.BottomRight.ToPoint() });
                figure.Segments.Add(new LineSegment { Point = bb.BottomLeft.ToPoint() });

                _geometry.Figures.Add(figure);
            }
        }

        private RecognizedText _recognizedText;
        public RecognizedText RecognizedText
        {
            get => _recognizedText;
            set => UpdateRecognizedText(_recognizedText = value, _imageSize);
        }

        private Vector2 _imageSize;
        public Vector2 ImageSize
        {
            get => _imageSize;
            set => UpdateRecognizedText(_recognizedText, _imageSize = value);
        }

        public string SelectedText { get; private set; } = string.Empty;

        public string Text { get; private set; } = string.Empty;

        public void SelectAll()
        {
            Focus(FocusState.Pointer);

            _selection?.SelectAll();
        }

        public void ClearSelection()
        {
            _selection?.ClearSelection();
        }

        public event EventHandler SelectionChanged;

        private void UpdateRecognizedText(RecognizedText result, Vector2 imageSize)
        {
            if (!_templateApplied || result == null || imageSize == Vector2.Zero)
            {
                return;
            }

            HideSkeleton();

            _geometry.Figures.Clear();

            if (_selection != null)
            {
                _selection.SelectionChanged -= OnSelectionChanged;
            }

            _selection = new RecognizedTextSelectionManager(result);
            _selection.SelectionChanged += OnSelectionChanged;

            SelectedText = string.Empty;
            Text = string.Join('\n', _selection.Blocks.Select(x => string.Join('\n', x.Lines.Select(x => x.Text))));

            UpdateOverlay();
        }

        private void UpdateOverlay()
        {
            if (_selection == null || ActualSize.X == 0 || ActualSize.Y == 0)
            {
                return;
            }

            _selection.Scale = ActualSize / _imageSize;

            var compositor = Window.Current.Compositor;

            var rectangle = CanvasGeometry.CreateRectangle(null, 0, 0, ActualSize.X, ActualSize.Y);
            var geometries = RecognizedTextBoundingBoxRounding.CreateRoundedPolygons(_selection.Blocks);

            geometries = geometries.Transform(Matrix3x2.CreateScale(ActualSize / _imageSize));

            var result = rectangle.CombineWith(geometries, Matrix3x2.Identity, CanvasGeometryCombine.Exclude);
            var path = compositor.CreatePathGeometry(new CompositionPath(result));

            var visual = ElementCompositionPreview.GetElementVisual(Overlay);
            visual.Clip = compositor.CreateGeometricClip(path);

            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0, 0);
            animation.InsertKeyFrame(1, 1);

            visual.StartAnimation("Opacity", animation);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);

            _selection?.ClearSelection();
        }

        #region Skeleton

        private bool _showSkeleton;

        public void ShowSkeleton()
        {
            var compositor = BootStrapper.Current.Compositor;
            var rectangle = compositor.CreateRectangleGeometry();
            rectangle.Size = ActualSize;

            var strokeColor = /*Background is SolidColorBrush brush ? brush.Color :*/ Colors.White;

            var stroke = compositor.CreateLinearGradientBrush();
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, Color.FromArgb(0x00, strokeColor.R, strokeColor.G, strokeColor.B)));
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(0x77, strokeColor.R, strokeColor.G, strokeColor.B)));
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, Color.FromArgb(0x00, strokeColor.R, strokeColor.G, strokeColor.B)));

            var fill = compositor.CreateLinearGradientBrush();
            fill.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, Color.FromArgb(0x00, 0xff, 0xff, 0xff)));
            fill.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(0x77, 0xff, 0xff, 0xff)));
            fill.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, Color.FromArgb(0x00, 0xff, 0xff, 0xff)));

            var shape = compositor.CreateSpriteShape();
            shape.Geometry = rectangle;
            shape.FillBrush = fill;
            shape.StrokeBrush = stroke;
            shape.StrokeThickness = 2;

            var shapeVisual = compositor.CreateShapeVisual();
            shapeVisual.Size = new Vector2(ActualSize.X, ActualSize.Y);
            shapeVisual.Shapes.Add(shape);

            var endless = compositor.CreateScalarKeyFrameAnimation();
            endless.InsertKeyFrame(0, -ActualSize.X);
            endless.InsertKeyFrame(1, +ActualSize.X);
            endless.IterationBehavior = AnimationIterationBehavior.Forever;
            endless.Duration = TimeSpan.FromMilliseconds(2000);

            stroke.StartAnimation("Offset.X", endless);
            fill.StartAnimation("Offset.X", endless);

            _showSkeleton = true;
            ElementCompositionPreview.SetElementChildVisual(this, shapeVisual);

            if (Overlay != null)
            {
                var visual = ElementCompositionPreview.GetElementVisual(Overlay);
                visual.Clip = null;
            }
        }

        public void HideSkeleton()
        {
            _showSkeleton = false;
            ElementCompositionPreview.SetElementChildVisual(this, BootStrapper.Current.Compositor.CreateSpriteVisual());
        }

        #endregion

    }

    public partial class ImageTextSelectionAutomationPeer : FrameworkElementAutomationPeer
    {
        private readonly ImageTextSelection _owner;

        public ImageTextSelectionAutomationPeer(ImageTextSelection owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetClassNameCore()
        {
            return nameof(TextBlock);
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Text;
        }

        protected override string GetNameCore()
        {
            return _owner.Text;
        }
    }
}
