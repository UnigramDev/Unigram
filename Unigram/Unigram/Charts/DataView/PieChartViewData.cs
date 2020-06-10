using Unigram.Charts.Data;

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
