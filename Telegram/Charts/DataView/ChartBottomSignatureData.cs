//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Charts.DataView
{
    public partial class ChartBottomSignatureData
    {
        public readonly int step;
        public readonly int stepMax;
        public readonly int stepMin;

        public int alpha;

        public int fixedAlpha = 255;

        public ChartBottomSignatureData(int step, int stepMax, int stepMin)
        {
            this.step = step;
            this.stepMax = stepMax;
            this.stepMin = stepMin;
        }
    }
}
