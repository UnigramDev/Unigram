//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Td.Api;
using Windows.UI;
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

            var background = widget.BackgroundColor.ToColor(true);
            var luminance = 0.2126 * (background.R / 255d) + 0.7152 * (background.G / 255d) + 0.0722 * (background.B / 255d);
            var foreground = luminance > 0.5 ? Colors.Black : Colors.White;

            Label.Text = $"{widget.Emoji} {widget.Temperature}°C";
            Label.Foreground = new SolidColorBrush(foreground);

            RootGrid.Background = new SolidColorBrush(background);
            RootGrid.CornerRadius = radius;
        }
    }
}
