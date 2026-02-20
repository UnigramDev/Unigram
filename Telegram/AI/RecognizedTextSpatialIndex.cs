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
    public class RecognizedTextSpatialIndex
    {
        private readonly RecognizedTextSpatialGrid _spatialGrid;

        public RecognizedTextSpatialIndex(IList<RecognizedTextBlock> blocks)
        {
            _spatialGrid = new RecognizedTextSpatialGrid(blocks);
        }

        public RecognizedTextPointer FindNearestWord(Vector2 point)
        {
            var candidates = _spatialGrid.GetCandidatesNear(point, 0);
            var containingWords = candidates.Where(w => IsPointInBoundingBox(point, w.BoundingBox))
                                           .ToList();

            if (containingWords.Any())
            {
                return containingWords.OrderBy(w => CalculateBoundingBoxArea(w.BoundingBox)).First();
            }

            float[] searchRadii = { 10f, 25f, 50f, 100f, 200f };

            foreach (var radius in searchRadii)
            {
                candidates = _spatialGrid.GetCandidatesNear(point, radius);

                if (candidates.Any())
                {
                    var closest = candidates
                        .Select(w => new
                        {
                            Word = w,
                            Distance = CalculateDistanceToBoundingBox(point, w.BoundingBox)
                        })
                        .Where(x => x.Distance <= radius)
                        .OrderBy(x => x.Distance)
                        .FirstOrDefault();

                    if (closest != null)
                    {
                        return closest.Word;
                    }
                }
            }

            return RecognizedTextPointer.Empty;
        }

        private bool IsPointInBoundingBox(Vector2 point, RecognizedTextBoundingBox quad)
        {
            var points = new[] { quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft };
            return points.ContainsPoint(point);
        }

        private float CalculateBoundingBoxArea(RecognizedTextBoundingBox quad)
        {
            var points = new[] { quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft };

            float area = 0;
            for (int i = 0; i < points.Length; i++)
            {
                var j = (i + 1) % points.Length;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }

            return Math.Abs(area) / 2.0f;
        }

        private float CalculateDistanceToBoundingBox(Vector2 point, RecognizedTextBoundingBox quad)
        {
            if (IsPointInBoundingBox(point, quad))
                return 0;

            var points = new[] { quad.TopLeft, quad.TopRight, quad.BottomRight, quad.BottomLeft };
            float minDistance = float.MaxValue;

            for (int i = 0; i < points.Length; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Length];

                float distance = DistancePointToSegment(point.X, point.Y, p1.X, p1.Y, p2.X, p2.Y);
                minDistance = Math.Min(minDistance, distance);
            }

            return minDistance;
        }

        private float DistancePointToSegment(float px, float py, float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;

            if (dx == 0 && dy == 0)
                return (float)Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));

            float t = ((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy);
            t = Math.Max(0, Math.Min(1, t));

            float projX = x1 + t * dx;
            float projY = y1 + t * dy;

            return (float)Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
        }
    }

    public class RecognizedTextSpatialGrid
    {
        private readonly Dictionary<(int x, int y), List<RecognizedTextPointer>> _grid;
        private readonly float _cellSize;
        private readonly AxisAlignedBounds _worldBounds;

        public RecognizedTextSpatialGrid(IList<RecognizedTextBlock> blocks, float? cellSizeOverride = null)
        {
            _grid = new Dictionary<(int, int), List<RecognizedTextPointer>>();

            var allBounds = blocks.SelectMany(b => b.Lines)
                                 .SelectMany(l => l.Words)
                                 .Select(w => AxisAlignedBounds.FromBoundingBox(w.BoundingBox))
                                 .ToList();

            if (allBounds.Any())
            {
                var minX = allBounds.Min(b => b.MinX);
                var maxX = allBounds.Max(b => b.MaxX);
                var minY = allBounds.Min(b => b.MinY);
                var maxY = allBounds.Max(b => b.MaxY);
                _worldBounds = new AxisAlignedBounds(minX, minY, maxX, maxY);

                _cellSize = cellSizeOverride ?? CalculateOptimalCellSize(allBounds);
            }
            else
            {
                _cellSize = cellSizeOverride ?? 100f;
            }

            BuildGrid(blocks);
        }

        private float CalculateOptimalCellSize(List<AxisAlignedBounds> wordBounds)
        {
            int totalWords = wordBounds.Count;

            var widths = wordBounds.Select(b => b.MaxX - b.MinX).ToList();
            var heights = wordBounds.Select(b => b.MaxY - b.MinY).ToList();

            float avgWordWidth = widths.Average();
            float avgWordHeight = heights.Average();
            float medianWordWidth = widths.OrderBy(w => w).Skip(widths.Count / 2).First();
            float medianWordHeight = heights.OrderBy(h => h).Skip(heights.Count / 2).First();

            float avgWordSize = (medianWordWidth + medianWordHeight) / 2f;

            float documentArea = _worldBounds.Area;
            float wordDensity = totalWords / documentArea; // words per unit area

            int targetWordsPerCell = CalculateTargetWordsPerCell(totalWords);

            float densityBasedCellSize = (float)Math.Sqrt(targetWordsPerCell / wordDensity);
            float wordSizeBasedCell = avgWordSize * 4f;
            float aspectRatioAdjusted = AdjustForDocumentAspectRatio(wordSizeBasedCell);

            float optimalSize = ChooseBestSizingStrategy(totalWords, wordSizeBasedCell,
                                                       densityBasedCellSize, aspectRatioAdjusted);

            float minCellSize = Math.Max(avgWordSize * 1.5f, 20f);
            float maxCellSize = Math.Min(_worldBounds.MaxX - _worldBounds.MinX,
                                       _worldBounds.MaxY - _worldBounds.MinY) / 8f;

            optimalSize = Math.Max(minCellSize, Math.Min(maxCellSize, optimalSize));

            float actualWordsPerCell = EstimateWordsPerCell(optimalSize, wordBounds);

            System.Diagnostics.Debug.WriteLine($"Spatial Grid: {totalWords} words, " +
                $"avg word size: {avgWordSize:F1}, density: {wordDensity:F6}, " +
                $"target per cell: {targetWordsPerCell}, optimal cell size: {optimalSize:F1}, " +
                $"estimated words/cell: {actualWordsPerCell:F1}");

            return optimalSize;
        }

        private int CalculateTargetWordsPerCell(int totalWords)
        {
            // Adaptive target based on document size and expected usage patterns
            if (totalWords < 100) return 2;      // Small docs: very fine-grained
            if (totalWords < 1000) return 4;     // Medium docs: balanced
            if (totalWords < 5000) return 8;     // Large docs: coarser but still responsive
            return 16;                           // Very large docs: prioritize memory/speed
        }

        private float AdjustForDocumentAspectRatio(float baseSize)
        {
            // Adjust cell size for very wide or very tall documents
            float aspectRatio = (_worldBounds.MaxX - _worldBounds.MinX) /
                               (_worldBounds.MaxY - _worldBounds.MinY);

            if (aspectRatio > 3f) // Very wide document (like spreadsheet row)
            {
                return baseSize * 0.7f; // Smaller cells for better horizontal resolution
            }
            else if (aspectRatio < 0.33f) // Very tall document (like column)
            {
                return baseSize * 0.7f; // Smaller cells for better vertical resolution
            }

            return baseSize;
        }

        private float ChooseBestSizingStrategy(int totalWords, float wordSizeBased,
                                             float densityBased, float aspectAdjusted)
        {
            // For small documents, word-size based is usually better
            if (totalWords < 500)
            {
                return Math.Min(wordSizeBased, aspectAdjusted);
            }

            // For large documents, blend strategies
            float blendFactor = Math.Min(1f, totalWords / 2000f);
            return wordSizeBased * (1f - blendFactor) + densityBased * blendFactor;
        }

        private float EstimateWordsPerCell(float cellSize, List<AxisAlignedBounds> wordBounds)
        {
            // Quick simulation: sample a few cells and count average occupancy
            int gridWidth = (int)Math.Ceiling((_worldBounds.MaxX - _worldBounds.MinX) / cellSize);
            int gridHeight = (int)Math.Ceiling((_worldBounds.MaxY - _worldBounds.MinY) / cellSize);

            int sampleCells = Math.Min(25, gridWidth * gridHeight / 4); // Sample up to 25 cells
            int totalSampledWords = 0;

            var random = new Random(42); // Deterministic for consistency

            for (int i = 0; i < sampleCells; i++)
            {
                int cellX = random.Next(gridWidth);
                int cellY = random.Next(gridHeight);

                float cellMinX = _worldBounds.MinX + cellX * cellSize;
                float cellMaxX = cellMinX + cellSize;
                float cellMinY = _worldBounds.MinY + cellY * cellSize;
                float cellMaxY = cellMinY + cellSize;

                var cellBounds = new AxisAlignedBounds(cellMinX, cellMinY, cellMaxX, cellMaxY);

                int wordsInCell = wordBounds.Count(wb => wb.Intersects(cellBounds));
                totalSampledWords += wordsInCell;
            }

            return sampleCells > 0 ? (float)totalSampledWords / sampleCells : 0;
        }

        private void BuildGrid(IList<RecognizedTextBlock> blocks)
        {
            for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                var block = blocks[blockIndex];

                for (int lineIndex = 0; lineIndex < block.Lines.Count; lineIndex++)
                {
                    var line = block.Lines[lineIndex];

                    for (int wordIndex = 0; wordIndex < line.Words.Count; wordIndex++)
                    {
                        var word = line.Words[wordIndex];
                        var wordPointer = new RecognizedTextPointer(blockIndex, lineIndex, wordIndex, word.BoundingBox);

                        InsertWordIntoGrid(wordPointer);
                    }
                }
            }
        }

        private void InsertWordIntoGrid(RecognizedTextPointer word)
        {
            var bounds = AxisAlignedBounds.FromBoundingBox(word.BoundingBox);

            int minCellX = (int)Math.Floor((bounds.MinX - _worldBounds.MinX) / _cellSize);
            int maxCellX = (int)Math.Floor((bounds.MaxX - _worldBounds.MinX) / _cellSize);
            int minCellY = (int)Math.Floor((bounds.MinY - _worldBounds.MinY) / _cellSize);
            int maxCellY = (int)Math.Floor((bounds.MaxY - _worldBounds.MinY) / _cellSize);

            for (int x = minCellX; x <= maxCellX; x++)
            {
                for (int y = minCellY; y <= maxCellY; y++)
                {
                    var key = (x, y);
                    if (!_grid.ContainsKey(key))
                    {
                        _grid[key] = new List<RecognizedTextPointer>();
                    }
                    _grid[key].Add(word);
                }
            }
        }

        public List<RecognizedTextPointer> GetCandidatesNear(Vector2 point, float radius = 0)
        {
            var searchBounds = new AxisAlignedBounds(point.X - radius, point.Y - radius,
                                                   point.X + radius, point.Y + radius);

            int minCellX = (int)Math.Floor((searchBounds.MinX - _worldBounds.MinX) / _cellSize);
            int maxCellX = (int)Math.Floor((searchBounds.MaxX - _worldBounds.MinX) / _cellSize);
            int minCellY = (int)Math.Floor((searchBounds.MinY - _worldBounds.MinY) / _cellSize);
            int maxCellY = (int)Math.Floor((searchBounds.MaxY - _worldBounds.MinY) / _cellSize);

            var candidates = new HashSet<RecognizedTextPointer>();

            for (int x = minCellX; x <= maxCellX; x++)
            {
                for (int y = minCellY; y <= maxCellY; y++)
                {
                    var key = (x, y);
                    if (_grid.TryGetValue(key, out var words))
                    {
                        foreach (var word in words)
                        {
                            candidates.Add(word);
                        }
                    }
                }
            }

            return candidates.ToList();
        }


        public readonly struct AxisAlignedBounds
        {
            public readonly float MinX;
            public readonly float MinY;
            public readonly float MaxX;
            public readonly float MaxY;

            public AxisAlignedBounds(float minX, float minY, float maxX, float maxY)
            {
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
            }

            public static AxisAlignedBounds FromBoundingBox(RecognizedTextBoundingBox box)
            {
                var points = new[] { box.TopLeft, box.TopRight, box.BottomLeft, box.BottomRight };
                var minX = points.Min(p => p.X);
                var maxX = points.Max(p => p.X);
                var minY = points.Min(p => p.Y);
                var maxY = points.Max(p => p.Y);

                return new AxisAlignedBounds(minX, minY, maxX, maxY);
            }

            public bool Contains(Vector2 point)
            {
                return point.X >= MinX && point.X <= MaxX &&
                       point.Y >= MinY && point.Y <= MaxY;
            }

            public bool Intersects(AxisAlignedBounds other)
            {
                return MinX <= other.MaxX && MaxX >= other.MinX &&
                       MinY <= other.MaxY && MaxY >= other.MinY;
            }

            public float Area => (MaxX - MinX) * (MaxY - MinY);

            public Vector2 Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);

            public AxisAlignedBounds Union(AxisAlignedBounds other)
            {
                return new AxisAlignedBounds(
                    Math.Min(MinX, other.MinX),
                    Math.Min(MinY, other.MinY),
                    Math.Max(MaxX, other.MaxX),
                    Math.Max(MaxY, other.MaxY)
                );
            }

            public AxisAlignedBounds Expand(float radius)
            {
                return new AxisAlignedBounds(MinX - radius, MinY - radius, MaxX + radius, MaxY + radius);
            }
        }
    }
}
