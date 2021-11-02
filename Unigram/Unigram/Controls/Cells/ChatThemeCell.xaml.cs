using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Settings;

namespace Unigram.Controls.Cells
{
    public sealed partial class ChatThemeCell : UserControl
    {
        private ChatTheme _theme;

        public ChatThemeCell()
        {
            InitializeComponent();
        }

        public void Update(ChatTheme theme)
        {
            _theme = theme;
            Name.Text = theme?.Name ?? string.Empty;

            var settings = ActualTheme == ElementTheme.Light ? theme.LightSettings : theme.DarkSettings;
            if (settings == null)
            {
                NoTheme.Visibility = Visibility.Visible;

                Preview.Fill = null;
                Outgoing.Fill = null;
                Incoming.Fill = null;
                return;
            }

            NoTheme.Visibility = Visibility.Collapsed;

            if (settings.Background?.Type is BackgroundTypePattern pattern)
            {
                Preview.Fill = pattern.Fill;
            }
            else if (settings.Background?.Type is BackgroundTypeFill fill)
            {
                Preview.Fill = fill.Fill;
            }
            else
            {
                Preview.Fill = null;
            }

            Outgoing.Fill = settings.OutgoingMessageFill;
            Incoming.Fill = new SolidColorBrush(ThemeAccentInfo.Colorize(ActualTheme == ElementTheme.Light ? TelegramThemeType.Day : TelegramThemeType.Tinted, settings.AccentColor.ToColor(), "MessageBackgroundBrush"));
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            Update(_theme);
        }
    }
}
