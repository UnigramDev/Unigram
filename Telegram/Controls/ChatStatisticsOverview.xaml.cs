//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Converters;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public sealed partial class ChatStatisticsOverview : UserControl
    {
        public ChatStatisticsOverview()
        {
            InitializeComponent();
        }

        public string Title
        {
            get => TitleLabel.Text;
            set => TitleLabel.Text = value;
        }

        private double _percentage;
        public double Percentage
        {
            get => _percentage;
            set => SetPercentage(value);
        }

        private void SetPercentage(double value)
        {
            _percentage = value;
            ValueLabel.Text = string.Format("{0:0.0}%", value);
        }

        private StatisticalValue _value;
        public StatisticalValue Value
        {
            get => _value;
            set => Set(value);
        }

        private void Set(StatisticalValue value)
        {
            _value = value;

            if (value == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            ValueLabel.Text = Formatter.ShortNumber((int)value.Value);

            var diff = value.Value - value.PreviousValue;
            if (diff > 0)
            {
                VisualStateManager.GoToState(this, "Positive", false);
                GrowthLabel.Text = string.Format("+{0} ({1:F2}%)", Formatter.ShortNumber((int)diff), value.GrowthRatePercentage);
            }
            else if (diff < 0)
            {
                VisualStateManager.GoToState(this, "Negative", false);
                GrowthLabel.Text = string.Format("-{0} ({1:F2}%)", Formatter.ShortNumber(-(int)diff), -value.GrowthRatePercentage);
            }
        }
    }
}
