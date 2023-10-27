using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public class DashPath : FrameworkElement
    {
        private readonly ShapeVisual _visual;
        private int _count;

        public DashPath()
        {
            _visual = Window.Current.Compositor.CreateShapeVisual();
            ElementCompositionPreview.SetElementChildVisual(this, _visual);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Stripe1 != null && Stripe2 != null)
            {
                UpdateDoubleStripe(finalSize);
            }
            else if (Stripe1 != null && Stripe2 == null)
            {
                UpdateSingleStripe(finalSize);
            }
            else
            {
                _count = 0;
                _visual.Shapes.Clear();
            }

            return finalSize;
        }

        private void UpdateDoubleStripe(Size finalSize)
        {
            var h = 3.5f;
            var w = 3.5f;
            var y = 0f;

            var count = (int)Math.Ceiling(finalSize.Height / (h + w));
            if (count == _count || Stripe1 is not SolidColorBrush brush1 || Stripe2 is not SolidColorBrush brush2)
            {
                return;
            }

            CanvasGeometry result;
            using (var builder = new CanvasPathBuilder(null))
            {
                for (int i = 0; i <= count / 2; i++)
                {
                    builder.BeginFigure(w, y);
                    builder.AddLine(w, y + w + h);
                    builder.AddLine(0, y + w + h + w);
                    builder.AddLine(0, /*i == 0 ? y :*/ y + w);
                    builder.EndFigure(CanvasFigureLoop.Closed);

                    y += (w + h) * 3;
                }

                result = CanvasGeometry.CreatePath(builder);
            }

            var geometry = Window.Current.Compositor.CreatePathGeometry(new CompositionPath(result));

            var shape1 = Window.Current.Compositor.CreateSpriteShape(geometry);
            shape1.StrokeThickness = 0;
            shape1.FillBrush = Window.Current.Compositor.CreateColorBrush(brush1.Color);
            shape1.Offset = new Vector2(0, h * 4);

            var shape2 = Window.Current.Compositor.CreateSpriteShape(geometry);
            shape2.StrokeThickness = 0;
            shape2.FillBrush = Window.Current.Compositor.CreateColorBrush(brush2.Color);
            shape2.Offset = new Vector2(0, h * 2);

            _visual.Shapes.Clear();
            _visual.Shapes.Add(shape1);
            _visual.Shapes.Add(shape2);
            _visual.Size = finalSize.ToVector2();

            _count = count;
        }

        private void UpdateSingleStripe(Size finalSize)
        {
            var h = 4.5f;
            var w = 3.5f;
            var y = 3.5f;

            var count = (int)Math.Ceiling(finalSize.Height / (h + w));
            if (count == _count || Stripe1 is not SolidColorBrush brush)
            {
                return;
            }

            CanvasGeometry result;
            using (var builder = new CanvasPathBuilder(null))
            {
                for (int i = 0; i <= count; i++)
                {
                    builder.BeginFigure(w, y);
                    builder.AddLine(w, y + w + h);
                    builder.AddLine(0, y + w + h + w);
                    builder.AddLine(0, y + w);
                    builder.EndFigure(CanvasFigureLoop.Closed);

                    y += (w + h) * 2;
                }

                result = CanvasGeometry.CreatePath(builder);
            }

            var geometry = Window.Current.Compositor.CreatePathGeometry(new CompositionPath(result));
            var shape = Window.Current.Compositor.CreateSpriteShape(geometry);
            shape.StrokeThickness = 0;
            shape.FillBrush = Window.Current.Compositor.CreateColorBrush(brush.Color);

            _visual.Shapes.Clear();
            _visual.Shapes.Add(shape);
            _visual.Size = finalSize.ToVector2();

            _count = count;
        }

        #region Stripe1

        public Brush Stripe1
        {
            get { return (Brush)GetValue(Stripe1Property); }
            set { SetValue(Stripe1Property, value); }
        }

        public static readonly DependencyProperty Stripe1Property =
            DependencyProperty.Register("Stripe1", typeof(Brush), typeof(DashPath), new PropertyMetadata(null, OnStripe1Changed));

        private static void OnStripe1Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DashPath)d).OnStripe1Changed((Brush)e.NewValue);
        }

        private void OnStripe1Changed(Brush newValue)
        {
            if (_visual.Shapes.Count > 0 && _visual.Shapes[0] is CompositionSpriteShape shape && newValue is SolidColorBrush brush)
            {
                shape.FillBrush = shape.Compositor.CreateColorBrush(brush.Color);
            }
            else
            {
                InvalidateArrange();
            }
        }

        #endregion

        #region Stripe2

        public Brush Stripe2
        {
            get { return (Brush)GetValue(Stripe2Property); }
            set { SetValue(Stripe2Property, value); }
        }

        public static readonly DependencyProperty Stripe2Property =
            DependencyProperty.Register("Stripe2", typeof(Brush), typeof(DashPath), new PropertyMetadata(null, OnStripe2Changed));

        private static void OnStripe2Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((DashPath)d).OnStripe2Changed((Brush)e.NewValue);
        }

        private void OnStripe2Changed(Brush newValue)
        {
            if (_visual.Shapes.Count > 1 && _visual.Shapes[1] is CompositionSpriteShape shape && newValue is SolidColorBrush brush)
            {
                shape.FillBrush = shape.Compositor.CreateColorBrush(brush.Color);
            }
            else
            {
                InvalidateArrange();
            }
        }

        #endregion
    }
}
