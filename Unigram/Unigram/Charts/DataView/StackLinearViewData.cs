//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Unigram.Charts.Data;

namespace Unigram.Charts.DataView
{
    public class StackLinearViewData : LineViewData
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
