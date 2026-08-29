//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Native.AI;

namespace Telegram.AI
{
    public static partial class RecognizedTextBoundingBoxSimplifier
    {
        public static List<List<Vector2>> Union<T>(IEnumerable<T> boxes, float tolerance, float padding) where T : IOcrObject
        {
            var polygons = GetUnionOfBoundingBoxes(boxes, padding);
            var vectors = new List<List<Vector2>>();

            foreach (var points in polygons)
            {
                if (points.Count < 3)
                    continue;

                bool[] keep = new bool[points.Count];
                keep[0] = true;
                keep[^1] = true;

                SimplifySection(points, 0, points.Count - 1, tolerance, keep);

                var result = new List<Vector2>();
                for (int i = 0; i < points.Count; i++)
                    if (keep[i])
                        result.Add(points[i]);

                vectors.Add(result);
            }

            return vectors;
        }

        private static List<List<Vector2>> GetUnionOfBoundingBoxes<T>(IEnumerable<T> boxes, float padding) where T : IOcrObject
        {
            var empty = true;

            using (var builder = new CanvasPathBuilder(null))
            {
                // The quads overlap and all wind the same way, so under the winding rule the
                // filled region is their union, and Outline traces exactly that boundary.
                builder.SetFilledRegionDetermination(CanvasFilledRegionDetermination.Winding);

                foreach (var box in boxes)
                {
                    var quad = box.BoundingBox.Inflate(padding);

                    builder.BeginFigure(quad.TopLeft);
                    builder.AddLine(quad.TopRight);
                    builder.AddLine(quad.BottomRight);
                    builder.AddLine(quad.BottomLeft);
                    builder.EndFigure(CanvasFigureLoop.Closed);

                    empty = false;
                }

                if (empty)
                {
                    return new List<List<Vector2>>();
                }

                var receiver = new PolygonReceiver();

                using (var quads = CanvasGeometry.CreatePath(builder))
                using (var outline = quads.Outline())
                {
                    outline.SendPathTo(receiver);
                }

                return receiver.Polygons;
            }
        }

        private partial class PolygonReceiver : ICanvasPathReceiver
        {
            public List<List<Vector2>> Polygons { get; } = new();

            private List<Vector2> _figure;

            public void BeginFigure(Vector2 startPoint, CanvasFigureFill figureFill)
            {
                _figure = new List<Vector2> { startPoint };
            }

            public void AddLine(Vector2 endPoint)
            {
                Add(endPoint);
            }

            // The outline of a polygonal path has no curves, but the interface requires these.
            public void AddArc(Vector2 endPoint, float radiusX, float radiusY, float rotationAngle, CanvasSweepDirection sweepDirection, CanvasArcSize arcSize) => Add(endPoint);
            public void AddCubicBezier(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 endPoint) => Add(endPoint);
            public void AddQuadraticBezier(Vector2 controlPoint, Vector2 endPoint) => Add(endPoint);

            public void SetFilledRegionDetermination(CanvasFilledRegionDetermination filledRegionDetermination) { }
            public void SetSegmentOptions(CanvasFigureSegmentOptions figureSegmentOptions) { }

            public void EndFigure(CanvasFigureLoop figureLoop)
            {
                if (_figure.Count > 1 && IsCoincident(_figure[0], _figure[^1]))
                {
                    _figure.RemoveAt(_figure.Count - 1);
                }

                if (_figure.Count > 2)
                {
                    Polygons.Add(_figure);
                }

                _figure = null;
            }

            // The rounding pass normalizes (curr - prev), so a repeated vertex would give it NaN.
            private void Add(Vector2 point)
            {
                if (!IsCoincident(_figure[^1], point))
                {
                    _figure.Add(point);
                }
            }

            private static bool IsCoincident(Vector2 a, Vector2 b)
            {
                return Vector2.DistanceSquared(a, b) < 0.0001f;
            }
        }

        private static void SimplifySection(List<Vector2> points, int start, int end, float tolerance, bool[] keep)
        {
            if (start + 1 >= end)
                return;

            float maxDistance = 0;
            int index = start;

            Vector2 a = points[start];
            Vector2 b = points[end];

            for (int i = start + 1; i < end; i++)
            {
                float dist = PerpendicularDistance(points[i], a, b);
                if (dist > maxDistance)
                {
                    maxDistance = dist;
                    index = i;
                }
            }

            if (maxDistance > tolerance)
            {
                keep[index] = true;
                SimplifySection(points, start, index, tolerance, keep);
                SimplifySection(points, index, end, tolerance, keep);
            }
        }

        private static float PerpendicularDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
        {
            float dx = lineEnd.X - lineStart.X;
            float dy = lineEnd.Y - lineStart.Y;

            if (dx == 0 && dy == 0)
                return Vector2.Distance(point, lineStart);

            float t = ((point.X - lineStart.X) * dx + (point.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);
            t = MathF.Max(0, MathF.Min(1, t));

            Vector2 projection = lineStart + t * new Vector2(dx, dy);
            return Vector2.Distance(point, projection);
        }
    }
}
