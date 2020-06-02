using Telegram.Td.Api;
using Unigram.Converters;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
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

        private StatisticsValue _value;
        public StatisticsValue Value
        {
            get => _value;
            set => Set(value);
        }

        private void Set(StatisticsValue value)
        {
            _value = value;

            if (value == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            ValueLabel.Text = BindConvert.ShortNumber((int)value.Value);

            var diff = value.Value - value.PreviousValue;
            if (diff > 0)
            {
                VisualStateManager.GoToState(this, "Positive", false);
                GrowthLabel.Text = string.Format("+{0} ({1:F2}%)", BindConvert.ShortNumber((int)diff), value.GrowthRatePercentage);
            }
            else if (diff < 0)
            {
                VisualStateManager.GoToState(this, "Negative", false);
                GrowthLabel.Text = string.Format("-{0} ({1:F2}%)", BindConvert.ShortNumber(-(int)diff), -value.GrowthRatePercentage);
            }
        }
    }
}
