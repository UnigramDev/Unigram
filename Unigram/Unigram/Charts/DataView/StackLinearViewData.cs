using Unigram.Charts.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
