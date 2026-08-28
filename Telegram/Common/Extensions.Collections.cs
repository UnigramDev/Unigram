//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Telegram.Common
{
    public static partial class Extensions
    {
        public static int FindIndex<T>(this IReadOnlyList<T> list, Func<T, bool> predicate)
        {
            for (int i = 0; i < list.Count; i++)
                if (predicate(list[i])) return i;
            return -1;
        }

        public static int FindLastIndex<T>(this IReadOnlyList<T> list, Func<T, bool> predicate)
        {
            for (int i = list.Count - 1; i >= 0; i--)
                if (predicate(list[i])) return i;
            return -1;
        }

        public static T Random<T>(this IReadOnlyList<T> source)
        {
            if (source.Count > 0)
            {
                return source[new Random().Next(source.Count)];
            }

            return default;
        }

        public static bool TryGet<TKey, TValue>(this IDictionary<TKey, object> dict, TKey key, out TValue value)
        {
            if (dict.TryGetValue(key, out object tryGetValue) && tryGetValue is TValue tryGet)
            {
                value = tryGet;
                return true;
            }

            value = default;
            return false;
        }

        public static void Put<T>(this IList<T> source, bool begin, T item)
        {
            if (begin)
            {
                source.Insert(0, item);
            }
            else
            {
                source.Add(item);
            }
        }

        // Rotates rather than shifts: a positive offset moves the front to the back, a negative
        // one the back to the front. Reversal keeps it O(n) without a temporary array.
        public static void ShiftInPlace<T>(this T[] array, int offset)
        {
            var count = Normalize(array.Length, offset);
            if (count == 0)
            {
                return;
            }

            Array.Reverse(array, 0, count);
            Array.Reverse(array, count, array.Length - count);
            Array.Reverse(array);
        }

        public static T[] Shift<T>(this T[] array, int offset)
        {
            var output = new T[array.Length];
            var count = Normalize(array.Length, offset);

            Array.Copy(array, count, output, 0, array.Length - count);
            Array.Copy(array, 0, output, array.Length - count, count);

            return output;
        }

        private static int Normalize(int length, int offset)
        {
            if (length == 0)
            {
                return 0;
            }

            return ((offset % length) + length) % length;
        }

#if !NET9_0_OR_GREATER
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
#endif

        public static long Hash<T>(this IEnumerable<T> source, Func<T, long> predicate)
        {
            var hash = 0L;

            foreach (var item in source)
            {
                hash = ((hash * 20261) + 0x80000000L + predicate(item)) % 0x80000000L;
            }

            return hash;
        }

        public static T RemoveLast<T>(this List<T> list)
        {
            if (list.Count > 0)
            {
                var last = list[list.Count - 1];
                list.Remove(last);

                return last;
            }

            return default;
        }

        public static void AddRange<T>(this IList<T> list, IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                list.Add(item);
            }
        }

        public static void AddRange<T>(this IList<T> list, params T[] source)
        {
            foreach (var item in source)
            {
                list.Add(item);
            }
        }

        public static int BinarySearch<TItem, TSearch>(this IList<TItem> list, TSearch value, Func<TSearch, TItem, int> comparer)
        {
            int lower = 0;
            int upper = list.Count - 1;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                int comparisonResult = comparer(value, list[middle]);
                if (comparisonResult < 0)
                {
                    upper = middle - 1;
                }
                else if (comparisonResult > 0)
                {
                    lower = middle + 1;
                }
                else
                {
                    return middle;
                }
            }

            return ~lower;
        }

        public static int BinarySearch<TItem>(this IList<TItem> list, TItem value)
        {
            return BinarySearch(list, value, Comparer<TItem>.Default);
        }

        public static int BinarySearch<TItem>(this IList<TItem> list, TItem value, IComparer<TItem> comparer)
        {
            return list.BinarySearch(value, comparer.Compare);
        }

        public static void ClearIfNotEmpty<T>(this IList<T> list)
        {
            if (list.Count > 0)
            {
                list.Clear();
            }
        }

        //public static bool Empty<T>(this ICollection<T> list)
        //{
        //    return list.Count == 0;
        //}

        public static bool Empty<T>(this IEnumerable<T> source)
        {
            if (source is ICollection<T> collection)
            {
                return collection.Count == 0;
            }

            if (source is IReadOnlyCollection<T> readOnly)
            {
                return readOnly.Count == 0;
            }

            return !source.Any();
        }

        public static bool EmptyT(this IList list)
        {
            return list.Count == 0;
        }

        public static void ForEach<T>(this IEnumerable<T> list, Action<T> action)
        {
            foreach (var item in list)
            {
                action?.Invoke(item);
            }
        }
    }
}
