//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Native.AI;

namespace Telegram.AI
{
    public static class RecognizedTextBoundingBoxHelper
    {
        public static RecognizedTextBoundingBox Inflate(this RecognizedTextBoundingBox box, float amount)
        {
            float horizontalAmount = amount;
            float verticalAmount = amount;

            // Calculate edge vectors (assuming points are in order: top-left, top-right, bottom-right, bottom-left)
            Vector2 p1 = box.TopLeft;
            Vector2 p2 = box.TopRight;
            Vector2 p3 = box.BottomRight;
            Vector2 p4 = box.BottomLeft;

            // Calculate the primary axes of the quadrilateral
            Vector2 horizontalAxis = Vector2.Normalize(p2 - p1 + p3 - p4); // Average of top and bottom edges
            Vector2 verticalAxis = Vector2.Normalize(p4 - p1 + p3 - p2);   // Average of left and right edges

            // Create offset vectors
            Vector2 horizontalOffset = horizontalAxis * horizontalAmount;
            Vector2 verticalOffset = verticalAxis * verticalAmount;

            // Apply offsets based on position relative to center
            Vector2 center = (p1 + p2 + p3 + p4) * 0.25f;

            RecognizedTextBoundingBox inflated = new();

            // For each point, determine its position relative to center and apply appropriate offsets
            Vector2 newP1 = p1;
            if (Vector2.Dot(p1 - center, horizontalAxis) < 0) newP1 -= horizontalOffset; else newP1 += horizontalOffset;
            if (Vector2.Dot(p1 - center, verticalAxis) < 0) newP1 -= verticalOffset; else newP1 += verticalOffset;

            Vector2 newP2 = p2;
            if (Vector2.Dot(p2 - center, horizontalAxis) < 0) newP2 -= horizontalOffset; else newP2 += horizontalOffset;
            if (Vector2.Dot(p2 - center, verticalAxis) < 0) newP2 -= verticalOffset; else newP2 += verticalOffset;

            Vector2 newP3 = p3;
            if (Vector2.Dot(p3 - center, horizontalAxis) < 0) newP3 -= horizontalOffset; else newP3 += horizontalOffset;
            if (Vector2.Dot(p3 - center, verticalAxis) < 0) newP3 -= verticalOffset; else newP3 += verticalOffset;

            Vector2 newP4 = p4;
            if (Vector2.Dot(p4 - center, horizontalAxis) < 0) newP4 -= horizontalOffset; else newP4 += horizontalOffset;
            if (Vector2.Dot(p4 - center, verticalAxis) < 0) newP4 -= verticalOffset; else newP4 += verticalOffset;

            inflated.TopLeft = newP1;
            inflated.TopRight = newP2;
            inflated.BottomRight = newP3;
            inflated.BottomLeft = newP4;

            return inflated;
        }

        public static float Width(this RecognizedTextBoundingBox box)
        {
            float topWidth = Vector2.Distance(box.TopLeft, box.TopRight);
            float bottomWidth = Vector2.Distance(box.BottomLeft, box.BottomRight);
            return (topWidth + bottomWidth) / 2f;
        }

        public static float Height(this RecognizedTextBoundingBox box)
        {
            float leftHeight = Vector2.Distance(box.TopLeft, box.BottomLeft);
            float rightHeight = Vector2.Distance(box.TopRight, box.BottomRight);
            return (leftHeight + rightHeight) / 2f;
        }

        public static bool ContainsPoint(this IList<Vector2> pts, Vector2 point)
        {
            int windingNumber = 0;

            for (int i = 0; i < pts.Count; i++)
            {
                var p1 = pts[i];
                var p2 = pts[(i + 1) % pts.Count];

                if (p1.Y <= point.Y)
                {
                    if (p2.Y > point.Y && IsLeft(p1, p2, point) > 0)
                        windingNumber++;
                }
                else
                {
                    if (p2.Y <= point.Y && IsLeft(p1, p2, point) < 0)
                        windingNumber--;
                }
            }

            return windingNumber != 0;
        }

        private static float IsLeft((float X, float Y) a, (float X, float Y) b, (float X, float Y) c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
        }

