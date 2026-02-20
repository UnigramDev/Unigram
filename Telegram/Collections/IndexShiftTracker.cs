//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;

namespace Telegram.Collections
{
    public sealed class IndexShiftTracker
    {
        private readonly List<Shift> _shifts = new();
        private readonly List<Range> _pending = new();

        private bool _invalidated;

        public int Translate(int currentIndex)
        {
            int offset = 0;
            foreach (var s in _shifts/*.OrderBy(x => x.Index)*/)
            {
                if (s.Index <= currentIndex)
                    offset += s.Delta;
                else
                    break;
            }
            return currentIndex + offset;
        }

        public int ReverseTranslate(int translatedIndex)
        {
            int offset = 0;
            foreach (var s in _shifts/*.OrderBy(x => x.Index)*/)
            {
                if (s.Index <= translatedIndex)
                    offset -= s.Delta;
                else
                    break;
            }
            return translatedIndex + offset;
        }

        public void RegisterRemove(int index, int oldStartingIndex, float height, bool anchor)
        {
            _shifts.Add(new Shift(oldStartingIndex, +1));
            _pending.Add(new Range(index, height, anchor));
        }

        public void RegisterRemove(int index, int count = 1)
        {
            if (_pending.Count > 0)
            {
                _shifts.Add(new Shift(index, +count));
            }
        }

        public void RegisterInsert(int index, int count = 1)
        {
            if (_pending.Count > 0)
            {
                _shifts.Add(new Shift(index, -count));
            }
        }

        public void Invalidate()
        {
            _invalidated = true;
        }

        public void Clear()
        {
            _shifts.ClearIfNotEmpty();
            _pending.ClearIfNotEmpty();
            _invalidated = false;
        }

        public bool HasRanges() => !_invalidated && _pending.Count > 0;

        public List<Range> GetRanges(bool reverse)
        {
            if (_invalidated)
            {
                return null;
            }

            var ordered = _pending.OrderBy(p => p.Index).ToList();
            var ranges = new List<Range>();

            int start = ordered[0].Index;
            int last = start;
            float totalHeight = ordered[0].Height;
            bool anchor = ordered[0].Anchor;

            for (int i = 1; i < ordered.Count; i++)
            {
                var cur = ordered[i];
                if (cur.Index == last + 1)
                {
                    last = cur.Index;
                    totalHeight += cur.Height;
                    anchor |= cur.Anchor;
                }
                else
                {
                    ranges.Add(new Range(start, totalHeight, anchor));

                    start = last = cur.Index;
                    totalHeight = cur.Height;
                    anchor = cur.Anchor;
                }
            }

            ranges.Add(new Range(start, totalHeight, anchor));

            var adjusted = new List<Range>(ranges.Count);

            int seen = 0;
            int lastOriginal = int.MinValue;
            int dupCountForSameOriginal = 0;

            for (int i = 0; i < ranges.Count; i++)
            {
                var p = ranges[i];
                if (p.Index != lastOriginal)
                {
                    lastOriginal = p.Index;
                    dupCountForSameOriginal = 0;
                }

                int countLess = seen;
                int adjustedIndex = p.Index - countLess;
                adjusted.Add(new Range(adjustedIndex, p.Height, p.Anchor));

                dupCountForSameOriginal++;
                seen++;
            }

            if (adjusted.Count == 0)
                return null;

            if (reverse)
            {
                adjusted.Sort((a, b) => b.Index.CompareTo(a.Index));
            }
            else
            {
                adjusted.Sort((a, b) => a.Index.CompareTo(b.Index));
            }

            return adjusted;
        }

        private readonly record struct Shift(int Index, int Delta);

        public readonly record struct Range(int Index, float Height, bool Anchor);
    }
}
