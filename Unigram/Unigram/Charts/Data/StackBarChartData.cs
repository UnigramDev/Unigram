using Windows.Data.Json;

namespace Unigram.Charts.Data
{
    public class StackBarChartData : ChartData
    {
        public int[] ySum;
        public SegmentTree ySumSegmentTree;

        public StackBarChartData(JsonObject jsonObject)
                : base(jsonObject)
        {
            init();
        }

        public void init()
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

        public int findMax(int start, int end)
        {
            return ySumSegmentTree.rMaxQ(start, end);
        }
    }
}
