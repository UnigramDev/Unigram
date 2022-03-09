using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Cells
{
    public sealed partial class ChatShareCell : Grid, IMultipleElement
    {
        public ChatShareCell()
        {
            InitializeComponent();
            InitializeSelection();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Stroke is SolidColorBrush stroke && _strokeToken == 0)
            {
                _strokeToken = stroke.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnStrokeChanged);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (Stroke is SolidColorBrush stroke && _strokeToken != 0)
            {
                stroke.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _strokeToken);
                _strokeToken = 0;
            }
        }

        public ProfilePicture Photo => PhotoElement;

        #region Stroke

        private long _strokeToken;

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ChatShareCell), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatShareCell)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            if (oldValue != null && _strokeToken != 0)
            {
                oldValue.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _strokeToken);
                _strokeToken = 0;
            }

            if (newValue == null || _ellipse == null)
            {
                return;
            }

            _ellipse.FillBrush = Window.Current.Compositor.CreateColorBrush(newValue.Color);
            _strokeToken = newValue.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnStrokeChanged);
        }

        private void OnStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || _ellipse == null)
            {
                return;
            }

            _ellipse.FillBrush = Window.Current.Compositor.CreateColorBrush(solid.Color);
        }

        #endregion

        #region SelectionStroke

        public SolidColorBrush SelectionStroke
        {
            get => (SolidColorBrush)GetValue(SelectionStrokeProperty);
            set => SetValue(SelectionStrokeProperty, value);
        }

        public static readonly DependencyProperty SelectionStrokeProperty =
            DependencyProperty.Register("SelectionStroke", typeof(SolidColorBrush), typeof(ChatShareCell), new PropertyMetadata(default(Color), OnSelectionStrokeChanged));

        private static void OnSelectionStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ChatShareCell;
            var solid = e.NewValue as SolidColorBrush;

            if (solid == null || sender._stroke == null)
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(solid.Color);

            sender._stroke.FillBrush = brush;
        }

        #endregion

        #region Selection Animation

        private Visual _selectionOutline;
        private Visual _selectionPhoto;

        private CompositionPathGeometry _polygon;
        private CompositionSpriteShape _ellipse;
        private CompositionSpriteShape _stroke;
        private ShapeVisual _visual;

        private void InitializeSelection()
        {
            static CompositionPath GetCheckMark()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    //builder.BeginFigure(new Vector2(3.821f, 7.819f));
                    //builder.AddLine(new Vector2(6.503f, 10.501f));
                    //builder.AddLine(new Vector2(12.153f, 4.832f));
                    builder.BeginFigure(new Vector2(5.821f, 9.819f));
                    builder.AddLine(new Vector2(7.503f, 12.501f));
                    builder.AddLine(new Vector2(14.153f, 6.832f));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return new CompositionPath(result);
            }

            var compositor = Window.Current.Compositor;
            //12.711,5.352 11.648,4.289 6.5,9.438 4.352,7.289 3.289,8.352 6.5,11.563

            //if (ApiInfo.CanUseDirectComposition)
            {
                var polygon = compositor.CreatePathGeometry();
                polygon.Path = GetCheckMark();

                var shape1 = compositor.CreateSpriteShape();
                shape1.Geometry = polygon;
                shape1.StrokeThickness = 1.5f;
                shape1.StrokeBrush = compositor.CreateColorBrush(Colors.White);

                var ellipse = compositor.CreateEllipseGeometry();
                ellipse.Radius = new Vector2(8);
                ellipse.Center = new Vector2(10);

                var shape2 = compositor.CreateSpriteShape();
                shape2.Geometry = ellipse;
                shape2.FillBrush = compositor.CreateColorBrush(Colors.Black);

                var outer = compositor.CreateEllipseGeometry();
                outer.Radius = new Vector2(10);
                outer.Center = new Vector2(10);

                var shape3 = compositor.CreateSpriteShape();
                shape3.Geometry = outer;
                shape3.FillBrush = compositor.CreateColorBrush(Colors.White);

                var visual = compositor.CreateShapeVisual();
                visual.Shapes.Add(shape3);
                visual.Shapes.Add(shape2);
                visual.Shapes.Add(shape1);
                visual.Size = new Vector2(20, 20);
                visual.Offset = new Vector3(36 - 17, 36 - 17, 0);
                visual.CenterPoint = new Vector3(8);
                visual.Scale = new Vector3(0);

                ElementCompositionPreview.SetElementChildVisual(PhotoPanel, visual);

                _polygon = polygon;
                _ellipse = shape2;
                _stroke = shape3;
                _visual = visual;
            }

            _selectionPhoto = ElementCompositionPreview.GetElementVisual(PhotoElement);
            _selectionOutline = ElementCompositionPreview.GetElementVisual(SelectionOutline);
            _selectionPhoto.CenterPoint = new Vector3(18);
            _selectionOutline.CenterPoint = new Vector3(18);
            _selectionOutline.Opacity = 0;
        }

        public void UpdateState(bool selected, bool animate)
        {
            if (animate)
            {
                var compositor = Window.Current.Compositor;

                var anim3 = compositor.CreateScalarKeyFrameAnimation();
                anim3.InsertKeyFrame(selected ? 0 : 1, 0);
                anim3.InsertKeyFrame(selected ? 1 : 0, 1);

                if (_visual != null)
                {
                    var anim1 = compositor.CreateScalarKeyFrameAnimation();
                    anim1.InsertKeyFrame(selected ? 0 : 1, 0);
                    anim1.InsertKeyFrame(selected ? 1 : 0, 1);
                    anim1.DelayTime = TimeSpan.FromMilliseconds(anim1.Duration.TotalMilliseconds / 2);
                    anim1.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                    var anim2 = compositor.CreateVector3KeyFrameAnimation();
                    anim2.InsertKeyFrame(selected ? 0 : 1, new Vector3(0));
                    anim2.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));

                    _polygon.StartAnimation("TrimEnd", anim1);
                    _visual.StartAnimation("Scale", anim2);
                    _visual.StartAnimation("Opacity", anim3);
                }


                var anim4 = compositor.CreateVector3KeyFrameAnimation();
                anim4.InsertKeyFrame(selected ? 0 : 1, new Vector3(1));
                anim4.InsertKeyFrame(selected ? 1 : 0, new Vector3(28f / 36f));

                var anim5 = compositor.CreateVector3KeyFrameAnimation();
                anim5.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));
                anim5.InsertKeyFrame(selected ? 0 : 1, new Vector3(28f / 36f));

                _selectionPhoto.StartAnimation("Scale", anim4);
                _selectionOutline.StartAnimation("Scale", anim5);
                _selectionOutline.StartAnimation("Opacity", anim3);
            }
            else
            {
                if (_visual != null)
                {
                    _polygon.TrimEnd = selected ? 1 : 0;
                    _visual.Scale = new Vector3(selected ? 1 : 0);
                    _visual.Opacity = selected ? 1 : 0;
                }

                _selectionPhoto.Scale = new Vector3(selected ? 28f / 36f : 1);
                _selectionOutline.Scale = new Vector3(selected ? 1 : 28f / 36f);
                _selectionOutline.Opacity = selected ? 1 : 0;
            }
        }

        #endregion
    }
}
