namespace Unigram.Charts.DataView
{
    public class ChartBottomSignatureData
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
