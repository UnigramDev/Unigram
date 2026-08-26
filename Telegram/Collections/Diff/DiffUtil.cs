//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
// Based on Rg.DiffUtils <https://github.com/rotorgames/Rg.DiffUtils>, itself a port of
// Android's androidx.recyclerview.widget.DiffUtil. Reworked here: batching dropped, the
// per-item and per-step objects turned into values, and move detection's quadratic
// allocation removed.
//
// Copyright (c) 2021 Kirill Lyubimov
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Linq;

namespace Telegram.Collections
{
    internal static class DiffUtil
    {
        /// <summary>
        /// Runs Myers over the two sequences and reports the diagonals - the runs the two
        /// have in common - along with the scratch arrays the caller reuses to mark moves.
        /// </summary>
        public static void CalculateDiagonals<T>(
            IEnumerable<T> seq1, IEnumerable<T> seq2,
            IDiffEqualityComparer<T> comparer,
            out T[] array1, out T[] array2,
            out List<Diagonal> diagonals,
            out int[] oldItemStatuses, out int[] newItemStatuses)
        {
            // Both sequences are snapshotted here and never read again, so everything after
            // this is safe against the caller's collections changing - but these two lines
            // are not, which is why a diff has to start on the thread that owns them.
            array1 = seq1.ToArray();
            array2 = seq2.ToArray();

            var oldSize = array1.Length;
            var newSize = array2.Length;

            diagonals = new List<Diagonal>();

            var stack = new Stack<DiffRange>();
            stack.Push(new DiffRange(0, oldSize, 0, newSize));

            var max = (oldSize + newSize + 1) / 2;

            var forward = new CenteredArray(max * 2 + 1);
            var backward = new CenteredArray(max * 2 + 1);

            while (stack.Count > 0)
            {
                var range = stack.Pop();

                if (!TryMidPoint(array1, array2, range, forward, backward, comparer, out var snake))
                {
                    continue;
                }

                if (snake.DiagonalSize > 0)
                {
                    diagonals.Add(snake.ToDiagonal());
                }

                stack.Push(new DiffRange(range.OldListStart, snake.StartX, range.NewListStart, snake.StartY));
                stack.Push(new DiffRange(snake.EndX, range.OldListEnd, snake.EndY, range.NewListEnd));
            }

            diagonals.Sort();

            // The two arrays Myers walked are long enough to index every item, so they are
            // reused as the move-detection status arrays rather than allocated again.
            oldItemStatuses = forward.BackingData();
            newItemStatuses = backward.BackingData();

            Array.Clear(oldItemStatuses, 0, oldItemStatuses.Length);
            Array.Clear(newItemStatuses, 0, newItemStatuses.Length);

            AddEdgeDiagonals(diagonals, oldSize, newSize);
        }

        private static void AddEdgeDiagonals(List<Diagonal> diagonals, int oldSize, int newSize)
        {
            if (diagonals.Count == 0 || diagonals[0].X != 0 || diagonals[0].Y != 0)
            {
                diagonals.Insert(0, new Diagonal(0, 0, 0));
            }

            diagonals.Add(new Diagonal(oldSize, newSize, 0));
        }

