using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

namespace Unigram.Common
{
    [Flags]
    public enum MosaicItemPosition
    {
        None = 0,
        Top = 1,
        Bottom = 2,
        Left = 4,
        Right = 8,
        Inside = 16,
        Unknown = 65536
    }

    public struct GroupMediaLayout
    {
        public Rect Geometry;
        public MosaicItemPosition Sides = MosaicItemPosition.None;
    };

    public class MosaicAlbumLayout
    {
        public static IList<GroupMediaLayout> LayoutMediaGroup(
            IList<Size> sizes,
            int maxWidth,
            int minWidth,
            int spacing)
        {
            return new MosaicAlbumLayout(sizes, maxWidth, minWidth, spacing).Layout();
        }

        private MosaicAlbumLayout(
            IList<Size> sizes,
            int maxWidth,
            int minWidth,
            int spacing)
        {
            _sizes = sizes;
            _ratios = CountRatios(_sizes);
            _proportions = CountProportions(_ratios);
            _count = _ratios.Count;
            // All apps currently use square max size first.
            // In complex case they use maxWidth * 4 / 3 as maxHeight.
            _maxWidth = maxWidth;
            _maxHeight = maxWidth;
            _minWidth = minWidth;
            _spacing = spacing;
            _averageRatio = (1.0 + _ratios.Sum()) / _count;
            _maxSizeRatio = _maxWidth / (double)_maxHeight;
        }

        public IList<GroupMediaLayout> Layout()
        {
            if (_count == 0)
            {
                return Array.Empty<GroupMediaLayout>();
            }
            else if (_count == 1)
            {
                return LayoutOne();
            }

            if (_count >= 5 || _ratios.Any(x => x > 2))
            {
                return new ComplexLayouter(
                    _ratios,
                    _averageRatio,
                    _maxWidth,
                    _minWidth,
                    _spacing).layout();
            }

            if (_count == 2)
            {
                return LayoutTwo();
            }
            else if (_count == 3)
            {
                return LayoutThree();
            }

            return LayoutFour();
        }

        private static IList<double> CountRatios(IList<Size> sizes)
        {
            return sizes.Select(size => size.Width / size.Height).ToArray();
        }

        private static string CountProportions(IList<double> ratios)
        {
            return string.Join(string.Empty, ratios.Select(ratio => (ratio > 1.2) ? "w" : (ratio < 0.8) ? "n" : "q"));
        }

        private IList<GroupMediaLayout> LayoutTwo()
        {
            if ((_proportions == "ww")
                && (_averageRatio > 1.4 * _maxSizeRatio)
                && (_ratios[1] - _ratios[0] < 0.2))
            {
                return LayoutTwoTopBottom();
            }
            else if (_proportions == "ww" || _proportions == "qq")
            {
                return LayoutTwoLeftRightEqual();
            }
            return LayoutTwoLeftRight();
        }

        private IList<GroupMediaLayout> LayoutThree()
        {
            if (_proportions[0] == 'n')
            {
                return LayoutThreeLeftAndOther();
            }
            return LayoutThreeTopAndOther();
        }

        private IList<GroupMediaLayout> LayoutFour()
        {
            if (_proportions[0] == 'w')
            {
                return LayoutFourTopAndOther();
            }
            return LayoutFourLeftAndOther();
        }

