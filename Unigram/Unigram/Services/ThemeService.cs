using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Services.Settings;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.Services
{
    public interface IThemeService
    {
        IList<ThemeInfoBase> GetThemes();
        Task<IList<ThemeInfoBase>> GetCustomThemesAsync();

        Task SerializeAsync(StorageFile file, ThemeCustomInfo theme);
        Task<ThemeCustomInfo> DeserializeAsync(StorageFile file);

        Task InstallThemeAsync(StorageFile file);
        Task SetThemeAsync(ThemeInfoBase info, bool apply);
    }

    public partial class ThemeService : IThemeService
    {
        private readonly IProtoService _protoService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        public ThemeService(IProtoService protoService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _settingsService = settingsService;
            _aggregator = aggregator;
        }

        public static Dictionary<string, Color> GetLookup(TelegramTheme flags)
        {
            return flags == TelegramTheme.Dark ? _defaultDark : _defaultLight;
        }

        public static Dictionary<string, AccentShade> GetShades(TelegramTheme flags)
        {
            return flags == TelegramTheme.Dark ? _accentsDark : _accentsLight;
        }

        public IList<ThemeInfoBase> GetThemes()
        {
            var result = new List<ThemeInfoBase>();
            result.Add(new ThemeBundledInfo { Name = Strings.Resources.ThemeClassic, Parent = TelegramTheme.Light });
            result.Add(ThemeAccentInfo.FromAccent(TelegramThemeType.Day, _settingsService.Appearance.Accents[TelegramThemeType.Day]));
            result.Add(ThemeAccentInfo.FromAccent(TelegramThemeType.Tinted, _settingsService.Appearance.Accents[TelegramThemeType.Tinted]));
            result.Add(ThemeAccentInfo.FromAccent(TelegramThemeType.Night, _settingsService.Appearance.Accents[TelegramThemeType.Night]));

            return result;
        }

        public async Task<IList<ThemeInfoBase>> GetCustomThemesAsync()
        {
            var result = new List<ThemeInfoBase>();

            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("themes", CreationCollisionOption.OpenIfExists);
            var files = await folder.GetFilesAsync();

            foreach (var file in files)
            {
                try
                {
                    result.Add(await DeserializeAsync(file));
                }
                catch { }
            }

            return result;
        }

        public async Task SerializeAsync(StorageFile file, ThemeCustomInfo theme)
        {
            var lines = new StringBuilder();
            lines.AppendLine("!");
            lines.AppendLine($"name: {theme.Name}");
            lines.AppendLine($"parent: {(int)theme.Parent}");

            var lastbrush = false;

            foreach (var item in theme.Values)
            {
                if (item.Value is Color color)
                {
                    if (!lastbrush)
                    {
                        lines.AppendLine("#");
                    }

                    var hexValue = (color.A << 24) + (color.R << 16) + (color.G << 8) + (color.B & 0xff);

                    lastbrush = true;
                    lines.AppendLine(string.Format("{0}: #{1:X8}", item.Key, hexValue));
                }
            }

            await FileIO.WriteTextAsync(file, lines.ToString());
        }

        public async Task<ThemeCustomInfo> DeserializeAsync(StorageFile file)
        {
            var lines = await FileIO.ReadLinesAsync(file);
            var theme = ThemeCustomInfo.FromFile(file.Path, lines);

            return theme;
        }



        public async Task InstallThemeAsync(StorageFile file)
        {
            var info = await DeserializeAsync(file);
            if (info == null)
            {
                return;
            }

            var installed = await GetCustomThemesAsync();

            var equals = installed.FirstOrDefault(x => x is ThemeCustomInfo custom && ThemeCustomInfo.Equals(custom, info));
            if (equals != null)
            {
                await SetThemeAsync(equals, true);
                return;
            }

            var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync("themes");
            var result = await file.CopyAsync(folder, file.Name, NameCollisionOption.GenerateUniqueName);

            var theme = await DeserializeAsync(result);
            if (theme != null)
            {
                await SetThemeAsync(theme, true);
            }
        }

        public async Task SetThemeAsync(ThemeInfoBase info, bool apply)
        {
            if (apply)
            {
                _settingsService.Appearance.RequestedTheme = info.Parent;
            }

            if (info is ThemeCustomInfo custom)
            {
                _settingsService.Appearance[info.Parent].Type = TelegramThemeType.Custom;
                _settingsService.Appearance[info.Parent].Custom = custom.Path;
            }
            else if (info is ThemeAccentInfo accent)
            {
                _settingsService.Appearance[info.Parent].Type = accent.Type;
                _settingsService.Appearance.Accents[accent.Type] = accent.AccentColor;
            }
            else
            {
                _settingsService.Appearance[info.Parent].Type = info.Parent == TelegramTheme.Light ? TelegramThemeType.Classic : TelegramThemeType.Night;
            }

            var flags = _settingsService.Appearance.GetCalculatedElementTheme();
            var theme = flags == ElementTheme.Dark ? TelegramTheme.Dark : TelegramTheme.Light;

            if (theme != info.Parent && !apply)
            {
                return;
            }

            _settingsService.Appearance.UpdateNightMode(true);
        }
    }

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

        public static ThemeAccentInfo FromAccent(TelegramThemeType type, Color accent)
        {
            var color = accent;
            if (color == default)
            {
                color = BootStrapper.Current.UISettings.GetColorValue(UIColorType.Accent);
            }

            var colorizer = ThemeColorizer.FromTheme(type, _accent[type][AccentShade.Base], color);
            var values = new Dictionary<string, Color>();

            foreach (var item in _accent[type])
            {
                values[$"SystemAccentColor{item.Key}"] = colorizer.Colorize(item.Value);
            }

            foreach (var item in _map[type])
            {
                values[item.Key] = colorizer.Colorize(item.Value);
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
            return ThemeCustomInfo.FromFile(path, System.IO.File.ReadAllLines(path));
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

    public class ThemeBundledInfo : ThemeInfoBase
    {
        public override bool IsOfficial => true;
    }

    public abstract partial class ThemeInfoBase
    {
        public string Name { get; set; }
        public TelegramTheme Parent { get; set; }

        public abstract bool IsOfficial { get; }



        public virtual Color ChatBackgroundColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0xdf, 0xe4, 0xe8);
                }

                return Color.FromArgb(0xFF, 0x10, 0x14, 0x16);
            }
        }

        public virtual Color ChatBorderColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0xe6, 0xe6, 0xe6);
                }

                return Color.FromArgb(0xFF, 0x2b, 0x2b, 0x2b);
            }
        }

        public virtual Color MessageBackgroundColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
                }

                return Color.FromArgb(0xFF, 0x18, 0x25, 0x33);
            }
        }

        public virtual Color MessageBackgroundOutColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF);
                }

                return Color.FromArgb(0xFF, 0x2B, 0x52, 0x78);
            }
        }

        public virtual Color SelectionColor => AccentColor;

        public virtual Color AccentColor
        {
            get
            {
                if (Parent == TelegramTheme.Light)
                {
                    return Color.FromArgb(0xFF, 0x15, 0x8D, 0xCD);
                }

                return Color.FromArgb(0xFF, 0x71, 0xBA, 0xFA);
            }
        }
    }
}
