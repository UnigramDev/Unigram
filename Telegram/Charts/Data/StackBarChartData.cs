//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Data.Json;

namespace Telegram.Charts.Data
{
    public partial class StackBarChartData : ChartData
    {
        public int[] ySum;
        public SegmentTree ySumSegmentTree;

        public StackBarChartData(JsonObject jsonObject)
                : base(jsonObject)
        {
            Init();
        }

        public void Init()
        {
            int n = lines[0].y.Length;
            int k = lines.Count;

            ySum = new int[n];
            for (int i = 0; i < n; i++)
            {
                ySum[i] = 0;
                for (int j = 0; j < k; j++)
                {
                    ySum[i] += lines[j].y[i];
                }
            }

            ySumSegmentTree = new SegmentTree(ySum);
        }

        public int FindMax(int start, int end)
        {
            return ySumSegmentTree.rMaxQ(start, end);
        }
    }
}
