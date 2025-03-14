//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Charts.Data;

namespace Telegram.Charts.DataView
{
    public partial class StackLinearViewData : LineViewData
    {
        public StackLinearViewData(ChartData.Line line)
            : base(line)
        {
            //paint.setStyle(Paint.Style.FILL);
            //if (BaseChartView.USE_LINES)
            //{
            //    paint.setAntiAlias(false);
            //}
        }
    }
}