        private static float IsLeft(Vector2 a, Vector2 b, Vector2 c)
        {
            return (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
        }

        public static RecognizedTextBoundingBox Scale(this RecognizedTextBoundingBox box, Vector2 scale)
        {
            return new RecognizedTextBoundingBox
            {
                TopLeft = box.TopLeft * scale,
                TopRight = box.TopRight * scale,
                BottomRight = box.BottomRight * scale,
                BottomLeft = box.BottomLeft * scale
            };
        }

        public static RecognizedTextBoundingBox Compute(IEnumerable<RecognizedTextBoundingBox> boxes)
        {
            var points = new List<Vector2>();
            foreach (var bb in boxes)
            {
                points.Add(bb.TopLeft);
                points.Add(bb.TopRight);
                points.Add(bb.BottomRight);
                points.Add(bb.BottomLeft);
            }

            if (points.Count == 4)
            {
                return new RecognizedTextBoundingBox
                {
                    TopLeft = points[0],
                    TopRight = points[1],
                    BottomRight = points[2],
                    BottomLeft = points[3],
                };
            }

            var hull = ConvexHull(points);

            float minArea = float.MaxValue;
            Vector2[] bestRect = new Vector2[4];

            for (int i = 0; i < hull.Length; i++)
            {
                Vector2 p1 = hull[i];
                Vector2 p2 = hull[(i + 1) % hull.Length];

                float angle = (float)Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);

                var rotated = hull.Select(p => RotatePoint(p, -angle)).ToList();

                float minX = rotated.Min(p => p.X);
                float maxX = rotated.Max(p => p.X);
                float minY = rotated.Min(p => p.Y);
                float maxY = rotated.Max(p => p.Y);

                float area = (maxX - minX) * (maxY - minY);
                if (area < minArea)
                {
                    minArea = area;

                    Vector2 r1 = new(minX, minY);
                    Vector2 r2 = new(maxX, minY);
                    Vector2 r3 = new(maxX, maxY);
                    Vector2 r4 = new(minX, maxY);

                    bestRect[0] = RotatePoint(r1, angle);
                    bestRect[1] = RotatePoint(r2, angle);
                    bestRect[2] = RotatePoint(r3, angle);
                    bestRect[3] = RotatePoint(r4, angle);

                    // Reorder corners consistently
                    bestRect = OrderRectangleCorners(bestRect);
                }
            }

            return new RecognizedTextBoundingBox
            {
                TopLeft = bestRect[3],
                TopRight = bestRect[0],
                BottomRight = bestRect[1],
                BottomLeft = bestRect[2],
            };
        }

        public static Vector2 RotatePoint(Vector2 p, float angle)
        {
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            return new Vector2(
                p.X * cos - p.Y * sin,
                p.X * sin + p.Y * cos
            );
        }

        public static Vector2 ComputeCentroid(Vector2[] points)
        {
            float cx = points.Average(p => p.X);
            float cy = points.Average(p => p.Y);
            return new Vector2(cx, cy);
        }

        private static Vector2[] OrderRectangleCorners(Vector2[] rect)
        {
            var center = ComputeCentroid(rect);

            // Compute angles relative to centroid
            var cornersWithAngle = rect.Select(p =>
            {
                float angle = (float)Math.Atan2(p.Y - center.Y, p.X - center.X);
                return new { Point = p, Angle = angle };
            }).ToList();

            // Sort clockwise starting from top-left
            var ordered = cornersWithAngle
                .OrderBy(p =>
                {
                    // Adjust angle so that top-left (~-135 deg) comes first
                    float a = p.Angle;
                    if (a < -Math.PI / 2) a += 2 * (float)Math.PI;
                    return a;
                })
                .Select(p => p.Point)
                .ToArray();

            return ordered;
        }

        // Optional: If you still want a convex hull fallback for complex shapes.
        public static Vector2[] ConvexHull(IList<Vector2> pts)
        {
            var points = pts.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            if (points.Count <= 1) return points.ToArray();

            List<Vector2> lower = new();
            foreach (var p in points)
            {
                while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0) lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }

            List<Vector2> upper = new();
            for (int i = points.Count - 1; i >= 0; i--)
            {
                var p = points[i];
                while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0) upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower.ToArray();

            static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
                (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        }
    }
}
