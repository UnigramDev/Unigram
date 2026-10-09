//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Telegram.Services
{
    public partial class ThemeCustomInfo : ThemeAccentInfo
    {
        public ThemeCustomInfo(TelegramTheme parent, Color accent, string name)
            : base(TelegramThemeType.Custom, accent, null, null)
        {
            Parent = parent;
            Name = name;
        }

        private ThemeCustomInfo(string path, Color accent, Dictionary<string, Color> values, Dictionary<AccentShade, Color> shades)
            : base(TelegramThemeType.Custom, accent, values, shades)
        {
        }

        public string Path { get; set; }

        // None of the readers throw: a theme file is user input, often one received in a chat, and
        // a file that cannot be read or parsed is simply not a theme. Null is the answer.

        public static ThemeCustomInfo FromFile(string path)
        {
            try
            {
                return FromFile(path, System.IO.File.ReadAllLines(path));
            }
            catch
            {
                return null;
            }
        }

        public static async Task<ThemeCustomInfo> FromFileAsync(IClientService clientService, StorageFile file)
        {
            IList<string> lines;

            try
            {
                lines = await FileIO.ReadLinesAsync(file);
            }
            catch
            {
                return null;
            }

            var theme = FromFile(file.Path, lines);
            if (theme?.Settings?.Background is { Type: BackgroundTypeWallpaper or BackgroundTypePattern })
            {
                if (await clientService.SendAsync(new SearchBackground(theme.Settings.Background.Name)) is Background resolved)
                {
                    theme.Settings.Background = new Background(resolved.Id, resolved.IsDefault, resolved.IsDark, resolved.Name, resolved.Document, theme.Settings.Background.Type);
                }
            }

            return theme;
        }

        /// <summary>
        /// The header of a v2 file: the settings it is colorized from before its own values go on
        /// top, as a variant is. Null for a v1 file, which is the plain lookup of its parent with its
        /// values on top - and stays that way, because colorizing it would change every key it does
        /// not set.
        /// </summary>
        public ThemeSettings Settings { get; set; }

        public static ThemeCustomInfo FromFile(string path, IList<string> lines)
        {
            try
            {
                var values = new Dictionary<string, Color>();
                if (lines == null || !TryParse(lines, values, AppSettings.Appearance.RequestedTheme, out ThemeSettings settings, out string name, out TelegramTheme requested, out Color accent))
                {
                    return null;
                }

                if (settings != null)
                {
                    var colorized = FromSettings(requested, settings);

                    // Its own values win over what the header colorizes to.
                    foreach (var item in values)
                    {
                        colorized.Values[item.Key] = item.Value;
                    }

                    return new ThemeCustomInfo(path, accent, colorized.Values, colorized.Shades)
                    {
                        Name = name,
                        Path = path,
                        Parent = requested,
                        Settings = settings
                    };
                }

                var shades = new Dictionary<AccentShade, Color>();

                var color = accent;
                if (color == default)
                {
                    color = BootStrapper.Current.UISettings.GetColorValue(UIColorType.Accent);
                }

                for (int i = 0; i < 7; i++)
                {
                    shades[(AccentShade)i] = SystemAccentPalette.GetShade(color, (AccentShade)i);
                }

                return new ThemeCustomInfo(path, accent, values, shades)
                {
                    Name = name,
                    Path = path,
                    Parent = requested,
                };
            }
            catch
            {
                // Whatever else malformed input can trip in the header's fill or link, or in the
                // colorizer it feeds.
                return null;
            }
        }

        /// <summary>
        /// The header alone, for the settings that wear a theme file without drawing it. Null for a
        /// v1 file or one that cannot be read. Takes the base rather than reading AppSettings.Appearance,
        /// because AppearanceSettings calls this while it is still being constructed.
        /// </summary>
        public static ThemeSettings ReadSettings(string path, TelegramTheme requested)
        {
            try
            {
                return TryParse(System.IO.File.ReadAllLines(path), null, requested, out ThemeSettings settings, out _, out _, out _)
                    ? settings
                    : null;
            }
            catch
            {
                return null;
            }
        }

        // False for a malformed file. True with null settings is a valid v1 file, which is why the
        // two cannot share the return value.
        private static bool TryParse(IList<string> lines, Dictionary<string, Color> values, TelegramTheme fallback, out ThemeSettings settings, out string name, out TelegramTheme requested, out Color accent)
        {
            settings = null;
            requested = fallback;
            accent = _accent[requested == TelegramTheme.Dark ? TelegramThemeType.Night : TelegramThemeType.Day];
            name = string.Empty;

            BuiltInTheme baseTheme = null;
            Color? outgoing = null;
            BackgroundFill fill = null;
            var animate = false;
            string background = null;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.Equals("!") || line.Equals("#"))
                {
                    continue;
                }

                // The first colon only: a name or a link may hold more.
                var separator = line.IndexOf(':');
                if (separator < 0)
                {
                    continue;
                }

                var key = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();

                switch (key)
                {
                    case "name":
                        name = value;
                        break;
                    case "parent":
                        // Anything but light or dark would pick no lookup at all.
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parent)
                            || (parent != (int)TelegramTheme.Light && parent != (int)TelegramTheme.Dark))
                        {
                            return false;
                        }

                        requested = (TelegramTheme)parent;
                        accent = _accent[requested == TelegramTheme.Dark ? TelegramThemeType.Night : TelegramThemeType.Day];
                        break;
                    case "base":
                        if (Enum.TryParse(value, true, out TelegramThemeType type) && type != TelegramThemeType.Custom)
                        {
                            baseTheme = ThemeSettingsStore.ToBuiltInTheme(type);
                        }
                        break;
                    case "accent":
                        if (TryParseColor(value, out Color color))
                        {
                            accent = color;
                        }
                        else if (value == "default")
                        {
                            accent = default;
                        }
                        break;
                    case "outgoing":
                        if (TryParseColor(value, out Color outgoingColor))
                        {
                            outgoing = outgoingColor;
                        }
                        break;
                    case "fill":
                        fill = TdBackground.FromString(value);
                        break;
                    case "animate":
                        animate = value == "1";
                        break;
                    case "background":
                        background = value;
                        break;
                    default:
                        if (values != null && TryParseColor(value, out Color brush))
                        {
                            values[key] = brush;
                        }
                        break;
                }
            }

            if (baseTheme == null)
            {
                return true;
            }

            // The built-in decides the base; a parent that disagrees is a hand-edited file.
            requested = AppearanceSettings.GetBase(ThemeSettingsStore.ToThemeType(baseTheme, requested));

            settings = new ThemeSettings
            {
                BaseTheme = baseTheme,
                AccentColor = accent.ToValue(),
                HasOutgoingMessageAccentColor = outgoing != null,
                OutgoingMessageAccentColor = (outgoing ?? accent).ToValue(),
                OutgoingMessageFill = fill,
                AnimateOutgoingMessageFill = animate,
                Background = ThemeSettingsStore.LoadBackground(background, 0, requested == TelegramTheme.Dark)
            };

            return true;
        }

        private static bool TryParseColor(string value, out Color color)
        {
            if (value.StartsWith("#") && int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexValue))
            {
                byte a = (byte)((hexValue & 0xff000000) >> 24);
                byte r = (byte)((hexValue & 0x00ff0000) >> 16);
                byte g = (byte)((hexValue & 0x0000ff00) >> 8);
                byte b = (byte)(hexValue & 0x000000ff);

                color = Color.FromArgb(a, r, g, b);
                return true;
            }

            color = default;
            return false;
        }

        public static bool Equals(ThemeCustomInfo x, ThemeCustomInfo y)
        {
            if (x.Parent != y.Parent || !x.Settings.AreTheSame(y.Settings))
            {
                return false;
            }

            bool equal = false;
            if (x.Values.Count == y.Values.Count) // Require equal count.
            {
                equal = true;
                foreach (var pair in x.Values)
                {
                    if (y.Values.TryGetValue(pair.Key, out Color value))
                    {
                        // Require value be equal.
                        if (!Equals(value, pair.Value))
                        {
                            equal = false;
                            break;
                        }
                    }
                    else
                    {
                        // Require key be present.
                        equal = false;
                        break;
                    }
                }
            }

            return equal;
        }



        public override Color ChatBackgroundColor
        {
            get
            {
                //if (Values.TryGet("PageHeaderBackgroundBrush", out Color color))
                //{
                //    return color;
                //}

                if (Values.TryGetValue("PageBackgroundDarkBrush", out Color color))
                {
                    return color;
                }

                if (Values.TryGetValue("ApplicationPageBackgroundThemeBrush", out Color color2))
                {
                    return color2;
                }

                return base.ChatBackgroundColor;
            }
        }

        public override Color ChatBorderColor
        {
            get
            {
                //if (Values.TryGet("PageHeaderBackgroundBrush", out Color color))
                //{
                //    return color;
                //}

                if (Values.TryGetValue("PageHeaderBackgroundBrush", out Color color))
                {
                    return color;
                }

                return base.ChatBorderColor;
            }
        }

        public override Color MessageBackgroundColor
        {
            get
            {
                if (Values.TryGetValue("MessageBackgroundIncoming", out Color color))
                {
                    return color;
                }

                return base.MessageBackgroundColor;
            }
        }

        public override Color MessageBackgroundOutColor
        {
            get
            {
                if (Values.TryGetValue("MessageBackgroundOutgoing", out Color color))
                {
                    return color;
                }

                return base.MessageBackgroundOutColor;
            }
        }
    }
}
