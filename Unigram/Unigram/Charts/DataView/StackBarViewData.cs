using Unigram.Charts.Data;
using Windows.UI;

namespace Unigram.Charts.DataView
{
    public class StackBarViewData : LineViewData
    {

        public readonly Paint unselectedPaint = new Paint();
        public Color blendColor;

        public StackBarViewData(ChartData.Line line)
            : base(line)
        {
            paint.StrokeWidth = 1;
            //paint.setStyle(Paint.Style.STROKE);
            //unselectedPaint.setStyle(Paint.Style.STROKE);
            //paint.setAntiAlias(false);
        }

        //public void updateColors()
        //{
        //    super.updateColors();
        //}

        public override void updateColors()
        {
            base.updateColors();
            //blendColor = Extensions.blendARGB(Theme.getColor(Theme.key_windowBackgroundWhite), lineColor, 0.3f);
            blendColor = Extensions.blendARGB(Colors.White, lineColor, 0.3f);
        }
    }
}
