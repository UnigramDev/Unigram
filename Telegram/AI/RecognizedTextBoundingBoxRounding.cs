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

namespace Telegram.AI
{
    public static class RecognizedTextBoundingBoxRounding
    {
        public static CanvasGeometry CreateRoundedPolygons(IEnumerable<RecognizedTextBlock> boxes)
        {
            var geometries = new List<CanvasGeometry>();

            foreach (var box in boxes)
            {
                foreach (var polygon in box.Polygons)
                {
                    geometries.Add(RecognizedTextBoundingBoxRounding.CreateRoundedPolygon(polygon, box.Padding));
                }
            }

            CanvasGeometry result = geometries[0];

            for (int j = 1; j < geometries.Count; j++)
            {
                var newResult = result.CombineWith(geometries[j], Matrix3x2.Identity, CanvasGeometryCombine.Union);
                if (result != geometries[0]) // Don't dispose the first geometry yet
                    result.Dispose();
                result = newResult;
            }

            return result;
        }

        public static CanvasGeometry CreateRoundedPolygon(IList<Vector2> polygon, float radius)
        {
            using (var pathBuilder = new CanvasPathBuilder(null))
            {
                int n = polygon.Count;
                const float minRadius = 0.5f; // Minimum radius threshold

                // Calculate all rounded corner points first
                var cornerData = new List<(Vector2 p1, Vector2 p2, float r, bool useRounding)>();

                for (int i = 0; i < n; i++)
                {
                    Vector2 prev = polygon[(i - 1 + n) % n];
                    Vector2 curr = polygon[i];
                    Vector2 next = polygon[(i + 1) % n];

                    Vector2 v1 = Vector2.Normalize(curr - prev); // incoming edge
                    Vector2 v2 = Vector2.Normalize(next - curr); // outgoing edge

                    // Check if vectors are nearly parallel (very sharp or very obtuse angle)
                    float dot = Vector2.Dot(v1, v2);
                    bool isNearlyParallel = MathF.Abs(dot) > 0.98f; // ~11.5 degree threshold

                    if (isNearlyParallel)
                    {
                        // Skip rounding for nearly parallel edges
                        cornerData.Add((curr, curr, 0, false));
                        continue;
                    }

                    // Calculate available distances
                    float distPrev = Vector2.Distance(curr, prev);
                    float distNext = Vector2.Distance(curr, next);

                    // Clamp radius more conservatively
                    float maxRadius = MathF.Min(distPrev, distNext) * 0.4f; // Use 40% instead of 50%
                    float r = MathF.Min(radius, maxRadius);

                    if (r >= minRadius)
                    {
                        Vector2 p1 = curr - v1 * r;
                        Vector2 p2 = curr + v2 * r;
                        cornerData.Add((p1, p2, r, true));
                    }
                    else
                    {
                        cornerData.Add((curr, curr, 0, false));
                    }
                }

                // Build the path
                bool figureStarted = false;

                for (int i = 0; i < n; i++)
                {
                    var current = cornerData[i];
                    var next = cornerData[(i + 1) % n];

                    if (!figureStarted)
                    {
                        Vector2 startPoint = current.useRounding ? current.p1 : current.p1;
                        pathBuilder.BeginFigure(startPoint.X, startPoint.Y);
                        figureStarted = true;
                    }
                    else
                    {
                        Vector2 lineToPoint = current.useRounding ? current.p1 : current.p1;
                        pathBuilder.AddLine(lineToPoint.X, lineToPoint.Y);
                    }

                    if (current.useRounding)
                    {
                        // Calculate sweep direction
                        Vector2 prev = polygon[(i - 1 + n) % n];
                        Vector2 curr = polygon[i];
                        Vector2 nextVert = polygon[(i + 1) % n];

                        Vector2 v1 = Vector2.Normalize(curr - prev);
                        Vector2 v2 = Vector2.Normalize(nextVert - curr);

                        // Use cross product to determine sweep direction
                        float cross = v1.X * v2.Y - v1.Y * v2.X;
                        CanvasSweepDirection sweep = cross > 0 ? CanvasSweepDirection.Clockwise : CanvasSweepDirection.CounterClockwise;

                        pathBuilder.AddArc(current.p2, current.r, current.r, 0, sweep, CanvasArcSize.Small);
                    }
                }

                pathBuilder.EndFigure(CanvasFigureLoop.Closed);
                return CanvasGeometry.CreatePath(pathBuilder);
            }
        }
    }
}
