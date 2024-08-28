using System;
using System.Collections.Generic;
using System.Linq;

namespace Rg.DiffUtils
{
    public static class DiffUtil
    {
        public static DiffResult<T> CalculateDiff<T>(IEnumerable<T> seq1, IEnumerable<T> seq2)
        {
            return CalculateDiff(seq1, seq2, DiffEqualityComparer<T>.Default, new DiffOptions());
        }

        public static DiffResult<T> CalculateDiff<T>(IEnumerable<T> seq1, IEnumerable<T> seq2, IDiffEqualityComparer<T> comparer)
        {
            return CalculateDiff(seq1, seq2, comparer, new DiffOptions());
        }

        public static DiffResult<T> CalculateDiff<T>(IEnumerable<T> seq1, IEnumerable<T> seq2, DiffEqualityComparer<T>.ComparerItemsDelegate comparer)
        {
            return CalculateDiff(seq1, seq2, comparer, new DiffOptions());
        }

        public static DiffResult<T> CalculateDiff<T>(IEnumerable<T> seq1, IEnumerable<T> seq2, DiffEqualityComparer<T>.ComparerItemsDelegate comparer, DiffOptions options)
        {
            return CalculateDiff(seq1, seq2, new DiffEqualityComparer<T>(comparer), options);
        }

        public static DiffResult<T> CalculateDiff<T>(IEnumerable<T> seq1, IEnumerable<T> seq2, DiffOptions options)
        {
            return CalculateDiff(seq1, seq2, DiffEqualityComparer<T>.Default, options);
        }

        public static DiffResult<T> CalculateDiff<T>(IEnumerable<T> seq1, IEnumerable<T> seq2, IDiffEqualityComparer<T> comparer, DiffOptions options)
        {
            var array1 = seq1.ToArray();
            var array2 = seq2.ToArray();

            var oldSize = array1.Length;
            var newSize = array2.Length;

            var diagonals = new List<Diagonal>();

            var stack = new Stack<DiffRange>();

            stack.Push(new DiffRange(0, oldSize, 0, newSize));

            var max = (oldSize + newSize + 1) / 2;
            
            var forward = new CenteredArray(max * 2 + 1);
            var backward = new CenteredArray(max * 2 + 1);

            var rangePool = new Stack<DiffRange>();

            while(stack.Any())
            {
                var range = stack.Pop();
                var snake = MidPoint(array1, array2, range, forward, backward, comparer);

                if(snake != null)
                {
                    if (snake.DiagonalSize > 0)
                        diagonals.Add(snake.ToDiagonal());

                    var left = rangePool.Any() ? rangePool.Pop() : new DiffRange();

                    left.OldListStart = range.OldListStart;
                    left.NewListStart = range.NewListStart;
                    left.OldListEnd = snake.StartX;
                    left.NewListEnd = snake.StartY;

                    stack.Push(left);

                    var right = range;

                    right.OldListEnd = range.OldListEnd;
                    right.NewListEnd = range.NewListEnd;
                    right.OldListStart = snake.EndX;
                    right.NewListStart = snake.EndY;

                    stack.Push(right);
                }
                else
                {
                    rangePool.Push(range);
                }
            }

            diagonals.Sort(new DiagonalComparer());

            return new DiffResult<T>(array1, array2, diagonals, forward.BackingData(), backward.BackingData(), comparer, options);
        }

        static Snake MidPoint<T>(
            T[] array1, T[] array2,
            DiffRange range,
            CenteredArray forward,
            CenteredArray backward,
            IDiffEqualityComparer<T> comparer)
        {
            if (range.OldSize < 1 || range.NewSize < 1)
                return null;

            var max = (range.OldSize + range.NewSize + 1) / 2;

            forward.Set(1, range.OldListStart);
            backward.Set(1, range.OldListEnd);

            for (int d = 0; d < max; d++)
            {
                var snake = Forward(array1, array2, range, forward, backward, d, comparer);

                if (snake != null)
                    return snake;

                snake = Backward(array1, array2, range, forward, backward, d, comparer);

                if (snake != null)
                    return snake;
            }

            return null;
        }

        static Snake Forward<T>(
            T[] array1, T[] array2,
            DiffRange range,
            CenteredArray forward,
            CenteredArray backward,
            int d,
            IDiffEqualityComparer<T> comparer)
        {
            var checkForSnake = Math.Abs(range.OldSize - range.NewSize) % 2 == 1;
            int delta = range.OldSize - range.NewSize;

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
                    int backwardsK = delta - k;

                    if (backwardsK >= -d + 1
                            && backwardsK <= d - 1
                            && backward.Get(backwardsK) <= x)
                    {
                        Snake snake = new Snake
                        {
                            StartX = startX,
                            StartY = startY,
                            EndX = x,
                            EndY = y,
                            IsReverse = false
                        };

                        return snake;
                    }
                }
            }

            return null;
        }

        static Snake Backward<T>(
            T[] array1, T[] array2,
            DiffRange range,
            CenteredArray forward,
            CenteredArray backward,
            int d,
            IDiffEqualityComparer<T> compare)
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
                    && compare.CompareItems(array1[x - 1], array2[y - 1]))
                {
                    x--;
                    y--;
                }

                backward.Set(k, x);

                if (checkForSnake)
                {
                    int forwardsK = delta - k;

                    if (forwardsK >= -d
                            && forwardsK <= d
                            && forward.Get(forwardsK) >= x)
                    {
                        Snake snake = new Snake
                        {
                            StartX = x,
                            StartY = y,
                            EndX = startX,
                            EndY = startY,
                            IsReverse = true
                        };

                        return snake;
                    }
                }
            }

            return null;
        }
    }
}
