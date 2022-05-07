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
            return session.Type switch
            {
                SessionTypeXbox => ("", Color.FromArgb(0xff, 0x35, 0xc7, 0x59), null),
                SessionTypeChrome => ("\uE96D", Color.FromArgb(0xFF, 0x35, 0xC7, 0x59), "Chrome"),
                SessionTypeBrave => ("", Color.FromArgb(0xFF, 0xFF, 0x95, 0x00), null),
                SessionTypeVivaldi => ("", Color.FromArgb(0xFF, 0xFF, 0x3C, 0x30), null),
                SessionTypeSafari => ("\uE974", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Safari"),
                SessionTypeFirefox => ("\uE96F", Color.FromArgb(0xFF, 0xFF, 0x95, 0x00), "Firefox"),
                SessionTypeOpera => ("", Color.FromArgb(0xFF, 0xFF, 0x3C, 0x30), null),
                SessionTypeAndroid => ("\uE96C", Color.FromArgb(0xFF, 0x35, 0xC7, 0x59), "Android"),
                SessionTypeIphone => ("\uE971", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Iphone"),
                SessionTypeIpad => ("\uE970", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Ipad"),
                SessionTypeMac => ("\uE973", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Mac"),
                SessionTypeUbuntu => ("\uE976", Color.FromArgb(0xFF, 0xFF, 0x95, 0x00), "Ubuntu"),
                SessionTypeLinux => ("\uE972", Color.FromArgb(0xFF, 0x8E, 0x8E, 0x93), "Linux"),
                SessionTypeWindows => ("\uE977", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Windows"),
                _ => ("", Color.FromArgb(0xFF, 0x8E, 0x8E, 0x93), null)
            };
        }
    }
}