        private IList<GroupMediaLayout> LayoutOne()
        {
            var width = _maxWidth;
            var height = (_sizes[0].Height * width) / _sizes[0].Width;

            return new[]
            {
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, 0, width, height),
                    Sides = MosaicItemPosition.Left | MosaicItemPosition.Top | MosaicItemPosition.Right | MosaicItemPosition.Bottom
                },
            };
        }

        private IList<GroupMediaLayout> LayoutTwoTopBottom()
        {
            var width = _maxWidth;
            var height = Math.Round(Math.Min(width / _ratios[0], Math.Min(width / _ratios[1], (_maxHeight - _spacing) / 2)));

            return new[]
            {
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, 0, width, height),
                    Sides = MosaicItemPosition.Left | MosaicItemPosition.Top | MosaicItemPosition.Right

                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, height + _spacing, width, height),
                    Sides = MosaicItemPosition.Left | MosaicItemPosition.Bottom | MosaicItemPosition.Right
                },
            };
        }

        private IList<GroupMediaLayout> LayoutTwoLeftRightEqual()
        {
            var width = (_maxWidth - _spacing) / 2;
            var height = Math.Round(Math.Min(width / _ratios[0], Math.Min(width / _ratios[1], _maxHeight * 1)));

            return new[]
            {
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, 0, width, height),
                    Sides = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Bottom
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(width + _spacing, 0, width, height),
                    Sides = MosaicItemPosition.Top | MosaicItemPosition.Right | MosaicItemPosition.Bottom
                },
            };
        }

        private IList<GroupMediaLayout> LayoutTwoLeftRight()
        {
            var minimalWidth = Math.Round(_minWidth * 1.5);
            var secondWidth = Math.Min(Math.Round(Math.Max(0.4 * (_maxWidth - _spacing), (_maxWidth - _spacing) / _ratios[0] / (1 / _ratios[0] + 1 / _ratios[1]))), _maxWidth - _spacing - minimalWidth);
            var firstWidth = _maxWidth - secondWidth - _spacing;
            var height = Math.Min(_maxHeight, Math.Round(Math.Min(firstWidth / _ratios[0], secondWidth / _ratios[1])));

            return new[]
            {
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, 0, firstWidth, height),
                    Sides = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Bottom
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(firstWidth + _spacing, 0, secondWidth, height),
                    Sides = MosaicItemPosition.Top | MosaicItemPosition.Right | MosaicItemPosition.Bottom
                },
            };
        }

        private IList<GroupMediaLayout> LayoutThreeLeftAndOther()
        {
            var firstHeight = _maxHeight;
            var thirdHeight = Math.Round(Math.Min(
                (_maxHeight - _spacing) / 2,
                (_ratios[1] * (_maxWidth - _spacing)
                    / (_ratios[2] + _ratios[1]))));
            var secondHeight = firstHeight
                - thirdHeight
                - _spacing;
            var rightWidth = Math.Max(
                _minWidth,
                Math.Round(Math.Min(
                    (_maxWidth - _spacing) / 2,
                    Math.Min(
                        thirdHeight * _ratios[2],
                        secondHeight * _ratios[1]))));
            var leftWidth = Math.Min(
                Math.Round(firstHeight * _ratios[0]),
                _maxWidth - _spacing - rightWidth);

            return new[]
            {
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, 0, leftWidth, firstHeight),
                    Sides = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Bottom
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(leftWidth + _spacing, 0, rightWidth, secondHeight),
                    Sides = MosaicItemPosition.Top | MosaicItemPosition.Right
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(leftWidth + _spacing, secondHeight + _spacing, rightWidth, thirdHeight),
                    Sides = MosaicItemPosition.Bottom | MosaicItemPosition.Right
                },
            };
        }

        private IList<GroupMediaLayout> LayoutThreeTopAndOther()
        {
            var firstWidth = _maxWidth;
            var firstHeight = Math.Round(Math.Min(
                firstWidth / _ratios[0],
                (_maxHeight - _spacing) * 0.66));
            var secondWidth = (_maxWidth - _spacing) / 2;
            var secondHeight = Math.Min(
                _maxHeight - firstHeight - _spacing,
                Math.Round(Math.Min(
                    secondWidth / _ratios[1],
                    secondWidth / _ratios[2])));
            var thirdWidth = firstWidth - secondWidth - _spacing;

            return new[]
            {
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, 0, firstWidth, firstHeight),
                    Sides = MosaicItemPosition.Left | MosaicItemPosition.Top | MosaicItemPosition.Right
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, firstHeight + _spacing, secondWidth, secondHeight),
                    Sides = MosaicItemPosition.Bottom | MosaicItemPosition.Left
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(secondWidth + _spacing, firstHeight + _spacing, thirdWidth, secondHeight),
                    Sides = MosaicItemPosition.Bottom | MosaicItemPosition.Right
                },
            };
        }

        private IList<GroupMediaLayout> LayoutFourLeftAndOther()
        {
            var h = _maxHeight;
            var w0 = Math.Round(Math.Min(
                h * _ratios[0],
                (_maxWidth - _spacing) * 0.6));

            var w = Math.Round(
                (_maxHeight - 2 * _spacing)
                    / (1 / _ratios[1] + 1 / _ratios[2] + 1 / _ratios[3])
            );
            var h0 = Math.Round(w / _ratios[1]);
            var h1 = Math.Round(w / _ratios[2]);
            var h2 = h - h0 - h1 - 2 * _spacing;
            var w1 = Math.Max(
                _minWidth,
                Math.Min(_maxWidth - w0 - _spacing, w));

            return new[]
            {
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, 0, w0, h),
                    Sides = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Bottom
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(w0 + _spacing, 0, w1, h0),
                    Sides = MosaicItemPosition.Top | MosaicItemPosition.Right
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(w0 + _spacing, h0 + _spacing, w1, h1),
                    Sides = MosaicItemPosition.Right
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(w0 + _spacing, h0 + h1 + 2 * _spacing, w1, h2),
                    Sides = MosaicItemPosition.Bottom | MosaicItemPosition.Right
                },
            };
        }

        private IList<GroupMediaLayout> LayoutFourTopAndOther()
        {
            var w = _maxWidth;
            var h0 = Math.Round(Math.Min(
                w / _ratios[0],
                (_maxHeight - _spacing) * 0.66));
            var h = Math.Round(
                (_maxWidth - 2 * _spacing)
                    / (_ratios[1] + _ratios[2] + _ratios[3]));
            var w0 = Math.Max(
                _minWidth,
                Math.Round(Math.Min(
                    (_maxWidth - 2 * _spacing) * 0.4,
                    h * _ratios[1])));
            var w2 = Math.Round(Math.Max(
                Math.Max(
                    _minWidth * 1,
                    (_maxWidth - 2 * _spacing) * 0.33),
                h * _ratios[3]));
            var w1 = w - w0 - w2 - 2 * _spacing;
            var h1 = Math.Min(
                _maxHeight - h0 - _spacing,
                h);

            return new[]
            {
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, 0, w, h0),
                    Sides = MosaicItemPosition.Left | MosaicItemPosition.Top | MosaicItemPosition.Right
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(0, h0 + _spacing, w0, h1),
                    Sides = MosaicItemPosition.Bottom | MosaicItemPosition.Left
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(w0 + _spacing, h0 + _spacing, w1, h1),
                    Sides = MosaicItemPosition.Bottom,
                },
                new GroupMediaLayout
                {
                    Geometry = new Rect(w0 + _spacing + w1 + _spacing, h0 + _spacing, w2, h1),
                    Sides = MosaicItemPosition.Right | MosaicItemPosition.Bottom
                },
            };
        }

        private readonly IList<Size> _sizes;
        private readonly IList<double> _ratios;
        private readonly string _proportions;
        private readonly int _count = 0;
        private readonly int _maxWidth = 0;
        private readonly int _maxHeight = 0;
        private readonly int _minWidth = 0;
        private readonly int _spacing = 0;
        private readonly double _averageRatio = 1;
        private readonly double _maxSizeRatio = 1;
    }

    class ComplexLayouter
    {
        public ComplexLayouter(
            IList<double> ratios,
            double averageRatio,
            int maxWidth,
            int minWidth,
            int spacing)
        {
            _ratios = CropRatios(ratios, averageRatio);
            _count = _ratios.Count;
            // All apps currently use square max size first.
            // In complex case they use maxWidth * 4 / 3 as maxHeight.
            _maxWidth = maxWidth;
            _maxHeight = maxWidth * 4 / 3;
            _minWidth = minWidth;
            _spacing = spacing;
            _averageRatio = averageRatio;
        }

        public IList<GroupMediaLayout> layout()
        {
            var result = new List<GroupMediaLayout>(_count);

            var attempts = new List<Attempt>();
            double multiHeight(int offset, int count)
            {
                var ratios = _ratios.Skip(offset).Take(count);
                var sum = ratios.Sum();
                return (_maxWidth - (count - 1) * _spacing) / sum;
            };
            void pushAttempt(params int[] lineCounts)
            {
                var heights = new List<double>(lineCounts.Length);
                var offset = 0;
                foreach (var count in lineCounts)
                {
                    heights.Add(multiHeight(offset, count));
                    offset += count;
                }
                attempts.Add(new Attempt { LineCounts = lineCounts, Heights = heights });
            };

            for (int first = 1; first != _count; ++first)
            {
                var second = _count - first;
                if (first > 3 || second > 3)
                {
                    continue;
                }
                pushAttempt(first, second);
            }
            for (int firstLine = 1; firstLine != _count - 1; firstLine++)
            {
                for (int secondLine = 1; secondLine != _count - firstLine; secondLine++)
                {
                    var thirdLine = _count - firstLine - secondLine;
                    if (firstLine > 3 || secondLine > (_averageRatio < 0.85 ? 4 : 3) || thirdLine > 3)
                    {
                        continue;
                    }

                    pushAttempt(firstLine, secondLine, thirdLine);
                }
            }
            for (int firstLine = 1; firstLine != _count - 1; firstLine++)
            {
                for (int secondLine = 1; secondLine != _count - firstLine; secondLine++)
                {
                    for (int thirdLine = 1; thirdLine != _count - firstLine - secondLine; thirdLine++)
                    {
                        var fourthLine = _count - firstLine - secondLine - thirdLine;
                        if (firstLine > 3 || secondLine > 3 || thirdLine > 3 || fourthLine > 3)
                        {
                            continue;
                        }

                        pushAttempt(firstLine, secondLine, thirdLine, fourthLine);
                    }
                }
            }

            var optimalAttempt = default(Attempt?);
            var optimalDiff = 0d;
            foreach (var attempt in attempts)
            {
                var heights = attempt.Heights;
                var counts = attempt.LineCounts;
                var lineCount = counts.Count;
                var totalHeight = heights.Sum() + _spacing * (lineCount - 1);
                var minLineHeight = heights.Min();
                var bad1 = (minLineHeight < _minWidth) ? 1.5 : 1;
                double bad2()
                {
                    for (int line = 1; line != lineCount; ++line)
                    {
                        if (counts[line - 1] > counts[line])
                        {
                            return 1.5;
                        }
                    }
                    return 1;
                };
                var diff = Math.Abs(totalHeight - _maxHeight) * bad1 * bad2();
                if (optimalAttempt == null || diff < optimalDiff)
                {
                    optimalAttempt = attempt;
                    optimalDiff = diff;
                }
            }

            var index = 0;
            var y = 0.0;
            if (optimalAttempt is Attempt optimal)
            {
                for (int i = 0; i != optimal.LineCounts.Count; i++)
                {
                    var count = optimal.LineCounts[i];
                    var lineHeight = Math.Round(optimal.Heights[i]);
                    var x = 0.0;

                    for (int k = 0; k != count; k++)
                    {
                        var sides = MosaicItemPosition.None
                            | (i == 0 ? MosaicItemPosition.Top : MosaicItemPosition.None)
                            | (i == optimal.LineCounts.Count - 1 ? MosaicItemPosition.Bottom : MosaicItemPosition.None)
                            | (k == 0 ? MosaicItemPosition.Left : MosaicItemPosition.None)
                            | (k == count - 1 ? MosaicItemPosition.Right : MosaicItemPosition.None);

                        var ratio = _ratios[index];
                        var width = (k == count - 1)
                            ? (_maxWidth - x)
                            : Math.Round(ratio * lineHeight);
                        result.Add(new GroupMediaLayout
                        {
                            Geometry = new Rect(x, y, width, lineHeight),
                            Sides = sides
                        });

                        x += width + _spacing;
                        index++;
                    }

                    y += lineHeight + _spacing;
                }
            }

            return result;
        }

        private struct Attempt
        {
            public IList<int> LineCounts;
            public IList<double> Heights;
        };

        private static IList<double> CropRatios(
            IList<double> ratios,
            double averageRatio)
        {
            return ratios.Select(ratio =>
            {
                var kMaxRatio = 2.75;
                var kMinRatio = 0.6667;
                return (averageRatio > 1.1)
                    ? Math.Clamp(ratio, 1, kMaxRatio)
                    : Math.Clamp(ratio, kMinRatio, 1);
            }).ToArray();
        }

        private readonly IList<double> _ratios;
        private readonly int _count = 0;
        private readonly int _maxWidth = 0;
        private readonly int _maxHeight = 0;
        private readonly int _minWidth = 0;
        private readonly int _spacing = 0;
        private readonly double _averageRatio = 1;
    }
}
