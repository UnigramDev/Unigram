﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Foundation;

namespace Unigram.Common
{
    public class MosaicMediaPosition
    {
        public object Item { get; private set; }

        public double Width { get; private set; }

        public MosaicMediaPosition(object item, double width)
        {
            Item = item;
            Width = width;
        }
    }

    public class MosaicMedia
    {
        private IList _items;

        private Dictionary<int, int> itemSpans = new Dictionary<int, int>();
        private Dictionary<int, int> itemsToRow = new Dictionary<int, int>();
        private List<List<int>> rows;
        private double lineHeight = 100;

        private MosaicMedia(IList items)
        {
            _items = items;
        }

        public static IList<IList<MosaicMediaPosition>> Calculate(IList items)
        {
            var width = 320;
            var mosaic = new MosaicMedia(items);
            var preferredRowSize = mosaic.PrepareLayout(width);

            var result = new IList<MosaicMediaPosition>[mosaic.rows.Count];

            var index = 0;
            for (int i = 0; i < mosaic.rows.Count; i++)
            {
                result[i] = new MosaicMediaPosition[mosaic.rows[i].Count];

                for (int j = 0; j < mosaic.rows[i].Count; j++)
                {
                    result[i][j] = new MosaicMediaPosition(items[index], mosaic.itemSpans[index] / preferredRowSize);
                    index++;
                }
            }

            return result;
        }

        private double PrepareLayout(double viewPortAvailableSize)
        {
            itemSpans.Clear();
            itemsToRow.Clear();
            int preferredRowSize = Math.Min(getSpanCount(viewPortAvailableSize), 100);

            double totalItemSize = 0;
            int itemsCount = getFlowItemCount();
            int[] weights = new int[itemsCount];
            for (int j = 0; j < itemsCount; j++)
            {
                Size size = SizeForItem(j);
                totalItemSize += (size.Width / size.Height) * preferredRowSize;
                weights[j] = (int)Math.Round(size.Width / size.Height * 100);
            }

            int numberOfRows = (int)Math.Max(Math.Round(totalItemSize / viewPortAvailableSize), 1);

            rows = GetLinearPartitionForSequence(weights, numberOfRows);

            int i = 0, a;
            for (a = 0; a < rows.Count; a++)
            {
                List<int> row = rows[a];

                double summedRatios = 0;
                for (int j = i, n = i + row.Count; j < n; j++)
                {
                    Size preferredSize = SizeForItem(j);
                    summedRatios += preferredSize.Width / preferredSize.Height;
                }

                double rowSize = viewPortAvailableSize;

                if (rows.Count == 1 && a == rows.Count - 1)
                {
                    if (row.Count < 2)
                    {
                        rowSize = (float)Math.Floor(viewPortAvailableSize / 3.0f);
                    }
                    else if (row.Count < 3)
                    {
                        rowSize = (float)Math.Floor(viewPortAvailableSize * 2.0f / 3.0f);
                    }
                }

                int spanLeft = getSpanCount(viewPortAvailableSize);
                for (int j = i, n = i + row.Count; j < n; j++)
                {
                    Size preferredSize = SizeForItem(j);
                    int width = (int)Math.Round(rowSize / summedRatios * (preferredSize.Width / preferredSize.Height));
                    int itemSpan;
                    if (itemsCount < 3 || j != n - 1)
                    {
                        itemSpan = (int)(width / viewPortAvailableSize * getSpanCount(viewPortAvailableSize));
                        spanLeft -= itemSpan;
                    }
                    else
                    {
                        itemsToRow[j] = a;
                        itemSpan = spanLeft;
                    }
                    itemSpans[j] = itemSpan;
                }
                i += row.Count;
            }

            return preferredRowSize;
        }

        private int[] GetLinearPartitionTable(int[] sequence, int numPartitions)
        {
            int n = sequence.Length;
            int i, j, x;

            int[] tmpTable = new int[n * numPartitions];
            int[] solution = new int[(n - 1) * (numPartitions - 1)];

            for (i = 0; i < n; i++)
            {
                tmpTable[i * numPartitions] = sequence[i] + (i != 0 ? tmpTable[(i - 1) * numPartitions] : 0);
            }

            for (j = 0; j < numPartitions; j++)
            {
                tmpTable[j] = sequence[0];
            }

            for (i = 1; i < n; i++)
            {
                for (j = 1; j < numPartitions; j++)
                {
                    int currentMin = 0;
                    int minX = int.MaxValue;

                    for (x = 0; x < i; x++)
                    {
                        int cost = Math.Max(tmpTable[x * numPartitions + (j - 1)], tmpTable[i * numPartitions] - tmpTable[x * numPartitions]);
                        if (x == 0 || cost < currentMin)
                        {
                            currentMin = cost;
                            minX = x;
                        }
                    }
                    tmpTable[i * numPartitions + j] = currentMin;
                    solution[(i - 1) * (numPartitions - 1) + (j - 1)] = minX;
                }
            }

            return solution;
        }

