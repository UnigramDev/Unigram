using Unigram.Charts.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Charts.DataView
{
    public class PieChartViewData : StackLinearViewData
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
