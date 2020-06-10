using Unigram.Charts.Data;
using Windows.UI;

namespace Unigram.Charts.DataView
{
    public class BarViewData : LineViewData
    {
        public readonly Paint unselectedPaint = new Paint();

        public Color blendColor;

        public BarViewData(ChartData.Line line)
            : base(line)
        {
            //paint.setStyle(Paint.Style.STROKE);
            //unselectedPaint.setStyle(Paint.Style.STROKE);
            //paint.setAntiAlias(false);
        }

        //public void updateColors()
        //{
        //    super.updateColors();
        //}
    }
}
