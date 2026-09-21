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
        private const float StripeWidth = 3.5f;

        // Nothing is created until there is a stripe to draw: the link preview, game and
        // sponsored templates carry one of these unconditionally, and most of their messages
        // have no accent stripes at all.
        private ShapeVisual _visual;

        private CompositionSpriteShape _shape1;
        private CompositionSpriteShape _shape2;

        private CompositionColorBrush _brush1;
        private CompositionColorBrush _brush2;

        // What the geometry currently held by the shapes was built for. The two patterns run on
        // different periods, so the count alone does not identify one.
        private int _count;
        private bool _twin;

        protected override Size ArrangeOverride(Size finalSize)
        {
            // The second stripe is only ever set alongside the first, so the first one absent
            // means there is nothing to draw at all.
            if (_stripe1 == default)
            {
                Clear();
                return finalSize;
            }

            var twin = _stripe2 != default;

            // Period of the pattern: the single stripe is taller, so it needs fewer repeats.
            var count = (int)Math.Ceiling(finalSize.Height / (twin ? 7.0 : 8.0));

            var compositor = EnsureVisual(twin);
            _visual.Size = finalSize.ToVector2();

            if (count != _count || twin != _twin)
            {
                var geometry = twin
                    ? CreateTwinGeometry(compositor, count)
                    : CreateGeometry(compositor, count);

                _shape1.Geometry = geometry;
                _shape1.Offset = twin ? new Vector2(0, StripeWidth * 4) : Vector2.Zero;

                if (_shape2 != null)
                {
                    _shape2.Geometry = geometry;
                    _shape2.Offset = new Vector2(0, StripeWidth * 2);
                }

                _count = count;
                _twin = twin;
            }

            return finalSize;
        }

        private Compositor EnsureVisual(bool twin)
        {
            var compositor = BootStrapper.Current.Compositor;

            if (_visual == null)
            {
                _visual = compositor.CreateShapeVisual();
                ElementCompositionPreview.SetElementChildVisual(this, _visual);
            }

            if (_shape1 == null)
            {
                _brush1 = compositor.CreateColorBrush(_stripe1);

                _shape1 = compositor.CreateSpriteShape();
                _shape1.StrokeThickness = 0;
                _shape1.FillBrush = _brush1;

                _visual.Shapes.Add(_shape1);
            }

            if (twin && _shape2 == null)
            {
                _brush2 = compositor.CreateColorBrush(_stripe2);

                _shape2 = compositor.CreateSpriteShape();
                _shape2.StrokeThickness = 0;
                _shape2.FillBrush = _brush2;

                _visual.Shapes.Add(_shape2);
            }
            else if (!twin && _shape2 != null)
            {
                _visual.Shapes.Remove(_shape2);

                _shape2 = null;
                _brush2 = null;
            }

            return compositor;
        }

        private void Clear()
        {
            _visual?.Shapes.Clear();

            _shape1 = null;
            _shape2 = null;
            _brush1 = null;
            _brush2 = null;

            _count = 0;
        }

        private CompositionPathGeometry CreateTwinGeometry(Compositor compositor, int count)
        {
            var h = StripeWidth;
            var w = StripeWidth;
            var y = 0f;

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

            return compositor.CreatePathGeometry(new CompositionPath(result));
        }

        private CompositionPathGeometry CreateGeometry(Compositor compositor, int count)
        {
            var h = 4.5f;
            var w = StripeWidth;
            var y = 3.5f;

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

            return compositor.CreatePathGeometry(new CompositionPath(result));
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
                    // A stripe coming or going changes the pattern rather than its colour: one
                    // stripe and two are laid out on different periods.
                    var appearance = (_stripe1 == default) != (value == default);
                    _stripe1 = value;

                    if (appearance || _brush1 == null)
                    {
                        InvalidateArrange();
                    }
                    else
                    {
                        _brush1.Color = value;
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
                    var appearance = (_stripe2 == default) != (value == default);
                    _stripe2 = value;

                    if (appearance || _brush2 == null)
                    {
                        InvalidateArrange();
                    }
                    else
                    {
                        _brush2.Color = value;
                    }
                }
            }
        }

        #endregion
    }
}
