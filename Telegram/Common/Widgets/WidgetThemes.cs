//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Native.Widgets;
using Telegram.Navigation;
using Telegram.Services;
using Windows.UI;
using Windows.UI.Xaml.Media;

namespace Telegram.Common.Widgets
{
    // What a widget's theme is made of, for a widget to be sent a new one only when it changed.
    public readonly record struct WidgetThemeKey(bool Dark, double FontSize, int Version);

    // The chat's theme for a widget, from the bubble it sits in (widget-sandbox docs/sdk-v1.md §2.3,
    // §5.1): every one of the 15 Mini Apps keys, as #rrggbb.
    public static class WidgetThemes
    {
        // The message text's line height in Segoe UI, as a multiple of the font size.
        private const double LineHeight = 1.33;

        private static int _version;

        // A chat theme changed the bubbles' brushes, which have no version of their own. UI thread.
        public static void Invalidate()
        {
            _version++;
        }

        public static WidgetThemeKey Key(bool dark)
        {
            return new WidgetThemeKey(dark, FontSize, _version);
        }

        // The message text's size in DIPs, which are CSS px here.
        private static double FontSize => AppSettings.Appearance.MessageFontSize * BootStrapper.Current.TextScaleFactor;

        public static WidgetTheme Create(MessageBrushes brushes, bool dark)
        {
            var palette = dark ? brushes.Dark : brushes.Light;
            var background = palette["MessageBackgroundBrush"].Color;

            var theme = new WidgetTheme();
            theme.ColorScheme = dark ? "dark" : "light";

            var values = theme.Params;
            values["bg_color"] = Hex(background, background);
            values["text_color"] = Hex(palette, "MessageForegroundBrush", background);
            values["hint_color"] = Hex(palette, "MessageSubtleForegroundBrush", background);
            values["link_color"] = Hex(palette, "MessageForegroundLinkBrush", background);
            values["button_color"] = Hex(palette, "MessageAccentBrush", background);
            values["button_text_color"] = Hex(palette, "MessageMediaForegroundBrush", background);
            values["secondary_bg_color"] = Hex(palette, "MessageHeaderBackgroundBrush", background);
            values["accent_text_color"] = Hex(palette, "MessageHeaderForegroundBrush", background);
            values["subtitle_text_color"] = Hex(palette, "MessageSubtleLabelBrush", background);
            values["destructive_text_color"] = Hex(palette, "MessageCallMissedForegroundBrush", background);

            // No bubble counterpart: the closest colour, so that Mini Apps code works.
            values["section_bg_color"] = values["bg_color"];
            values["section_header_text_color"] = values["accent_text_color"];
            values["section_separator_color"] = Hex(palette, "MessageElevationBrush", background);
            values["header_bg_color"] = values["bg_color"];
            values["bottom_bar_bg_color"] = values["bg_color"];

            // The app's own button: 4 px corners, semibold body text. The framework takes generic
            // font families only; system-ui is Segoe UI here.
            theme.ButtonRadius = "4px";
            theme.ButtonFont = "600 14px system-ui";

            // The widget's text is the message text: the chat's text size, inset as a caption is from the bubble's edges.
            theme.TextFontSize = FontSize;
            theme.TextLineHeight = LineHeight;
            theme.TextPadding = "6px 10px";
            return theme;
        }

        private static string Hex(Dictionary<string, SolidColorBrush> palette, string key, Color background)
        {
            return Hex(palette[key].Color, background);
        }

        // The palette is opaque only: a translucent brush is flattened over the bubble.
        private static string Hex(Color color, Color background)
        {
            var a = color.A / 255.0;
            var r = (byte)(color.R * a + background.R * (1 - a) + 0.5);
            var g = (byte)(color.G * a + background.G * (1 - a) + 0.5);
            var b = (byte)(color.B * a + background.B * (1 - a) + 0.5);
            return $"#{r:x2}{g:x2}{b:x2}";
        }
    }
}
