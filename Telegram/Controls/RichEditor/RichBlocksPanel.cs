//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.RichEditor
{
    /// <summary>
    /// The document's root blocks, stacked: those realized are laid out, the others stand as a
    /// height, the one they measured at when they last were or an estimate, so a realized block
    /// sits where the blocks before it put it whether those are realized or not. Children are
    /// placed by <see cref="RichBlockPresenter.RootIndex"/>, not by their order in the panel.
    /// </summary>
    public sealed partial class RichBlocksPanel : Panel
    {
        private readonly struct Height
        {
            public Height(double value, double width)
            {
                Value = value;
                Width = width;
            }

            public double Value { get; }

            // The width it was measured at, NaN for an estimate.
            public double Width { get; }
        }

        // By block, so a height survives the root list changing around it; the arrays below
        // are the same by index, for the passes that run on every key.
        private Dictionary<RichBlock, Height> _heights = new();
        private Dictionary<RichBlock, Height> _spare = new();

        private IList<RichBlock> _roots = Array.Empty<RichBlock>();
        private double[] _values = Array.Empty<double>();
        private double[] _widths = Array.Empty<double>();

        // The top of each root, and the total height after the last.
        private double[] _offsets = new double[1];

        private double _width = double.NaN;

        internal RichEditorView Owner { get; set; }

        /// <summary>The root list, after a structural change: what was measured keeps its height, what is new is estimated.</summary>
        internal void SetRoots(IList<RichBlock> roots)
        {
            _roots = roots;

            if (_values.Length < roots.Count)
            {
                _values = new double[Math.Max(roots.Count, _values.Length * 2)];
                _widths = new double[_values.Length];
                _offsets = new double[_values.Length + 1];
            }

            var width = EstimateWidth;

            for (int i = 0; i < roots.Count; i++)
            {
                var block = roots[i];
                if (!_heights.TryGetValue(block, out Height height))
                {
                    height = new Height(Owner.EstimateHeight(roots, i, width), double.NaN);
                }

                _spare[block] = height;
                _values[i] = height.Value;
                _widths[i] = height.Width;
            }

            // What is not among the roots any more is gone from the document, or nested.
            var heights = _heights;
            _heights = _spare;
            _spare = heights;
            _spare.Clear();

            Sum();
            InvalidateMeasure();
        }

        /// <summary>The root at a height from the top of the panel, clamped to the roots there are.</summary>
        internal int IndexAt(double y)
        {
            var count = _roots.Count;
            if (count == 0)
            {
                return -1;
            }

            int low = 0, high = count - 1;

            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (_offsets[middle] <= y)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low;
        }

        internal double OffsetOf(int index) => _offsets[Math.Clamp(index, 0, _roots.Count)];

        // Before the first measure, the width the editor is shown at: the estimates are for it.
        private double EstimateWidth => double.IsNaN(_width) || double.IsInfinity(_width) ? Math.Max(ActualWidth, 320) : _width;

        // For the meter: the time spent in each, the blocks' own measure and arrange included.
        internal long MeasureTicks { get; private set; }

        internal long ArrangeTicks { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var started = Stopwatch.GetTimestamp();
            var width = availableSize.Width;

            // Text wraps again at another width, and every estimate is for the old one; what was
            // measured at it stays until it is measured again, closer than an estimate would be.
            if (width != _width && !double.IsInfinity(width))
            {
                _width = width;

                for (int i = 0; i < _roots.Count; i++)
                {
                    if (double.IsNaN(_widths[i]))
                    {
                        _values[i] = Owner.EstimateHeight(_roots, i, width);
                        _heights[_roots[i]] = new Height(_values[i], double.NaN);
                    }
                }
            }

            var children = Children;
            var widest = 0d;
            var constraint = new Size(width, double.PositiveInfinity);

            for (int i = 0; i < children.Count; i++)
            {
                var presenter = (RichBlockPresenter)children[i];
                presenter.Measure(constraint);

                var desired = presenter.DesiredSize;
                widest = Math.Max(widest, desired.Width);

                var index = presenter.RootIndex;
                if (index >= 0 && index < _roots.Count && (_values[index] != desired.Height || _widths[index] != width))
                {
                    _values[index] = desired.Height;
                    _widths[index] = width;
                    _heights[_roots[index]] = new Height(desired.Height, width);
                }
            }

            Sum();

            MeasureTicks += Stopwatch.GetTimestamp() - started;
            return new Size(widest, _offsets[_roots.Count]);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var started = Stopwatch.GetTimestamp();
            var children = Children;

            for (int i = 0; i < children.Count; i++)
            {
                var presenter = (RichBlockPresenter)children[i];
                var index = presenter.RootIndex;

                if (index >= 0 && index < _roots.Count)
                {
                    presenter.Arrange(new Rect(0, _offsets[index], finalSize.Width, _values[index]));
                }
            }

            ArrangeTicks += Stopwatch.GetTimestamp() - started;
            return finalSize;
        }

        private void Sum()
        {
            var offset = 0d;

            for (int i = 0; i < _roots.Count; i++)
            {
                _offsets[i] = offset;
                offset += _values[i];
            }

            _offsets[_roots.Count] = offset;
        }
    }
}
