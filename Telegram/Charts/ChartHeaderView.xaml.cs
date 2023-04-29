//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Converters;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Telegram.Charts
{
    public sealed partial class ChartHeaderView : UserControl
    {
        public ChartHeaderView()
        {
            InitializeComponent();
        }

        public void SetDates(long v1, long v2)
        {
            var start = Formatter.ToLocalTime(v1 / 1000);
            var end = Formatter.ToLocalTime(v2 / 1000);

            if (Dispatcher.HasThreadAccess)
            {
                Label1.Text = string.Format("{0} - {1}", Formatter.ShortDate.Format(start), Formatter.ShortDate.Format(end));
            }
            else
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => Label1.Text = string.Format("{0} - {1}", Formatter.ShortDate.Format(start), Formatter.ShortDate.Format(end)));
            }
        }

        public void ZoomTo(BaseChartView zoomedChartView, long d, bool v)
        {
            Back.Visibility = Visibility.Visible;
        }

        public void ZoomOut(BaseChartView chartView, bool animated)
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
