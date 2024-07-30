using Telegram.Common;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Stories.Widgets
{
    public sealed partial class StoryWeatherWidget : UserControl
    {
        public StoryWeatherWidget(StoryAreaTypeWeather widget, CornerRadius radius)
        {
            InitializeComponent();

            Label.Text = $"{widget.Emoji} {widget.Temperature}°C";

            RootGrid.Background = new SolidColorBrush(widget.BackgroundColor.ToColor(true));
            RootGrid.CornerRadius = radius;
        }
    }
}
