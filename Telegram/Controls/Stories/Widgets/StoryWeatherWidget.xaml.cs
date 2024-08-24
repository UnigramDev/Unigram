using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Telegram.Common;
using Telegram.Td.Api;

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
