using System.Collections.Generic;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services.Settings;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Unigram.Services
{
    public class ThemeAccentInfo : ThemeInfoBase
    {
        public ThemeAccentInfo(TelegramThemeType type, Color accent, Dictionary<string, Color> values)
        {
            Type = type;
            AccentColor = accent;
            Values = values;

            switch (type)
            {
                case TelegramThemeType.Day:
                    Parent = TelegramTheme.Light;
                    Name = Strings.Resources.ThemeDay;
                    break;
                case TelegramThemeType.Night:
                    Parent = TelegramTheme.Dark;
                    Name = Strings.Resources.ThemeNight;
                    break;
                case TelegramThemeType.Tinted:
                    Parent = TelegramTheme.Dark;
                    Name = Strings.Resources.ThemeDark;
                    break;
            }

            IsOfficial = true;
        }

        public static ThemeAccentInfo FromAccent(TelegramThemeType type, Color accent, Color outgoing = default)
        {
            var color = accent;
            if (color == default)
            {
                color = BootStrapper.Current.UISettings.GetColorValue(UIColorType.Accent);
            }

            var colorizer = ThemeColorizer.FromTheme(type, _accent[type][AccentShade.Base], color);
            var outgoingColorizer = outgoing != default ? ThemeColorizer.FromTheme(type, _accent[type][AccentShade.Base], outgoing) : null;
            var values = new Dictionary<string, Color>();

            foreach (var item in _accent[type])
            {
                values[$"SystemAccentColor{item.Key}"] = colorizer.Colorize(item.Value);
            }

            foreach (var item in _map[type])
            {
                if (outgoingColorizer != null && item.Key.EndsWith("Outgoing"))
                {
                    values[item.Key] = outgoingColorizer.Colorize(item.Value);
                }
                else
                {
                    values[item.Key] = colorizer.Colorize(item.Value);
                }
            }

            return new ThemeAccentInfo(type, accent, values);
        }

        public static Color Colorize(TelegramThemeType type, Color accent, string key)
        {
            var colorizer = ThemeColorizer.FromTheme(type, _accent[type][AccentShade.Base], accent);
            if (_map[type].TryGetValue(key, out Color color))
            {
                return colorizer.Colorize(color);
            }

            var lookup = ThemeService.GetLookup(type == TelegramThemeType.Day ? TelegramTheme.Light : TelegramTheme.Dark);
            return colorizer.Colorize(lookup[key]);
        }

        public override Color AccentColor { get; }

        public TelegramThemeType Type { get; private set; }

        public Dictionary<string, Color> Values { get; private set; }

        public override bool IsOfficial { get; }



        public override Color SelectionColor
        {
            get
            {
                if (AccentColor == default)
                {
                    return BootStrapper.Current.UISettings.GetColorValue(UIColorType.Accent);
                }

                return AccentColor;
            }
        }

        public override Color ChatBackgroundColor
        {
            get
            {
                if (Values.TryGetValue("PageBackgroundDarkBrush", out Color color))
                {
                    return color;
                }

                return base.ChatBackgroundColor;
            }
        }

        public override Color ChatBorderColor
        {
            get
            {
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

        public static bool IsAccent(TelegramThemeType type)
        {
            return type is TelegramThemeType.Tinted or
                TelegramThemeType.Night or
                TelegramThemeType.Day;
        }
    }
}
