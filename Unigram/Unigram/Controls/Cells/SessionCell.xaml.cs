using Telegram.Td.Api;
using Unigram.Converters;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Cells
{
    public sealed partial class SessionCell : Grid
    {
        public SessionCell()
        {
            InitializeComponent();
        }

        public Session Session
        {
            set => UpdateSession(value);
        }

        public void UpdateSession(Session session)
        {
            if (session == null)
            {
                Glyph.Text = "\uE977";
                GlyphBackground.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x79, 0xFF));

                // TODO: show loading skeleton
                return;
            }

            var icon = IconForSession(session);

            Glyph.Text = icon.Glyph;
            GlyphBackground.Background = new SolidColorBrush(icon.Backgroud);

            Name.Text = session.DeviceModel;
            Title.Text = string.Format("{0} {1}", session.ApplicationName, session.ApplicationVersion);
            Subtitle.Text = string.Format("{0} \u2022 {1}", session.Country, session.IsCurrent ? Strings.Resources.Online : Converter.DateExtended(session.LastActiveDate));
        }

        public static (string Glyph, Color Backgroud, string Animation) IconForSession(Session session)
        {
            var platform = session.Platform.ToLowerInvariant();
            var device = session.DeviceModel.ToLowerInvariant();
            var systemVersion = session.SystemVersion.ToLowerInvariant();

            if (device.Contains("xbox"))
            {
                return ("", Color.FromArgb(0xff, 0x35, 0xc7, 0x59), null);
            }
            if (device.Contains("chrome") && !device.Contains("chromebook"))
            {
                return ("\uE96D", Color.FromArgb(0xFF, 0x35, 0xC7, 0x59), "Chrome");
            }
            if (device.Contains("brave"))
            {
                return ("", Color.FromArgb(0xFF, 0xFF, 0x95, 0x00), null);
            }
            if (device.Contains("vivaldi"))
            {
                return ("", Color.FromArgb(0xFF, 0xFF, 0x3C, 0x30), null);
            }
            if (device.Contains("safari"))
            {
                return ("\uE974", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Safari");
            }
            if (device.Contains("firefox"))
            {
                return ("\uE96F", Color.FromArgb(0xFF, 0xFF, 0x95, 0x00), "Firefox");
            }
            if (device.Contains("opera"))
            {
                return ("", Color.FromArgb(0xFF, 0xFF, 0x3C, 0x30), null);
            }
            if (platform.Contains("android"))
            {
                return ("\uE96C", Color.FromArgb(0xFF, 0x35, 0xC7, 0x59), "Android");
            }
            if (device.Contains("iphone"))
            {
                return ("\uE971", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Iphone");
            }
            if (device.Contains("ipad"))
            {
                return ("\uE970", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Ipad");
            }
            if ((platform.Contains("macos") || systemVersion.Contains("macos")) && device.Contains("mac"))
            {
                return ("\uE973", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Mac");
            }
            if (platform.Contains("ios") || platform.Contains("macos") || systemVersion.Contains("macos"))
            {
                return ("\uE971", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Iphone");
            }
            if (platform.Contains("ubuntu") || systemVersion.Contains("ubuntu"))
            {
                return ("\uE976", Color.FromArgb(0xFF, 0xFF, 0x95, 0x00), "Ubuntu");
            }
            if (platform.Contains("linux") || systemVersion.Contains("linux"))
            {
                return ("\uE972", Color.FromArgb(0xFF, 0x8E, 0x8E, 0x93), "Linux");
            }
            if (platform.Contains("windows") || systemVersion.Contains("windows"))
            {
                return ("\uE977", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Windows");
            }

            return ("", Color.FromArgb(0xFF, 0x8E, 0x8E, 0x93), null);
        }
    }
}
