//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Clipper2Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Native.AI;

namespace Telegram.AI
{
    public static class RecognizedTextBoundingBoxSimplifier
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

        private static IEnumerable<List<Vector2>> GetUnionOfBoundingBoxes<T>(IEnumerable<T> boxes, float padding) where T : IOcrObject
        {
            var clipper = new Clipper64();
            var paths = boxes.Select(box => ConvertToClipperPath(box.BoundingBox.Inflate(padding))).ToList();

            clipper.AddSubject(new Paths64(paths));
            var solution = new Paths64();
            clipper.Execute(ClipType.Union, Clipper2Lib.FillRule.NonZero, solution);

            return solution.Select(path => path.Select(p => new Vector2((float)(p.X / 1000), (float)(p.Y / 1000))).Distinct().ToList());
        }

        private static Path64 ConvertToClipperPath(RecognizedTextBoundingBox box)
        {
            return new Path64
            {
                new Point64((long)(box.TopLeft.X * 1000), (long)(box.TopLeft.Y * 1000)),
                new Point64((long)(box.TopRight.X * 1000), (long)(box.TopRight.Y * 1000)),
                new Point64((long)(box.BottomRight.X * 1000), (long)(box.BottomRight.Y * 1000)),
                new Point64((long)(box.BottomLeft.X * 1000), (long)(box.BottomLeft.Y * 1000))
            };
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
