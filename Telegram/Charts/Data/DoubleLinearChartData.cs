//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Data.Json;

namespace Telegram.Charts.Data
{
    public partial class DoubleLinearChartData : ChartData
    {
        public float[] linesK;

        public DoubleLinearChartData(JsonObject jsonObject)
            : base(jsonObject)
        {
        }

        protected override void Measure()
        {
            base.Measure();
            int n = lines.Count;
            int max = 0;
            for (int i = 0; i < n; i++)
            {
                int m = lines[i].maxValue;
                if (m > max)
                {
                    max = m;
                }
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
