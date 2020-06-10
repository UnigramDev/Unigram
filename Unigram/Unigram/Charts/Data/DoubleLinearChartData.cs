using Windows.Data.Json;

namespace Unigram.Charts.Data
{
    public class DoubleLinearChartData : ChartData
    {
        public float[] linesK;

        public DoubleLinearChartData(JsonObject jsonObject)
            : base(jsonObject)
        {
        }

        protected override void measure()
        {
            base.measure();
            int n = lines.Count;
            int max = 0;
            for (int i = 0; i < n; i++)
            {
                int m = lines[i].maxValue;
                if (m > max) max = m;
            }

            linesK = new float[n];

            for (int i = 0; i < n; i++)
            {
                int m = lines[i].maxValue;
                if (max == m)
                {
                    linesK[i] = 1;
                    continue;
                }

                linesK[i] = max / m;
            }
        }
    }
}
