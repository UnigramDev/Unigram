//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    // Extension methods for ItemIndexRange
    public static class ItemIndexRangeExtensions
    {
        public static bool Equals(this ItemIndexRange This, ItemIndexRange range)
        {
            return (This.FirstIndex == range.FirstIndex && This.Length == range.Length);
        }

        public static bool ContiguousOrOverlaps(this ItemIndexRange This, ItemIndexRange range)
        {
            // This is left
            if (This.FirstIndex < range.FirstIndex)
            {
                return (range.FirstIndex <= This.LastIndex + 1);
            }
            // This is right
            else if (This.FirstIndex > range.FirstIndex)
            {
                return (This.FirstIndex <= range.LastIndex + 1);
            }

            // Aligned
            return true;
        }

        public static bool Intersects(this ItemIndexRange This, ItemIndexRange range)
        {
            return (range.FirstIndex >= This.FirstIndex && range.FirstIndex <= This.LastIndex) || (range.LastIndex >= This.FirstIndex && range.LastIndex <= This.LastIndex);
        }

        public static bool Intersects(this ItemIndexRange This, int FirstIndex, uint Length)
        {
            int LastIndex = FirstIndex + (int)Length - 1;
            return (FirstIndex >= This.FirstIndex && FirstIndex <= This.LastIndex) || (LastIndex >= This.FirstIndex && LastIndex <= This.LastIndex);
        }

        public static ItemIndexRange Combine(this ItemIndexRange This, ItemIndexRange range)
        {
            int start = Math.Min(This.FirstIndex, range.FirstIndex);
            int end = Math.Max(This.LastIndex, range.LastIndex);

            return new ItemIndexRange(start, 1 + (uint)Math.Abs(end - start));
        }

        public static bool DiffRanges(this ItemIndexRange RangeA, ItemIndexRange RangeB, out ItemIndexRange InBothAandB, out ItemIndexRange[] OnlyInRangeA, out ItemIndexRange[] OnlyInRangeB)
        {
            List<ItemIndexRange> exA = new();
            List<ItemIndexRange> exB = new();
            int i, j;
            i = Math.Max(RangeA.FirstIndex, RangeB.FirstIndex);
            j = Math.Min(RangeA.LastIndex, RangeB.LastIndex);

            if (i <= j)
            {
                // Ranges intersect
                InBothAandB = new ItemIndexRange(i, (uint)(1 + j - i));
                if (RangeA.FirstIndex < i) exA.Add(new ItemIndexRange(RangeA.FirstIndex, (uint)(i - RangeA.FirstIndex)));
                if (RangeA.LastIndex > j) exA.Add(new ItemIndexRange(j + 1, (uint)(RangeA.LastIndex - j)));
                if (RangeB.FirstIndex < i) exB.Add(new ItemIndexRange(RangeB.FirstIndex, (uint)(i - RangeB.FirstIndex)));
                if (RangeB.LastIndex > j) exB.Add(new ItemIndexRange(j + 1, (uint)(RangeB.LastIndex - j)));
                OnlyInRangeA = exA.ToArray();
                OnlyInRangeB = exB.ToArray();
                return true;
            }
            else
            {
                InBothAandB = default(ItemIndexRange);
                OnlyInRangeA = new ItemIndexRange[] { RangeA };
                OnlyInRangeB = new ItemIndexRange[] { RangeB };
                return false;
            }
        }

        public static ItemIndexRange Overlap(this ItemIndexRange RangeA, ItemIndexRange RangeB)
        {
            int i, j;
            i = Math.Max(RangeA.FirstIndex, RangeB.FirstIndex);
            j = Math.Min(RangeA.LastIndex, RangeB.LastIndex);

            if (i <= j)
            {
                // Ranges intersect
                return new ItemIndexRange(i, (uint)(1 + j - i));
            }
            else
            {
                return null;
            }
        }

    }
}
