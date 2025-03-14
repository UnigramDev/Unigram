//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
namespace Telegram.Charts
{
    public partial class DoubleStepChartView : DoubleLinearChartView
    {
        public DoubleStepChartView()
        {
            drawSteps = true;
        }

        protected override float GetMinDistance()
        {
            return 0.1f;
        }
    }
}
