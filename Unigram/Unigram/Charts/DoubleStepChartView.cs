namespace Unigram.Charts
{
    public class DoubleStepChartView : DoubleLinearChartView
    {
        public DoubleStepChartView()
        {
            drawSteps = true;
        }

        protected override float GetMinDistance()
        {
            return 0.1f;
        }
    }
}
