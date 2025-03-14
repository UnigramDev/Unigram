//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Charts.Data;

namespace Telegram.Charts.DataView
{
    public partial class PieChartViewData : StackLinearViewData
    {
        public float selectionA;
        public float drawingPart;
        public Animator animator;

        public PieChartViewData(ChartData.Line line)
            : base(line)
        {
        }
    }
}