        private List<List<int>> GetLinearPartitionForSequence(int[] sequence, int numberOfPartitions)
        {
            int n = sequence.Length;
            int k = numberOfPartitions;

            if (k <= 0)
            {
                return new List<List<int>>();
            }

            if (k >= n || n == 1)
            {
                List<List<int>> partition = new List<List<int>>(sequence.Length);
                for (int i = 0; i < sequence.Length; i++)
                {
                    List<int> arrayList = new List<int>(1);
                    arrayList.Add(sequence[i]);
                    partition.Add(arrayList);
                }
                return partition;
            }

            int[] solution = GetLinearPartitionTable(sequence, numberOfPartitions);
            int solutionRowSize = numberOfPartitions - 1;

            k = k - 2;
            n = n - 1;
            List<List<int>> answer = new List<List<int>>();

            while (k >= 0)
            {
                if (n < 1)
                {
                    answer.Insert(0, new List<int>());
                }
                else
                {
                    List<int> currentAnswer1 = new List<int>();
                    for (int i = solution[(n - 1) * solutionRowSize + k] + 1, range = n + 1; i < range; i++)
                    {
                        currentAnswer1.Add(sequence[i]);
                    }
                    answer.Insert(0, currentAnswer1);
                    n = solution[(n - 1) * solutionRowSize + k];
                }
                k = k - 1;
            }

            List<int> currentAnswer = new List<int>();
            for (int i = 0, range = n + 1; i < range; i++)
            {
                currentAnswer.Add(sequence[i]);
            }
            answer.Insert(0, currentAnswer);
            return answer;
        }










        private Size SizeForItem(int i)
        {
            Size size = GetSizeForItem(i);
            if (size.Width == 0)
            {
                size.Width = 100;
            }
            if (size.Height == 0)
            {
                size.Height = 100;
            }
            double aspect = size.Width / size.Height;
            if (aspect > 4.0f || aspect < 0.6f)
            {
                size.Height = size.Width = Math.Max(size.Width, size.Height);
            }
            return size;
        }

        protected Size GetSizeForItem(int i)
        {
            if (_items[i] is TLDocument document)
            {
                var size = new Size(100, 100);
                if (document.Thumb is TLPhotoSize photoSize)
                {
                    size.Width = photoSize.W;
                    size.Height = photoSize.H;
                }
                else if (document.Thumb is TLPhotoCachedSize photoCachedSize)
                {
                    size.Width = photoCachedSize.W;
                    size.Height = photoCachedSize.H;
                }

                for (int j = 0; j < document.Attributes.Count; j++)
                {
                    if (document.Attributes[j] is TLDocumentAttributeVideo videoAttribute)
                    {
                        size.Width = videoAttribute.W;
                        size.Height = videoAttribute.H;
                        break;
                    }
                    else if (document.Attributes[j] is TLDocumentAttributeImageSize imageSizeAttribute)
                    {
                        size.Width = imageSizeAttribute.W;
                        size.Height = imageSizeAttribute.H;
                        break;
                    }
                }

                return size;
            }

            //    TLRPC.Document document = recentGifs.get(i);
            //    size.width = document.thumb != null && document.thumb.w != 0 ? document.thumb.w : 100;
            //    size.height = document.thumb != null && document.thumb.h != 0 ? document.thumb.h : 100;
            //    for (int b = 0; b < document.attributes.size(); b++)
            //    {
            //        TLRPC.DocumentAttribute attribute = document.attributes.get(b);
            //        if (attribute instanceof TLRPC.TL_documentAttributeImageSize || attribute instanceof TLRPC.TL_documentAttributeVideo) {
            //        size.width = attribute.w;
            //        size.height = attribute.h;
            //        break;
            //    }
            //}
            //                return size;

            return new Size(100, 100);
        }

        protected int getSpanCount(double viewPortAvailableSize)
        {
            return (int)(viewPortAvailableSize / 5d);
        }

        protected int getFlowItemCount()
        {
            return _items.Count;
            //return getItemCount();
        }
    }
}