        private static bool TryMidPoint<T>(
            T[] array1, T[] array2,
            DiffRange range,
            CenteredArray forward,
            CenteredArray backward,
            IDiffEqualityComparer<T> comparer,
            out Snake snake)
        {
            snake = default;

            if (range.OldSize < 1 || range.NewSize < 1)
            {
                return false;
            }

            var max = (range.OldSize + range.NewSize + 1) / 2;

            forward.Set(1, range.OldListStart);
            backward.Set(1, range.OldListEnd);

            for (int d = 0; d < max; d++)
            {
                if (TryForward(array1, array2, range, forward, backward, d, comparer, out snake))
                {
                    return true;
                }

                if (TryBackward(array1, array2, range, forward, backward, d, comparer, out snake))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryForward<T>(
            T[] array1, T[] array2,
            DiffRange range,
            CenteredArray forward,
            CenteredArray backward,
            int d,
            IDiffEqualityComparer<T> comparer,
            out Snake snake)
        {
            var checkForSnake = Math.Abs(range.OldSize - range.NewSize) % 2 == 1;
            var delta = range.OldSize - range.NewSize;

            for (var k = -d; k <= d; k += 2)
            {
                int startX;
                int startY;
                int x, y;

                if (k == -d || (k != d && forward.Get(k + 1) > forward.Get(k - 1)))
                {
                    x = startX = forward.Get(k + 1);
                }
                else
                {
                    startX = forward.Get(k - 1);
                    x = startX + 1;
                }

                y = range.NewListStart + (x - range.OldListStart) - k;
                startY = (d == 0 || x != startX) ? y : y - 1;

                while (x < range.OldListEnd
                    && y < range.NewListEnd
                    && comparer.CompareItems(array1[x], array2[y]))
                {
                    x++;
                    y++;
                }

                forward.Set(k, x);

                if (checkForSnake)
                {
                    var backwardsK = delta - k;

                    if (backwardsK >= -d + 1
                        && backwardsK <= d - 1
                        && backward.Get(backwardsK) <= x)
                    {
                        snake = new Snake(startX, startY, x, y, false);
                        return true;
                    }
                }
            }

            snake = default;
            return false;
        }

        private static bool TryBackward<T>(
            T[] array1, T[] array2,
            DiffRange range,
            CenteredArray forward,
            CenteredArray backward,
            int d,
            IDiffEqualityComparer<T> comparer,
            out Snake snake)
        {
            var checkForSnake = (range.OldSize - range.NewSize) % 2 == 0;
            var delta = range.OldSize - range.NewSize;

            for (var k = -d; k <= d; k += 2)
            {
                int startX;
                int startY;
                int x, y;

                if (k == -d || (k != d && backward.Get(k + 1) < backward.Get(k - 1)))
                {
                    x = startX = backward.Get(k + 1);
                }
                else
                {
                    startX = backward.Get(k - 1);
                    x = startX - 1;
                }

                y = range.NewListEnd - ((range.OldListEnd - x) - k);
                startY = (d == 0 || x != startX) ? y : y + 1;

                while (x > range.OldListStart
                    && y > range.NewListStart
                    && comparer.CompareItems(array1[x - 1], array2[y - 1]))
                {
                    x--;
                    y--;
                }

                backward.Set(k, x);

                if (checkForSnake)
                {
                    var forwardsK = delta - k;

                    if (forwardsK >= -d
                        && forwardsK <= d
                        && forward.Get(forwardsK) >= x)
                    {
                        snake = new Snake(x, y, startX, startY, true);
                        return true;
                    }
                }
            }

            snake = default;
            return false;
        }
    }

    internal readonly struct CenteredArray
    {
        private readonly int[] _data;
        private readonly int _mid;

        public CenteredArray(int size)
        {
            _data = new int[size];
            _mid = size / 2;
        }

        public int Get(int index) => _data[index + _mid];

        public void Set(int index, int value) => _data[index + _mid] = value;

        public int[] BackingData() => _data;
    }

    internal readonly struct DiffRange
    {
        public int OldListStart { get; }

        public int OldListEnd { get; }

        public int NewListStart { get; }

        public int NewListEnd { get; }

        public int OldSize => OldListEnd - OldListStart;

        public int NewSize => NewListEnd - NewListStart;

        public DiffRange(int oldListStart, int oldListEnd, int newListStart, int newListEnd)
        {
            OldListStart = oldListStart;
            OldListEnd = oldListEnd;
            NewListStart = newListStart;
            NewListEnd = newListEnd;
        }
    }

    internal readonly struct Snake
    {
        public int StartX { get; }

        public int StartY { get; }

        public int EndX { get; }

        public int EndY { get; }

        public bool IsReverse { get; }

        public bool HasAdditionOrRemoval => EndY - StartY != EndX - StartX;

        public bool IsAddition => EndY - StartY > EndX - StartX;

        public int DiagonalSize => Math.Min(EndX - StartX, EndY - StartY);

        public Snake(int startX, int startY, int endX, int endY, bool isReverse)
        {
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
            IsReverse = isReverse;
        }

        public Diagonal ToDiagonal()
        {
            if (!HasAdditionOrRemoval)
            {
                return new Diagonal(StartX, StartY, EndX - StartX);
            }

            if (IsReverse)
            {
                return new Diagonal(StartX, StartY, DiagonalSize);
            }

            return IsAddition
                ? new Diagonal(StartX, StartY + 1, DiagonalSize)
                : new Diagonal(StartX + 1, StartY, DiagonalSize);
        }
    }

    internal readonly struct Diagonal : IComparable<Diagonal>
    {
        public int X { get; }

        public int Y { get; }

        public int Size { get; }

        public int EndX => X + Size;

        public int EndY => Y + Size;

        public Diagonal(int x, int y, int size)
        {
            X = x;
            Y = y;
            Size = size;
        }

        public int CompareTo(Diagonal other) => X - other.X;
    }
}
