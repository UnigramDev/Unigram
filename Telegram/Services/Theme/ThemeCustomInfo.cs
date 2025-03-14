//
// Copyright Fela Ameghino 2015-2025
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

        public static ThemeCustomInfo FromFile(string path)
        {
            return FromFile(path, System.IO.File.ReadAllLines(path));
        }

        public static async Task<ThemeCustomInfo> FromFileAsync(StorageFile file)
        {
            return FromFile(file.Path, await FileIO.ReadLinesAsync(file));
        }

        public static ThemeCustomInfo FromFile(string path, IList<string> lines)
        {
            var values = new Dictionary<string, Color>();
            var shades = new Dictionary<AccentShade, Color>();

            var requested = SettingsService.Current.Appearance.RequestedTheme;
            var accent = _accent[requested == TelegramTheme.Dark ? TelegramThemeType.Night : TelegramThemeType.Day][AccentShade.Default];
            var name = string.Empty;

            foreach (var line in lines)
            {
                if (line.StartsWith("name: "))
                {
                    name = line.Substring("name: ".Length);
                }
                else if (line.StartsWith("parent: "))
                {
                    requested = (TelegramTheme)int.Parse(line.Substring("parent: ".Length));
                    accent = _accent[requested == TelegramTheme.Dark ? TelegramThemeType.Night : TelegramThemeType.Day][AccentShade.Default];
                }
                else if (line.Equals("!") || line.Equals("#") || string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                else
                {
                    var split = line.Split(':');
                    if (split.Length < 2)
                    {
                        continue;
                    }

                    var key = split[0].Trim();
                    var value = split[1].Trim();

                    if (value.StartsWith("#") && int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexValue))
                    {
                        byte a = (byte)((hexValue & 0xff000000) >> 24);
                        byte r = (byte)((hexValue & 0x00ff0000) >> 16);
                        byte g = (byte)((hexValue & 0x0000ff00) >> 8);
                        byte b = (byte)(hexValue & 0x000000ff);

                        if (key == "accent")
                        {
                            accent = Color.FromArgb(a, r, g, b);
                        }
                        else
                        {
                            values[key] = Color.FromArgb(a, r, g, b);
                        }
                    }
                    else if (key == "accent" && value == "default")
                    {
                        accent = default;
                    }
                }
            }

            var color = accent;
            if (color == default)
            {
                color = BootStrapper.Current.UISettings.GetColorValue(UIColorType.Accent);
            }

            var type = requested == TelegramTheme.Dark ? TelegramThemeType.Night : TelegramThemeType.Day;
            var colorizer = ThemeColorizer.FromTheme(type, _accent[type][AccentShade.Default], color);

            foreach (var item in _accent[type])
            {
                shades[item.Key] = colorizer.Colorize(item.Value);
            }

            return new ThemeCustomInfo(path, accent, values, shades)
            {
                Name = name,
                Path = path,
                Parent = requested,
            };
        }

        public static bool Equals(ThemeCustomInfo x, ThemeCustomInfo y)
        {
            if (x.Parent != y.Parent)
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
                if (Values.TryGetValue("MessageBackgroundBrush", out Color color))
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
