using Unigram.Common;
using Unigram.Converters;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Charts
{
    public sealed partial class ChartHeaderView : UserControl
    {
        public ChartHeaderView()
        {
            InitializeComponent();
        }

        public void setDates(long v1, long v2)
        {
            var start = Utils.UnixTimestampToDateTime(v1 / 1000);
            var end = Utils.UnixTimestampToDateTime(v2 / 1000);

            if (Dispatcher.HasThreadAccess)
            {
                Label1.Text = string.Format("{0} - {1}", BindConvert.Current.ShortDate.Format(start), BindConvert.Current.ShortDate.Format(end));
            }
            else
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => Label1.Text = string.Format("{0} - {1}", BindConvert.Current.ShortDate.Format(start), BindConvert.Current.ShortDate.Format(end)));
            }
        }

        public void zoomTo(BaseChartView zoomedChartView, long d, bool v)
        {
            Back.Visibility = Visibility.Visible;
        }

        public void zoomOut(BaseChartView chartView, bool animated)
        {
            Back.Visibility = Visibility.Collapsed;
        }

        public event RoutedEventHandler Click;

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(sender, e);
        }
    }
}
