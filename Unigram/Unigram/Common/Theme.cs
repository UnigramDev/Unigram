using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using Telegram.Td.Api;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class Theme : ResourceDictionary
    {
        [ThreadStatic]
        public static Theme Current;

        private readonly ApplicationDataContainer isolatedStore;

        public Theme()
        {
            try
            {
                isolatedStore = ApplicationData.Current.LocalSettings.CreateContainer("Theme", ApplicationDataCreateDisposition.Always);
                Current ??= this;

                this.Add("MessageFontSize", GetValueOrDefault("MessageFontSize", 14d));

                var emojiSet = SettingsService.Current.Appearance.EmojiSet;
                switch (emojiSet.Id)
                {
                    case "microsoft":
                        this.Add("EmojiThemeFontFamily", new FontFamily($"XamlAutoFontFamily"));
                        break;
                    case "apple":
                        this.Add("EmojiThemeFontFamily", new FontFamily($"ms-appx:///Assets/Emoji/{emojiSet.Id}.ttf#Segoe UI Emoji"));
                        break;
                    default:
                        this.Add("EmojiThemeFontFamily", new FontFamily($"ms-appdata:///local/emoji/{emojiSet.Id}.{emojiSet.Version}.ttf#Segoe UI Emoji"));
                        break;
                }

                this.Add("ThreadStackLayout", new StackLayout());
            }
            catch { }

            MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/ThemeGreen.xaml") });

            UpdateAcrylicBrushes();
            Initialize();
        }

        public void Initialize()
        {
            Initialize(SettingsService.Current.Appearance.GetCalculatedApplicationTheme());
        }

        public void Initialize(ApplicationTheme requested)
        {
            Initialize(requested == ApplicationTheme.Dark ? TelegramTheme.Dark : TelegramTheme.Light);
        }

        public void Initialize(ElementTheme requested)
        {
            Initialize(requested == ElementTheme.Dark ? TelegramTheme.Dark : TelegramTheme.Light);
        }

        public void Initialize(TelegramTheme requested)
        {
            var settings = SettingsService.Current.Appearance;
            if (settings[requested].Type == TelegramThemeType.Custom && System.IO.File.Exists(settings[requested].Custom))
            {
                UpdateCustom(settings[requested].Custom);
            }
            else if (ThemeAccentInfo.IsAccent(settings[requested].Type))
            {
                Update(ThemeAccentInfo.FromAccent(settings[requested].Type, settings.Accents[settings[requested].Type]));
            }
            else
            {
                Update(requested);
            }
        }

        private void UpdateCustom(string path)
        {
            var lines = System.IO.File.ReadAllLines(path);
            var values = new Dictionary<string, Color>();

            var requested = SettingsService.Current.Appearance.RequestedTheme;

            foreach (var line in lines)
            {
                if (line.StartsWith("name: "))
                {
                    continue;
                }
                else if (line.StartsWith("parent: "))
                {
                    requested = (TelegramTheme)int.Parse(line.Substring("parent: ".Length));
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

                        values[key] = Color.FromArgb(a, r, g, b);
                    }
                }
            }

            Update(requested, values);
        }

        private int? _lastTheme;
        private long? _lastBackground;

        public bool Update(ElementTheme requested, ChatTheme theme)
        {
            var updated = false;

            var settings = requested == ElementTheme.Light ? theme?.LightSettings : theme?.DarkSettings;
            if (settings != null)
            {
                if (_lastTheme != settings.AccentColor)
                {
                    Update(ThemeAccentInfo.FromAccent(requested == ElementTheme.Light
                        ? TelegramThemeType.Day
                        : TelegramThemeType.Tinted, settings.OutgoingMessageAccentColor.ToColor()));
                }
                if (_lastBackground != settings.Background?.Id)
                {
                    updated = true;
                }

                _lastTheme = settings.AccentColor;
                _lastBackground = settings.Background?.Id;
            }
            else
            {
                if (_lastTheme != null)
                {
                    Update(requested == ElementTheme.Dark
                        ? TelegramTheme.Dark 
                        : TelegramTheme.Light);
                }
                if (_lastBackground != null)
                {
                    updated = true;
                }

                _lastTheme = null;
                _lastBackground = null;
            }

            return updated;
        }

        public void Update(ThemeInfoBase info)
        {
            if (info is ThemeCustomInfo custom)
            {
                Update(info.Parent, custom.Values);
            }
            else if (info is ThemeAccentInfo colorized)
            {
                Update(info.Parent, colorized.Values);
            }
            else
            {
                Update(info.Parent);
            }
        }

        private void Update(TelegramTheme requested, IDictionary<string, Color> values = null)
        {
            try
            {
                ThemeOutgoing.Update(requested, values);

                var target = MergedDictionaries[0].ThemeDictionaries[requested == TelegramTheme.Light ? "Light" : "Dark"] as ResourceDictionary;
                var mapping = ThemeService.GetMapping(requested);
                var lookup = ThemeService.GetLookup(requested);

                foreach (var item in lookup)
                {
                    if (target.TryGet(item.Key, out SolidColorBrush brush))
                    {
                        if (values != null && values.TryGetValue(item.Key, out Color themed))
                        {
                            brush.Color = themed;
                        }
                        else if (brush.Color != item.Value)
                        {
                            brush.Color = item.Value;
                        }
                    }
                }
            }
            catch { }
        }

        private void UpdateAcrylicBrushes()
        {
            UpdateAcrylicBrushesLightTheme(MergedDictionaries[0].ThemeDictionaries["Light"] as ResourceDictionary);
            UpdateAcrylicBrushesDarkTheme(MergedDictionaries[0].ThemeDictionaries["Dark"] as ResourceDictionary);
        }

        private void UpdateAcrylicBrushesLightTheme(ResourceDictionary dictionary)
        {
            if (dictionary.TryGet("AcrylicBackgroundFillColorDefaultBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicBackgroundFillColorDefaultBrush))
            {
                acrylicBackgroundFillColorDefaultBrush.TintLuminosityOpacity = 0.85;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorDefaultBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicInAppFillColorDefaultBrush))
            {
                acrylicInAppFillColorDefaultBrush.TintLuminosityOpacity = 0.85;
            }
            if (dictionary.TryGet("AcrylicBackgroundFillColorDefaultInverseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicBackgroundFillColorDefaultInverseBrush))
            {
                acrylicBackgroundFillColorDefaultInverseBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorDefaultInverseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicInAppFillColorDefaultInverseBrush))
            {
                acrylicInAppFillColorDefaultInverseBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicBackgroundFillColorBaseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicBackgroundFillColorBaseBrush))
            {
                acrylicBackgroundFillColorBaseBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorBaseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicInAppFillColorBaseBrush))
            {
                acrylicInAppFillColorBaseBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AccentAcrylicBackgroundFillColorDefaultBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush accentAcrylicBackgroundFillColorDefaultBrush))
            {
                accentAcrylicBackgroundFillColorDefaultBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AccentAcrylicInAppFillColorDefaultBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush accentAcrylicInAppFillColorDefaultBrush))
            {
                accentAcrylicInAppFillColorDefaultBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AccentAcrylicBackgroundFillColorBaseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush accentAcrylicBackgroundFillColorBaseBrush))
            {
                accentAcrylicBackgroundFillColorBaseBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AccentAcrylicInAppFillColorBaseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush accentAcrylicInAppFillColorBaseBrush))
            {
                accentAcrylicInAppFillColorBaseBrush.TintLuminosityOpacity = 0.9;
            }
        }

        private void UpdateAcrylicBrushesDarkTheme(ResourceDictionary dictionary)
        {
            if (dictionary.TryGet("AcrylicBackgroundFillColorDefaultBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicBackgroundFillColorDefaultBrush))
            {
                acrylicBackgroundFillColorDefaultBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorDefaultBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicInAppFillColorDefaultBrush))
            {
                acrylicInAppFillColorDefaultBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicBackgroundFillColorDefaultInverseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicBackgroundFillColorDefaultInverseBrush))
            {
                acrylicBackgroundFillColorDefaultInverseBrush.TintLuminosityOpacity = 0.85;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorDefaultInverseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicInAppFillColorDefaultInverseBrush))
            {
                acrylicInAppFillColorDefaultInverseBrush.TintLuminosityOpacity = 0.85;
            }
            if (dictionary.TryGet("AcrylicBackgroundFillColorBaseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicBackgroundFillColorBaseBrush))
            {
                acrylicBackgroundFillColorBaseBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorBaseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush acrylicInAppFillColorBaseBrush))
            {
                acrylicInAppFillColorBaseBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AccentAcrylicBackgroundFillColorDefaultBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush accentAcrylicBackgroundFillColorDefaultBrush))
            {
                accentAcrylicBackgroundFillColorDefaultBrush.TintLuminosityOpacity = 0.8;
            }
            if (dictionary.TryGet("AccentAcrylicInAppFillColorDefaultBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush accentAcrylicInAppFillColorDefaultBrush))
            {
                accentAcrylicInAppFillColorDefaultBrush.TintLuminosityOpacity = 0.8;
            }
            if (dictionary.TryGet("AccentAcrylicBackgroundFillColorBaseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush accentAcrylicBackgroundFillColorBaseBrush))
            {
                accentAcrylicBackgroundFillColorBaseBrush.TintLuminosityOpacity = 0.8;
            }
            if (dictionary.TryGet("AccentAcrylicInAppFillColorBaseBrush", out Microsoft.UI.Xaml.Media.AcrylicBrush accentAcrylicInAppFillColorBaseBrush))
            {
                accentAcrylicInAppFillColorBaseBrush.TintLuminosityOpacity = 0.8;
            }
        }

        #region Settings

        private int? _messageFontSize;
        public int MessageFontSize
        {
            get
            {
                if (_messageFontSize == null)
                {
                    _messageFontSize = (int)GetValueOrDefault("MessageFontSize", 14d);
                }

                return _messageFontSize ?? 14;
            }
            set
            {
                _messageFontSize = value;
                AddOrUpdateValue("MessageFontSize", (double)value);
            }
        }

        public bool AddOrUpdateValue(string key, object value)
        {
            bool valueChanged = false;

            if (isolatedStore.Values.ContainsKey(key))
            {
                if (isolatedStore.Values[key] != value)
                {
                    isolatedStore.Values[key] = value;
                    valueChanged = true;
                }
            }
            else
            {
                isolatedStore.Values.Add(key, value);
                valueChanged = true;
            }

            if (valueChanged)
            {
                try
                {
                    if (this.ContainsKey(key))
                    {
                        this[key] = value;
                    }
                    else
                    {
                        this.Add(key, value);
                    }
                }
                catch { }
            }

            return valueChanged;
        }

        public valueType GetValueOrDefault<valueType>(string key, valueType defaultValue)
        {
            valueType value;

            if (isolatedStore.Values.ContainsKey(key))
            {
                value = (valueType)isolatedStore.Values[key];
            }
            else
            {
                value = defaultValue;
            }

            return value;
        }

        #endregion
    }

    public class ThemeOutgoing : ResourceDictionary
    {
        [ThreadStatic]
        private static Dictionary<string, SolidColorBrush> _light = new()
        {
            { "MessageForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0x000000)) },
            { "MessageForegroundLinkBrush", new SolidColorBrush(ColorEx.FromHex(0x168ACD)) },
            { "MessageBackgroundBrush", new SolidColorBrush(ColorEx.FromHex(0xF0FDDF)) },
            { "MessageSubtleLabelBrush", new SolidColorBrush(ColorEx.FromHex(0x6DC264)) },
            { "MessageSubtleGlyphBrush", new SolidColorBrush(ColorEx.FromHex(0x5DC452)) },
            { "MessageSubtleForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0x6DC264)) },
            { "MessageHeaderForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0x3A8E26)) },
            { "MessageHeaderBorderBrush", new SolidColorBrush(ColorEx.FromHex(0x5DC452)) },
            { "MessageMediaForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0xF0FDDF)) },
            { "MessageMediaBackgroundBrush", new SolidColorBrush(ColorEx.FromHex(0x78C67F)) },
            { "MessageOverlayBackgroundBrush", new SolidColorBrush(ColorEx.FromHex(0x54000000)) },
            { "MessageCallForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0x2AB32A)) },
            { "MessageCallMissedForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0xDD5849)) },
        };

        [ThreadStatic]
        private static Dictionary<string, SolidColorBrush> _dark = new()
        {
            { "MessageForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0xE4ECF2)) },
            { "MessageForegroundLinkBrush", new SolidColorBrush(ColorEx.FromHex(0x83CAFF)) },
            { "MessageBackgroundBrush", new SolidColorBrush(ColorEx.FromHex(0x2B5278)) },
            { "MessageSubtleLabelBrush", new SolidColorBrush(ColorEx.FromHex(0x7DA8D3)) },
            { "MessageSubtleGlyphBrush", new SolidColorBrush(ColorEx.FromHex(0x72BCFD)) },
            { "MessageSubtleForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0x7DA8D3)) },
            { "MessageHeaderForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0x90CAFF)) },
            { "MessageHeaderBorderBrush", new SolidColorBrush(ColorEx.FromHex(0x65B9F4)) },
            { "MessageMediaForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0xFFFFFF)) },
            { "MessageMediaBackgroundBrush", new SolidColorBrush(ColorEx.FromHex(0x4C9CE2)) },
            { "MessageOverlayBackgroundBrush", new SolidColorBrush(ColorEx.FromHex(0x54000000)) },
            { "MessageCallForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0x49A2F0)) },
            { "MessageCallMissedForegroundBrush", new SolidColorBrush(ColorEx.FromHex(0xED5050)) },
        };

        public ThemeOutgoing()
        {
            var light = new ResourceDictionary();
            var dark = new ResourceDictionary();

            foreach (var item in _light)
            {
                light[item.Key] = item.Value;
            }

            foreach (var item in _dark)
            {
                dark[item.Key] = item.Value;
            }

            ThemeDictionaries["Light"] = light;
            ThemeDictionaries["Default"] = dark;
        }

        public static void Update(TelegramTheme parent, IDictionary<string, Color> values = null)
        {
            if (values == null)
            {
                Update(parent);
                return;
            }

            var target = parent == TelegramTheme.Dark ? _dark : _light;

            foreach (var value in values)
            {
                if (value.Key.EndsWith("OutColor"))
                {
                    var key = value.Key.Substring(0, value.Key.Length - "OutColor".Length);
                    if (target.TryGetValue($"{key}Brush", out SolidColorBrush brush))
                    {
                        brush.Color = value.Value;
                    }
                }
            }
        }

        public static void Update(TelegramTheme parent)
        {
            if (parent == TelegramTheme.Light)
            {
                _light["MessageForegroundBrush"].Color = ColorEx.FromHex(0x000000);
                _light["MessageForegroundLinkBrush"].Color = ColorEx.FromHex(0x168ACD);
                _light["MessageBackgroundBrush"].Color = ColorEx.FromHex(0xF0FDDF);
                _light["MessageSubtleLabelBrush"].Color = ColorEx.FromHex(0x6DC264);
                _light["MessageSubtleGlyphBrush"].Color = ColorEx.FromHex(0x5DC452);
                _light["MessageSubtleForegroundBrush"].Color = ColorEx.FromHex(0x6DC264);
                _light["MessageHeaderForegroundBrush"].Color = ColorEx.FromHex(0x3A8E26);
                _light["MessageHeaderBorderBrush"].Color = ColorEx.FromHex(0x5DC452);
                _light["MessageMediaForegroundBrush"].Color = ColorEx.FromHex(0xF0FDDF);
                _light["MessageMediaBackgroundBrush"].Color = ColorEx.FromHex(0x78C67F);
                _light["MessageOverlayBackgroundBrush"].Color = ColorEx.FromHex(0x54000000);
                _light["MessageCallForegroundBrush"].Color = ColorEx.FromHex(0x2AB32A);
                _light["MessageCallMissedForegroundBrush"].Color = ColorEx.FromHex(0xDD5849);
            }
            else
            {
                _dark["MessageForegroundBrush"].Color = ColorEx.FromHex(0xE4ECF2);
                _dark["MessageForegroundLinkBrush"].Color = ColorEx.FromHex(0x83CAFF);
                _dark["MessageBackgroundBrush"].Color = ColorEx.FromHex(0x2B5278);
                _dark["MessageSubtleLabelBrush"].Color = ColorEx.FromHex(0x7DA8D3);
                _dark["MessageSubtleGlyphBrush"].Color = ColorEx.FromHex(0x72BCFD);
                _dark["MessageSubtleForegroundBrush"].Color = ColorEx.FromHex(0x7DA8D3);
                _dark["MessageHeaderForegroundBrush"].Color = ColorEx.FromHex(0x90CAFF);
                _dark["MessageHeaderBorderBrush"].Color = ColorEx.FromHex(0x65B9F4);
                _dark["MessageMediaForegroundBrush"].Color = ColorEx.FromHex(0xFFFFFF);
                _dark["MessageMediaBackgroundBrush"].Color = ColorEx.FromHex(0x4C9CE2);
                _dark["MessageOverlayBackgroundBrush"].Color = ColorEx.FromHex(0x54000000);
                _dark["MessageCallForegroundBrush"].Color = ColorEx.FromHex(0x49A2F0);
                _dark["MessageCallMissedForegroundBrush"].Color = ColorEx.FromHex(0xED5050);
            }
        }
    }
}
