//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Common
{
    /// <summary>
    /// The widths of a table's columns in the room it is given: natural widths when they fit,
    /// the room shared out when they do not, and wider than the room, for the caller to scroll,
    /// once they cannot be squeezed without wrapping their text narrower than a reader can
    /// follow. Fed each cell's narrowest and natural width, which do not depend on the room, so
    /// a new width resolves again without measuring any text.
    /// </summary>
    public sealed class TableColumns
    {
        // How narrow a column has to be squeezed before it is allowed to wrap at all. Under
        // it a column keeps its natural width, so a thin column beside a wide one is left
        // alone and only the wide one gives ground.
        private const double MinColumnWidth = 96;

        private readonly struct Span
        {
            public Span(int column, int colspan, double min, double max)
            {
                Column = column;
                Colspan = colspan;
                Min = min;
                Max = max;
            }

            public readonly int Column;
            public readonly int Colspan;
            public readonly double Min;
            public readonly double Max;
        }

        // Kept across tables for their capacity.
        private readonly List<Span> _spans = new List<Span>();

        private double[] _min = Array.Empty<double>();
        private double[] _max = Array.Empty<double>();
        private double[] _widths = Array.Empty<double>();
        private double[] _deficit = Array.Empty<double>();

        private readonly double _slop;
        private readonly double _minimum;

        private int _count;
        private double _scale = 1;
        private double _padding;

        // What every cell pays on top of its text: its padding and the border on its trailing
        // edge, and in the first column the table's leading edge as well.
        private double _padded;
        private double _border;

        // A measure pass offers the same width again and again: answered from the last time.
        private double _resolvedFor = double.NaN;
        private double _total;

        /// <param name="slop">Room a column gets over what its text measured, for a caller that
        /// measures with one engine and draws with another.</param>
        /// <param name="minimum">The narrowest any column may be, whatever its text.</param>
        public TableColumns(double slop, double minimum)
        {
            _slop = slop;
            _minimum = minimum;
        }

        public int Count => _count;

        public double this[int column] => _widths[column];

        /// <summary>Whether the columns are wider than the room the last resolve was given.</summary>
        public bool IsScrollable { get; private set; }

        /// <summary>
        /// Starts again for <paramref name="count"/> columns laid out at <paramref name="scale"/>,
        /// each cell inside <paramref name="padding"/> and a border <paramref name="thickness"/> wide.
        /// </summary>
        public void Begin(int count, double scale, Thickness padding, double thickness)
        {
            if (_min.Length < count)
            {
                _min = new double[count];
                _max = new double[count];
                _widths = new double[count];
                _deficit = new double[count];
            }
            else
            {
                Array.Clear(_min, 0, count);
                Array.Clear(_max, 0, count);
            }

            _count = count;
            _scale = scale;
            _padding = padding.Left;
            _spans.Clear();
            _resolvedFor = double.NaN;

            // Rounded up one edge at a time, as that is how they are laid out: a 1 DIP border is
            // a pixel and a half at 150% and takes two, and a cell short of that half pixel wraps.
            _padded = Snap(padding.Left) + Snap(padding.Right);
            _border = Snap(thickness);
        }

        /// <summary>A cell's narrowest and natural width, as its text measures.</summary>
        public void Add(int column, int colspan, double min, double max)
        {
            if (column < 0 || column >= _count)
            {
                return;
            }

            var overhead = Overhead(column);
            min += overhead;
            max += overhead;

            if (colspan > 1)
            {
                _spans.Add(new Span(column, Math.Min(colspan, _count - column), min, max));
            }
            else
            {
                _min[column] = Math.Max(_min[column], min);
                _max[column] = Math.Max(_max[column], max);
            }
        }

        public void End()
        {
            // A spanned cell only has to fit across the columns it covers, so it grows them
            // just enough, and only once every single column cell has had its say.
            foreach (var span in _spans)
            {
                Grow(_min, span.Column, span.Colspan, span.Min);
                Grow(_max, span.Column, span.Colspan, span.Max);
            }

            for (int i = 0; i < _count; i++)
            {
                _max[i] = Math.Max(_max[i], _minimum);

                // A column is only asked to wrap once it is wider than a column has any need
                // to be: under that its natural width is its floor, and past it the floor is
                // the longest word, which is as narrow as text can go.
                _min[i] = Math.Max(_min[i], Math.Min(_max[i], Math.Max(MinColumnWidth, _minimum)));

                // Rounded up to a whole device pixel, which is where layout rounding puts every
                // column edge anyway. Up, because a column a hair narrower than its text wraps a
                // word no one asked to wrap; onto the grid, because a width that is not on it is
                // rounded there later, and rounding outwards is what leaves a ScrollViewer
                // scrollable by half a pixel with the scrollbar too small to see.
                _min[i] = Snap(_min[i]);
                _max[i] = Snap(_max[i]);
            }
        }

        /// <summary>Rounded up to a whole device pixel.</summary>
        public double Snap(double value)
        {
            return Math.Ceiling(value * _scale) / _scale;
        }

        /// <summary>The columns in <paramref name="available"/>, and how wide they come to.</summary>
        public double Resolve(double available)
        {
            if (available != _resolvedFor)
            {
                _resolvedFor = available;
                _total = ResolveCore(available);
            }

            return _total;
        }

        /// <summary>
        /// The last resolve's widths onto <paramref name="grid"/>'s columns, and
        /// <paramref name="scroll"/> scrollable only if they need it.
        /// </summary>
        public void Apply(Grid grid, ScrollViewer scroll)
        {
            var definitions = grid.ColumnDefinitions;

            for (int i = 0; i < _count && i < definitions.Count; i++)
            {
                var definition = definitions[i];

                // Assigning invalidates the layout of the grid, so only where it moved.
                if (!definition.Width.IsAbsolute || definition.Width.Value != _widths[i])
                {
                    definition.Width = new GridLength(_widths[i]);
                }
            }

            // A table that fits cannot be scrolled, so it must not be scrollable: left on, the
            // ScrollViewer swallows the mouse wheel over any table, and a stray fraction of a
            // pixel is enough for it to believe it has somewhere to go.
            var mode = IsScrollable ? ScrollMode.Auto : ScrollMode.Disabled;

            if (scroll.HorizontalScrollMode != mode)
            {
                scroll.HorizontalScrollMode = mode;
                scroll.HorizontalScrollBarVisibility = IsScrollable
                    ? ScrollBarVisibility.Auto
                    : ScrollBarVisibility.Disabled;
            }
        }

        // The three outcomes: natural widths, squeezed to fit, or scrolling.
        private double ResolveCore(double available)
        {
            var totalMax = 0d;

            for (int i = 0; i < _count; i++)
            {
                totalMax += _max[i];
            }

            // It fits unwrapped: the table is as wide as its content and no wider.
            if (double.IsInfinity(available) || totalMax <= available)
            {
                Array.Copy(_max, _widths, _count);
                IsScrollable = false;
                return totalMax;
            }

            // No column is squeezed below its text laid out at two thirds of the room, or
            // half less padding when there are two, so the floor is a readable width rather
            // than the longest word. Floored at the longest word, a table of prose fits any
            // width by wrapping each cell a word a line, and never scrolls.
            var text = _count == 2
                ? available / 2 - 4 * _padding
                : available / 1.5;

            var totalFloor = 0d;

            for (int i = 0; i < _count; i++)
            {
                _widths[i] = Math.Max(_min[i], Math.Min(_max[i], Snap(text + Overhead(i))));
                _deficit[i] = _max[i] - _widths[i];
                totalFloor += _widths[i];
            }

            if (totalFloor >= available)
            {
                IsScrollable = true;
                return totalFloor;
            }

            // In between, every column gets its floor and the room left over is handed
            // round in equal shares until each column has all it asked for or there is
            // nothing left - so a narrow column beside a wide one is satisfied outright
            // rather than left wrapping with a share proportional to a need it does not
            // have.
            var extra = available - totalFloor;
            var epsilon = 0.5 / _scale;

            while (extra > epsilon)
            {
                var active = 0;

                for (int i = 0; i < _count; i++)
                {
                    if (_deficit[i] > 0)
                    {
                        active++;
                    }
                }

                if (active == 0)
                {
                    break;
                }

                var step = extra / active;

                for (int i = 0; i < _count && extra > 0; i++)
                {
                    if (_deficit[i] <= 0)
                    {
                        continue;
                    }

                    var delta = Math.Min(_deficit[i], Math.Min(step, extra));

                    _widths[i] += delta;
                    _deficit[i] -= delta;
                    extra -= delta;
                }
            }

            // Snapped down onto the pixel grid so the columns still add up to something the
            // room can hold, with the rounding left over going to the widest column, the one
            // most likely to still be wrapping.
            var used = 0d;
            var widest = 0;

            for (int i = 0; i < _count; i++)
            {
                _widths[i] = Math.Floor(_widths[i] * _scale) / _scale;
                used += _widths[i];

                if (_max[i] > _max[widest])
                {
                    widest = i;
                }
            }

            var total = Math.Floor(available * _scale) / _scale;

            if (total > used)
            {
                _widths[widest] += total - used;
            }

            IsScrollable = false;
            return total;
        }

        // Summed in this order: at a fractional scale another one can land a hair either side of
        // a device pixel, which the rounding up then turns into a whole one.
        private double Overhead(int column)
        {
            return _padded + (column == 0 ? 2 : 1) * _border + _slop;
        }

        private static void Grow(double[] widths, int column, int colspan, double required)
        {
            var current = 0d;

            for (int i = column; i < column + colspan; i++)
            {
                current += widths[i];
            }

            if (current >= required)
            {
                return;
            }

            var growth = (required - current) / colspan;

            for (int i = column; i < column + colspan; i++)
            {
                widths[i] += growth;
            }
        }
    }
}
