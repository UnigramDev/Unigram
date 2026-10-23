//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Services;
using Telegram.Streams;
using Windows.Foundation;
using Windows.System.UserProfile;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Stories.Widgets
{
    /// <summary>
    /// A story's weather, in the story viewer and in the editor alike: the emoji and the
    /// temperature on a rounded fill. Its story area holds all of it.
    /// </summary>
    public sealed partial class StoryWeatherWidget : UserControl
    {
        private string _emoji;

        private Color _background;
        private bool _hasBackground;

        public StoryWeatherWidget()
        {
            InitializeComponent();
        }

        public void Update(IClientService clientService, string emoji, double temperature, Color background)
        {
            if (_emoji != emoji)
            {
                _emoji = emoji;
                Emoji.Source = clientService != null && !string.IsNullOrEmpty(emoji)
                    ? new AnimatedEmojiFileSource(clientService, emoji)
                    : null;
            }

            Label.Text = FormatTemperature(temperature);

            UpdateBackground(background);
        }

        public void UpdateBackground(Color background)
        {
            if (_hasBackground && _background == background)
            {
                return;
            }

            _background = background;
            _hasBackground = true;

            // By the fill's own colour, whatever its alpha: a translucent black still takes white.
            var brightness = (background.R * 0.2126 + background.G * 0.7152 + background.B * 0.0722) / 255;

            RootGrid.Background = new SolidColorBrush(background);
            Label.Foreground = new SolidColorBrush(brightness >= 0.721 ? Colors.Black : Colors.White);
        }

        /// <summary>
        /// The size it lays out at, worked out without laying it out: measuring the whole of it
        /// would load the emoji's animation, which needs the window.
        /// </summary>
        public Size GetNaturalSize()
        {
            Label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var padding = RootGrid.Padding;
            var emoji = EmojiBox.Margin.Left + EmojiBox.Width + EmojiBox.Margin.Right;

            return new Size(
                padding.Left + emoji + Label.DesiredSize.Width + padding.Right,
                padding.Top + Math.Max(EmojiBox.Height, LabelBox.Height) + padding.Bottom);
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RootGrid.CornerRadius = new CornerRadius(e.NewSize.Height * 0.2);
        }

        /// <summary>
        /// Whether temperatures read in Fahrenheit here: in the United States and the few other
        /// places that use them. Decided by the home region; the other clients go by the time zone.
        /// </summary>
        public static bool IsFahrenheit
        {
            get
            {
                var region = GlobalizationPreferences.HomeGeographicRegion;
                return region is "US" or "BS" or "BZ" or "KY" or "PW";
            }
        }

        /// <summary>
        /// Whole degrees, rounded half up, in the unit <see cref="IsFahrenheit"/> picks.
        /// </summary>
        public static string FormatTemperature(double celsius)
        {
            return IsFahrenheit
                ? $"{(int)Math.Floor(celsius * 9 / 5 + 32 + 0.5)}°F"
                : $"{(int)Math.Floor(celsius + 0.5)}°C";
        }
    }
}
