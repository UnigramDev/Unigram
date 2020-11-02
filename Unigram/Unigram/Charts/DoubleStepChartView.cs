namespace Unigram.Charts
{
    public class DoubleStepChartView : DoubleLinearChartView
    {
        public DoubleStepChartView()
        {
            drawSteps = true;
        }

        protected override float getMinDistance()
        {
            return 0.1f;
        }
    }
}
