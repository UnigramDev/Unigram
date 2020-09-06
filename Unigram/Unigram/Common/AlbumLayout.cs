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

    public static class MosaicAlbumLayout
    {
        private struct MosaicItemInfo
        {
            public Size ImageSize;
            public double AspectRatio;

            public Rect LayoutFrame;
            public MosaicItemPosition Position;
        }

        private struct MosaicLayoutAttempt
        {
            public int[] LineCounts;
            public double[] Heights;
        }

        public static ((Rect, MosaicItemPosition)[], Size) chatMessageBubbleMosaicLayout(Size maxSize, IEnumerable<Size> itemSizes)
        {
            var spacing = 0.0d;

            var proportions = "";
            var averageAspectRatio = 1.0d;
            var forceCalc = false;

            var itemInfos = itemSizes.Select(itemSize =>
            {
                var aspectRatio = itemSize.Width / itemSize.Height;
                if (aspectRatio > 1.2)
                {
                    proportions += "w";
                }
                else if (aspectRatio < 0.8)
                {
                    proportions += "n";
                }
                else
                {
                    proportions += "q";
                }

                if (aspectRatio > 2.0)
                {
                    forceCalc = true;
                }

                averageAspectRatio += aspectRatio;

                return new MosaicItemInfo
                {
                    ImageSize = itemSize,
                    AspectRatio = aspectRatio,
                    LayoutFrame = new Rect()
                };
            }).ToArray();

            var minWidth = 68.0d;
            var minHeight = 81.0;
            var maxAspectRatio = maxSize.Width / maxSize.Height;
            if (itemInfos.Length > 0)
            {
                averageAspectRatio = averageAspectRatio / (double)itemInfos.Length;
            }

            if (!forceCalc)
            {
                if (itemInfos.Length == 2)
                {
                    if (proportions == "ww" && averageAspectRatio > 1.4 * maxAspectRatio && itemInfos[1].AspectRatio - itemInfos[0].AspectRatio < 0.2)
                    {
                        var width = maxSize.Width;
                        var height = Math.Floor(Math.Min(width / itemInfos[0].AspectRatio, Math.Min(width / itemInfos[1].AspectRatio, (maxSize.Height - spacing) / 2.0)));

                        itemInfos[0].LayoutFrame = new Rect(0.0, 0.0, width, height);
                        itemInfos[0].Position = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Right;


                        itemInfos[1].LayoutFrame = new Rect(0.0, height + spacing, width, height);
                        itemInfos[1].Position = MosaicItemPosition.Bottom | MosaicItemPosition.Left | MosaicItemPosition.Right;
                    }
                    else if (proportions == "ww" || proportions == "qq")
                    {
                        var width = (maxSize.Width - spacing) / 2.0;
                        var height = Math.Floor(Math.Min(width / itemInfos[0].AspectRatio, Math.Min(width / itemInfos[1].AspectRatio, maxSize.Height)));

                        itemInfos[0].LayoutFrame = new Rect(0.0, 0.0, width, height);
                        itemInfos[0].Position = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Bottom;


                        itemInfos[1].LayoutFrame = new Rect(width + spacing, 0.0, width, height);
                        itemInfos[1].Position = MosaicItemPosition.Top | MosaicItemPosition.Right | MosaicItemPosition.Bottom;
                    }
                    else
                    {
                        var secondWidth = Math.Floor(Math.Min(0.5 * (maxSize.Width - spacing), Math.Round((maxSize.Width - spacing) / itemInfos[0].AspectRatio / (1.0 / itemInfos[0].AspectRatio + 1.0 / itemInfos[1].AspectRatio))));
                        var firstWidth = maxSize.Width - secondWidth - spacing;
                        var height = Math.Floor(Math.Min(maxSize.Height, Math.Round(Math.Min(firstWidth / itemInfos[0].AspectRatio, secondWidth / itemInfos[1].AspectRatio))));

                        itemInfos[0].LayoutFrame = new Rect(0.0, 0.0, firstWidth, height);
                        itemInfos[0].Position = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Bottom;


                        itemInfos[1].LayoutFrame = new Rect(firstWidth + spacing, 0.0, secondWidth, height);
                        itemInfos[1].Position = MosaicItemPosition.Top | MosaicItemPosition.Right | MosaicItemPosition.Bottom;
                    }
                }
                else if (itemInfos.Length == 3)
                {
                    if (proportions.StartsWith("n"))
                    {
                        var firstHeight = maxSize.Height;

                        var thirdHeight = Math.Min((maxSize.Height - spacing) * 0.5, Math.Round(itemInfos[1].AspectRatio * (maxSize.Width - spacing) / (itemInfos[2].AspectRatio + itemInfos[1].AspectRatio)));
                        var secondHeight = maxSize.Height - thirdHeight - spacing;
                        var rightWidth = Math.Max(minWidth, Math.Min((maxSize.Width - spacing) * 0.5, Math.Round(Math.Min(thirdHeight * itemInfos[2].AspectRatio, secondHeight * itemInfos[1].AspectRatio))));

                        var leftWidth = Math.Round(Math.Min(firstHeight * itemInfos[0].AspectRatio, (maxSize.Width - spacing - rightWidth)));
                        itemInfos[0].LayoutFrame = new Rect(0.0, 0.0, leftWidth, firstHeight);
                        itemInfos[0].Position = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Bottom;


                        itemInfos[1].LayoutFrame = new Rect(leftWidth + spacing, 0.0, rightWidth, secondHeight);
                        itemInfos[1].Position = MosaicItemPosition.Right | MosaicItemPosition.Top;


                        itemInfos[2].LayoutFrame = new Rect(leftWidth + spacing, secondHeight + spacing, rightWidth, thirdHeight);
                        itemInfos[2].Position = MosaicItemPosition.Right | MosaicItemPosition.Bottom;
                    }
                    else
                    {
                        var width = maxSize.Width;
                        var firstHeight = Math.Floor(Math.Min(width / itemInfos[0].AspectRatio, (maxSize.Height - spacing) * 0.66));
                        itemInfos[0].LayoutFrame = new Rect(0.0, 0.0, width, firstHeight);
                        itemInfos[0].Position = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Right;


                        width = (maxSize.Width - spacing) / 2.0;
                        var secondHeight = Math.Min(maxSize.Height - firstHeight - spacing, Math.Round(Math.Min(width / itemInfos[1].AspectRatio, width / itemInfos[2].AspectRatio)));
                        itemInfos[1].LayoutFrame = new Rect(0.0, firstHeight + spacing, width, secondHeight);
                        itemInfos[1].Position = MosaicItemPosition.Left | MosaicItemPosition.Bottom;


                        itemInfos[2].LayoutFrame = new Rect(width + spacing, firstHeight + spacing, width, secondHeight);
                        itemInfos[2].Position = MosaicItemPosition.Right | MosaicItemPosition.Bottom;
                    }
                }
                else if (itemInfos.Length == 4)
                {
                    if (proportions == "wwww" || proportions.StartsWith("w"))
                    {
                        var w = maxSize.Width;
                        var h0 = Math.Round(Math.Min(w / itemInfos[0].AspectRatio, (maxSize.Height - spacing) * 0.66));
                        itemInfos[0].LayoutFrame = new Rect(0.0, 0.0, w, h0);
                        itemInfos[0].Position = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Right;

                        var h = Math.Round((maxSize.Width - 2 * spacing) / (itemInfos[1].AspectRatio + itemInfos[2].AspectRatio + itemInfos[3].AspectRatio));
                        var w0 = Math.Max(minWidth, Math.Min((maxSize.Width - 2 * spacing) * 0.4, h * itemInfos[1].AspectRatio));
                        var w2 = Math.Max(Math.Max(minWidth, (maxSize.Width - 2 * spacing) * 0.33), h * itemInfos[3].AspectRatio);
                        var w1 = w - w0 - w2 - 2 * spacing;
                        h = Math.Max(minHeight, Math.Min(maxSize.Height - h0 - spacing, h));
                        itemInfos[1].LayoutFrame = new Rect(0.0, h0 + spacing, w0, h);
                        itemInfos[1].Position = MosaicItemPosition.Left | MosaicItemPosition.Bottom;

                        itemInfos[2].LayoutFrame = new Rect(w0 + spacing, h0 + spacing, w1, h);
                        itemInfos[2].Position = MosaicItemPosition.Bottom;

                        itemInfos[3].LayoutFrame = new Rect(w0 + w1 + 2 * spacing, h0 + spacing, w2, h);
                        itemInfos[3].Position = MosaicItemPosition.Right | MosaicItemPosition.Bottom;
                    }
                    else
                    {
                        var h = maxSize.Height;
                        var w0 = Math.Round(Math.Min(h * itemInfos[0].AspectRatio, (maxSize.Width - spacing) * 0.6));
                        itemInfos[0].LayoutFrame = new Rect(0.0, 0.0, w0, h);
                        itemInfos[0].Position = MosaicItemPosition.Top | MosaicItemPosition.Left | MosaicItemPosition.Bottom;

                        var w = Math.Round((maxSize.Height - 2 * spacing) / (1.0 / itemInfos[1].AspectRatio + 1.0 / itemInfos[2].AspectRatio + 1.0 / itemInfos[3].AspectRatio));
                        var h0 = Math.Floor(w / itemInfos[1].AspectRatio);
                        var h1 = Math.Floor(w / itemInfos[2].AspectRatio);
                        var h2 = h - h0 - h1 - 2.0 * spacing;
                        w = Math.Max(minWidth, Math.Min(maxSize.Width - w0 - spacing, w));
                        itemInfos[1].LayoutFrame = new Rect(w0 + spacing, 0.0, w, h0);
                        itemInfos[1].Position = MosaicItemPosition.Right | MosaicItemPosition.Top;


                        itemInfos[2].LayoutFrame = new Rect(w0 + spacing, h0 + spacing, w, h1);
                        itemInfos[2].Position = MosaicItemPosition.Right;


                        itemInfos[3].LayoutFrame = new Rect(w0 + spacing, h0 + h1 + 2 * spacing, w, h2);
                        itemInfos[2].Position = MosaicItemPosition.Right | MosaicItemPosition.Bottom;
                    }
                }
            }

            if (forceCalc || itemInfos.Length >= 5)
            {
                var croppedRatios = new List<double>();
                foreach (var itemInfo in itemInfos)
                {
                    var aspectRatio = itemInfo.AspectRatio;
                    var croppedRatio = aspectRatio;
                    if (averageAspectRatio > 1.1)
                    {
                        croppedRatio = Math.Max(1.0, aspectRatio);
                    }
                    else
                    {
                        croppedRatio = Math.Min(1.0, aspectRatio);
                    }

                    croppedRatio = Math.Max(0.66667, Math.Min(1.7, croppedRatio));
                    croppedRatios.Add(croppedRatio);
                }

                double multiHeight(IEnumerable<double> ratios)
                {
                    var count = 0;
                    var ratioSum = 0.0;
                    foreach (var ratio in ratios)
                    {
                        count++;
                        ratioSum += ratio;
                    }

                    return (maxSize.Width - (double)(count - 1) * spacing) / ratioSum;
                }

                var attempts = new List<MosaicLayoutAttempt>();
                void addAttempt(int[] lineCounts, double[] heights, ref List<MosaicLayoutAttempt> attempts)
                {
                    attempts.Add(new MosaicLayoutAttempt { LineCounts = lineCounts, Heights = heights });
                }

                for (int firstLine = 1; firstLine < croppedRatios.Count; firstLine++)
                {
                    var secondLine = croppedRatios.Count - firstLine;
                    if (firstLine > 3 || secondLine > 3)
                    {
                        continue;
                    }

                    addAttempt(new int[] { firstLine, croppedRatios.Count - firstLine }, new double[] {
                        multiHeight(croppedRatios.Take(firstLine)),
                        multiHeight(croppedRatios.Skip(firstLine).Take(croppedRatios.Count - firstLine)) }, ref attempts);
                }

                for (int firstLine = 1; firstLine < croppedRatios.Count - 1; firstLine++)
                {
                    for (int secondLine = 1; secondLine < croppedRatios.Count - firstLine; secondLine++)
                    {
                        var thirdLine = croppedRatios.Count - firstLine - secondLine;
                        if (firstLine > 3 || secondLine > (averageAspectRatio < 0.85 ? 4 : 3) || thirdLine > 3)
                        {
                            continue;
                        }

                        addAttempt(new int[] { firstLine, secondLine, thirdLine }, new double[] {
                            multiHeight(croppedRatios.Take(firstLine)),
                            multiHeight(croppedRatios.Skip(firstLine).Take(croppedRatios.Count - firstLine - thirdLine)),
                            multiHeight(croppedRatios.Skip(firstLine + secondLine).Take(croppedRatios.Count - firstLine - secondLine)) }, ref attempts);
                    }
                }

                if (croppedRatios.Count - 2 >= 1)
                {
                    outer: for (int firstLine = 1; firstLine < croppedRatios.Count - 2; firstLine++)
                    {
                        if (croppedRatios.Count - firstLine < 1)
                        {
                            goto outer;
                        }
                        for (int secondLine = 1; secondLine < croppedRatios.Count - firstLine; secondLine++)
                        {
                            for (int thirdLine = 1; thirdLine < croppedRatios.Count - firstLine - secondLine; thirdLine++)
                            {
                                var fourthLine = croppedRatios.Count - firstLine - secondLine - thirdLine;
                                if (firstLine > 3 || secondLine > 3 || thirdLine > 3 || fourthLine > 3)
                                {
                                    continue;
                                }

                                addAttempt(new int[] { firstLine, secondLine, thirdLine, fourthLine }, new double[] {
                                    multiHeight(croppedRatios.Take(firstLine)),
                                    multiHeight(croppedRatios.Skip(firstLine).Take(croppedRatios.Count - firstLine - thirdLine - fourthLine)),
                                    multiHeight(croppedRatios.Skip(firstLine + secondLine).Take(croppedRatios.Count - firstLine - secondLine - fourthLine)),
                                    multiHeight(croppedRatios.Skip(firstLine + secondLine + thirdLine).Take(croppedRatios.Count - firstLine - secondLine - thirdLine)) }, ref attempts);
                            }
                        }
                    }
                }

                var maxHeight = Math.Floor(maxSize.Width / 3.0 * 4.0);
                var optimalo = default(MosaicLayoutAttempt?);
                var optimalDiff = 0.0;
                foreach (var attempt in attempts)
                {
                    var totalHeight = spacing * (double)(attempt.Heights.Length - 1);
                    var minLineHeight = double.MaxValue;
                    var maxLineHeight = 0.0;
                    foreach (var h in attempt.Heights)
                    {
                        totalHeight += Math.Floor(h);
                        if (totalHeight < minLineHeight)
                        {
                            minLineHeight = totalHeight;
                        }
                        if (totalHeight > maxLineHeight)
                        {
                            maxLineHeight = totalHeight;
                        }
                    }

                    var diff = Math.Abs(totalHeight - maxHeight);

                    if (attempt.LineCounts.Length > 1)
                    {
                        if ((attempt.LineCounts[0] > attempt.LineCounts[1]) || (attempt.LineCounts.Length > 2 && attempt.LineCounts[1] > attempt.LineCounts[2]) || (attempt.LineCounts.Length > 3 && attempt.LineCounts[2] > attempt.LineCounts[3]))
                        {
                            diff *= 1.5;
                        }
                    }

                    if (minLineHeight < minWidth)
                    {
                        diff *= 1.5;
                    }

                    if (optimalo == null || diff < optimalDiff)
                    {
                        optimalo = attempt;
                        optimalDiff = diff;
                    }
                }

                var index = 0;
                var y = 0.0;
                if (optimalo is MosaicLayoutAttempt optimal)
                {
                    for (int i = 0; i < optimal.LineCounts.Length; i++)
                    {
                        var count = optimal.LineCounts[i];
                        var lineHeight = Math.Ceiling(optimal.Heights[i]);
                        var x = 0.0;



                        var positionFlags = MosaicItemPosition.None;
                        if (i == 0)
                        {
                            positionFlags |= MosaicItemPosition.Top;
                        }
                        if (i == optimal.LineCounts.Length - 1)
                        {
                            positionFlags |= MosaicItemPosition.Bottom;
                        }

                        for (int k = 0; k < count; k++)
                        {
                            var innerPositionFlags = positionFlags;

                            if (k == 0)
                            {
                                innerPositionFlags |= MosaicItemPosition.Left;
                            }
                            if (k == count - 1)
                            {
                                innerPositionFlags |= MosaicItemPosition.Right;
                            }

                            if (positionFlags == MosaicItemPosition.None)
                            {
                                innerPositionFlags = MosaicItemPosition.Inside;
                            }

                            var ratio = croppedRatios[index];
                            var width = Math.Ceiling(ratio * lineHeight);
                            itemInfos[index].LayoutFrame = new Rect(x, y, width, lineHeight);
                            itemInfos[index].Position = innerPositionFlags;


                            x += width + spacing;
                            index += 1;
                        }

                        y += lineHeight + spacing;
                    }

                    index = 0;
                    var maxWidth = 0.0;
                    for (int i = 0; i < optimal.LineCounts.Length; i++)
                    {
                        var count = optimal.LineCounts[i];
                        for (int k = 0; k < count; k++)
                        {
                            if (k == count - 1)
                            {
                                maxWidth = Math.Max(maxWidth, itemInfos[index].LayoutFrame.Right);
                            }
                            index += 1;
                        }
                    }

                    index = 0;
                    for (int i = 0; i < optimal.LineCounts.Length; i++)
                    {
                        var count = optimal.LineCounts[i];
                        for (int k = 0; k < count; k++)
                        {
                            if (k == count - 1)
                            {
                                var frame = itemInfos[index].LayoutFrame;
                                frame.Width = Math.Max(frame.Width, maxWidth - frame.Left);
                                itemInfos[index].LayoutFrame = frame;
                            }
                            index += 1;
                        }
                    }
                }
            }

            var dimensions = new Size();
            foreach (var itemInfo in itemInfos)
            {
                dimensions.Width = Math.Max(dimensions.Width, Math.Round(itemInfo.LayoutFrame.Right));
                dimensions.Height = Math.Max(dimensions.Height, Math.Round(itemInfo.LayoutFrame.Bottom));
            }

            return (itemInfos.Select(x => (x.LayoutFrame, x.Position)).ToArray(), dimensions);
        }
    }
}
