//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Converters;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
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

                Name.Text = "\u200B";
                Title.Text = "\u200B";
                Subtitle.Text = "\u200B";
                return;
            }

            var icon = IconForSession(session);

            Glyph.Text = icon.Glyph;
            GlyphBackground.Background = new SolidColorBrush(icon.Background);

            Name.Text = session.DeviceModel;
            Title.Text = string.Format("{0} {1}", session.ApplicationName, session.ApplicationVersion);
            Subtitle.Text = string.Format("{0} \u2022 {1}", session.Location, session.IsCurrent ? Strings.Online : Formatter.DateExtended(session.LastActiveDate));
        }

        public static (string Glyph, Color Background, string Animation) IconForSession(Session session)
        {
            return session.DeviceType switch
            {
                SessionDeviceTypeXbox => ("", Color.FromArgb(0xff, 0x35, 0xc7, 0x59), null),
                SessionDeviceTypeChrome => ("\uE96D", Color.FromArgb(0xFF, 0x35, 0xC7, 0x59), "Chrome"),
                SessionDeviceTypeBrave => ("", Color.FromArgb(0xFF, 0xFF, 0x95, 0x00), null),
                SessionDeviceTypeVivaldi => ("", Color.FromArgb(0xFF, 0xFF, 0x3C, 0x30), null),
                SessionDeviceTypeSafari => ("\uE974", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Safari"),
                SessionDeviceTypeApple => ("\uE974", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Safari"),
                SessionDeviceTypeFirefox => ("\uE96F", Color.FromArgb(0xFF, 0xFF, 0x95, 0x00), "Firefox"),
                SessionDeviceTypeOpera => ("", Color.FromArgb(0xFF, 0xFF, 0x3C, 0x30), null),
                SessionDeviceTypeAndroid => ("\uE96C", Color.FromArgb(0xFF, 0x35, 0xC7, 0x59), "Android"),
                SessionDeviceTypeIphone => ("\uE971", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Iphone"),
                SessionDeviceTypeIpad => ("\uE970", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Ipad"),
                SessionDeviceTypeMac => ("\uE973", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Mac"),
                SessionDeviceTypeUbuntu => ("\uE976", Color.FromArgb(0xFF, 0xFF, 0x95, 0x00), "Ubuntu"),
                SessionDeviceTypeLinux => ("\uE972", Color.FromArgb(0xFF, 0x8E, 0x8E, 0x93), "Linux"),
                SessionDeviceTypeWindows => ("\uE977", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Windows"),
                SessionDeviceTypeEdge => ("\uE96E", Color.FromArgb(0xFF, 0x00, 0x79, 0xFF), "Edge"),
                _ => ("", Color.FromArgb(0xFF, 0x8E, 0x8E, 0x93), null)
            };
        }
    }
}
