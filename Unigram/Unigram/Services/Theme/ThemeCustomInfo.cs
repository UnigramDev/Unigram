using System.Collections.Generic;
using System.Globalization;
using Unigram.Common;
using Unigram.Services.Settings;
using Windows.UI;

namespace Unigram.Services
{
    public class ThemeCustomInfo : ThemeInfoBase
    {
        public ThemeCustomInfo()
        {
            Values = new Dictionary<string, Color>();
            IsOfficial = false;
        }

        public Dictionary<string, Color> Values { get; private set; }

        public string Path { get; set; }

        public override bool IsOfficial { get; }

        public static ThemeCustomInfo FromFile(string path)
        {
            return FromFile(path, System.IO.File.ReadAllLines(path));
        }

        public static ThemeCustomInfo FromFile(string path, IList<string> lines)
        {
            var values = new Dictionary<string, Color>();

            var requested = SettingsService.Current.Appearance.RequestedTheme;
            var accent = _accent[requested == TelegramTheme.Dark ? TelegramThemeType.Night : TelegramThemeType.Day][AccentShade.Base];

            foreach (var line in lines)
            {
                if (line.StartsWith("name: "))
                {
                    continue;
                }
                else if (line.StartsWith("parent: "))
                {
                    requested = (TelegramTheme)int.Parse(line.Substring("parent: ".Length));
                    accent = _accent[requested == TelegramTheme.Dark ? TelegramThemeType.Night : TelegramThemeType.Day][AccentShade.Base];
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
                }
            }

            var type = requested == TelegramTheme.Dark ? TelegramThemeType.Night : TelegramThemeType.Day;

            var colorizer = ThemeColorizer.FromTheme(type, _accent[type][AccentShade.Base], accent);

            foreach (var item in _accent[type])
            {
                values[$"SystemAccentColor{item.Key}"] = colorizer.Colorize(item.Value);
            }

            return new ThemeCustomInfo
            {
                Parent = requested,
                Values = values,
                Path = path
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

        public override Color AccentColor
        {
            get
            {
                if (Values.TryGetValue("SystemAccentColor", out Color color))
                {
                    return color;
                }

                return base.AccentColor;
            }
        }
    }
}
