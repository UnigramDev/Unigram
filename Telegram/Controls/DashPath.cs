//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public partial class DashPath : FrameworkElement
    {
        private readonly ShapeVisual _visual;
        private int _count;

        public DashPath()
        {
            _visual = BootStrapper.Current.Compositor.CreateShapeVisual();
            ElementCompositionPreview.SetElementChildVisual(this, _visual);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Stripe1 != default && Stripe2 != default)
            {
                UpdateDoubleStripe(finalSize);
            }
            else if (Stripe1 != default && Stripe2 == default)
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
            if (count == _count)
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

            var geometry = BootStrapper.Current.Compositor.CreatePathGeometry(new CompositionPath(result));

            var shape1 = BootStrapper.Current.Compositor.CreateSpriteShape(geometry);
            shape1.StrokeThickness = 0;
            shape1.FillBrush = BootStrapper.Current.Compositor.CreateColorBrush(Stripe1);
            shape1.Offset = new Vector2(0, h * 4);

            var shape2 = BootStrapper.Current.Compositor.CreateSpriteShape(geometry);
            shape2.StrokeThickness = 0;
            shape2.FillBrush = BootStrapper.Current.Compositor.CreateColorBrush(Stripe2);
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
            if (count == _count)
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

            var geometry = BootStrapper.Current.Compositor.CreatePathGeometry(new CompositionPath(result));
            var shape = BootStrapper.Current.Compositor.CreateSpriteShape(geometry);
            shape.StrokeThickness = 0;
            shape.FillBrush = BootStrapper.Current.Compositor.CreateColorBrush(Stripe1);

            _visual.Shapes.Clear();
            _visual.Shapes.Add(shape);
            _visual.Size = finalSize.ToVector2();

            _count = count;
        }

        #region Stripe1

        private Color _stripe1;
        public Color Stripe1
        {
            get => _stripe1;
            set
            {
                if (_stripe1 != value)
                {
                    _stripe1 = value;

                    if (_visual.Shapes.Count > 0 && _visual.Shapes[0] is CompositionSpriteShape shape)
                    {
                        shape.FillBrush = shape.Compositor.CreateColorBrush(value);
                    }
                    else
                    {
                        InvalidateArrange();
                    }
                }
            }
        }

        #endregion

        #region Stripe2

        private Color _stripe2;
        public Color Stripe2
        {
            get => _stripe2;
            set
            {
                if (_stripe2 != value)
                {
                    _stripe2 = value;

                    if (_visual.Shapes.Count > 1 && _visual.Shapes[1] is CompositionSpriteShape shape)
                    {
                        shape.FillBrush = shape.Compositor.CreateColorBrush(value);
                    }
                    else
                    {
                        InvalidateArrange();
                    }
                }
            }
        }

        #endregion
    }
}
