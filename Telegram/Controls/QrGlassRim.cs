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
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public enum QrGlassRimKind
    {
        // Small below 30 px of height, Big from there.
        Auto,
        Big,
        Small,
        Swatch
    }

    // The edge of a piece of glass: a white crescent just inside the top and another inside the bottom, and a
    // dark line just outside that is strongest on the sides. Placed over the glass and Inset larger on every
    // side, because it reaches past the shape and the shape's own rounded clip would cut it.
    public partial class QrGlassRim : Grid
    {
        private const float Inset = 3;

        private readonly ShapeVisual _shapes;
        private Vector2 _size;

        public QrGlassRim()
        {
            IsHitTestVisible = false;

            _shapes = BootStrapper.Current.Compositor.CreateShapeVisual();
            _shapes.RelativeSizeAdjustment = Vector2.One;

            ElementCompositionPreview.SetElementChildVisual(this, _shapes);
            SizeChanged += OnSizeChanged;
        }

        public QrGlassRimKind Kind { get; set; }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var size = e.NewSize.ToVector2();
            if (size == _size)
            {
                return;
            }

            _size = size;
            Build();
        }

        private void Build()
        {
            _shapes.Shapes.Clear();

            var width = _size.X - 2 * Inset;
            var height = _size.Y - 2 * Inset;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            var kind = Kind == QrGlassRimKind.Auto
                ? height < 30 ? QrGlassRimKind.Small : QrGlassRimKind.Big
                : Kind;

            var (top, topAlpha, bottom, bottomAlpha, line, sides, ends) = Values(kind);
            var compositor = _shapes.Compositor;

            // Everything sits on the pill grown by 1 px.
            var x = Inset - 1;
            var y = Inset - 1;
            var w = width + 2;
            var h = height + 2;
            var r = MathF.Min(height / 2 + 1, MathF.Min(w, h) / 2);

            using var shape = CanvasGeometry.CreateRoundedRectangle(null, x, y, w, h, r, r);

            AddCrescent(compositor, shape, top, topAlpha);
            AddCrescent(compositor, shape, -bottom, bottomAlpha);

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.StartPoint = new Vector2(0, 0.5f);
            gradient.EndPoint = new Vector2(1, 0.5f);

            AddDarkStops(compositor, gradient, sides, ends);

            var ring = compositor.CreateRoundedRectangleGeometry();
            ring.Offset = new Vector2(x - line / 2, y - line / 2);
            ring.Size = new Vector2(w + line, h + line);
            ring.CornerRadius = new Vector2(r + line / 2);

            var dark = compositor.CreateSpriteShape(ring);
            dark.StrokeBrush = gradient;
            dark.StrokeThickness = line;

            _shapes.Shapes.Add(dark);
        }

        // Top crescent, its opacity, bottom crescent, its opacity, dark line width, dark at the sides, and dark at
        // the top and bottom.
        private static (float, float, float, float, float, float, float) Values(QrGlassRimKind kind)
        {
            return kind switch
            {
                QrGlassRimKind.Small => (0.83f, 0.27f, 0.79f, 0.23f, 0.86f, 0.33f, 0.025f),
                QrGlassRimKind.Swatch => (0.83f, 0.13f, 0.79f, 0.11f, 0.86f, 0.16f, 0.0125f),
                _ => (1.1f, 0.27f, 1.05f, 0.23f, 1.15f, 0.33f, 0.025f)
            };
        }

        // The dark line runs right outside the shape. Its strength follows |cos θ| round the shape, which on a
        // circle is a function of x alone; a horizontal gradient sampled from it is exact there and close on the
        // pills.
        private static void AddDarkStops(Compositor compositor, CompositionGradientBrush gradient, float sides, float ends)
        {
            const float falloff = 3.8f;

            for (int i = 0; i <= 16; i++)
            {
                var offset = i / 16f;
                var u = MathF.Abs(offset * 2 - 1);
                var alpha = ends + (sides - ends) * MathF.Pow(u, falloff);

                gradient.ColorStops.Add(compositor.CreateColorGradientStop(offset, Color.FromArgb((byte)(255 * alpha), 0, 0, 0)));
            }
        }

        // A plain stroke just inside a shape that morphs, where a Border would draw its border: the rim for a
        // surface in the system's flyout colours rather than glass.
        public static ShapeVisual CreateOutline(Compositor compositor, CompositionRoundedRectangleGeometry shape, CompositionBrush brush, float thickness)
        {
            var shapes = compositor.CreateShapeVisual();
            shapes.RelativeSizeAdjustment = Vector2.One;

            var inner = compositor.CreateRoundedRectangleGeometry();
            Follow(inner, shape, -thickness / 2);

            var stroke = compositor.CreateSpriteShape(inner);
            stroke.StrokeBrush = brush;
            stroke.StrokeThickness = thickness;

            shapes.Shapes.Add(stroke);
            return shapes;
        }

        // Keeps target on source grown by d on every side (shrunk for a negative d).
        private static void Follow(CompositionRoundedRectangleGeometry target, CompositionRoundedRectangleGeometry source, float d)
        {
            var offset = target.Compositor.CreateExpressionAnimation("g.Offset - Vector2(d, d)");
            offset.SetReferenceParameter("g", source);
            offset.SetScalarParameter("d", d);

            var size = target.Compositor.CreateExpressionAnimation("g.Size + Vector2(2 * d, 2 * d)");
            size.SetReferenceParameter("g", source);
            size.SetScalarParameter("d", d);

            var radius = target.Compositor.CreateExpressionAnimation("Vector2(Max(g.CornerRadius.X + d, 0), Max(g.CornerRadius.Y + d, 0))");
            radius.SetReferenceParameter("g", source);
            radius.SetScalarParameter("d", d);

            target.StartAnimation("Offset", offset);
            target.StartAnimation("Size", size);
            target.StartAnimation("CornerRadius", radius);
        }

        // The shape minus itself moved by offset: positive leaves a sliver along the top, negative along the
        // bottom, thickest in the middle and gone at the sides.
        private void AddCrescent(Compositor compositor, CanvasGeometry shape, float offset, float alpha)
        {
            var crescent = shape.CombineWith(shape, Matrix3x2.CreateTranslation(0, offset), CanvasGeometryCombine.Exclude);

            var fill = compositor.CreateSpriteShape(compositor.CreatePathGeometry(new CompositionPath(crescent)));
            fill.FillBrush = compositor.CreateColorBrush(Color.FromArgb((byte)(255 * alpha), 0xFF, 0xFF, 0xFF));

            _shapes.Shapes.Add(fill);
        }
    }
}
