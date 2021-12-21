using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
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
            var main = Current == null;

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
            UpdateMica();

            if (main)
            {
                UpdateAcrylicBrushes();
                Initialize();
            }
        }

        private void UpdateMica()
        {
            var add = false;
            if (MergedDictionaries.Count == 2 && _lastMica != SettingsService.Current.Diagnostics.Mica)
            {
                MergedDictionaries.RemoveAt(1);
                add = true;
            }
            else if (MergedDictionaries.Count == 1)
            {
                add = true;
            }

            if (add)
            {
                _lastMica = SettingsService.Current.Diagnostics.Mica;
                MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(string.Format("ms-appx:///Themes/Experimental_{0}.xaml", _lastMica ? "enabled" : "disabled"))
                });
            }
        }

        public ChatTheme ChatTheme => _lastTheme;

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
            UpdateMica();

            if (_lastTheme != null)
            {
                Update(requested, _lastTheme);
            }
            else
            {
                Initialize(requested == ElementTheme.Dark ? TelegramTheme.Dark : TelegramTheme.Light);
            }
        }

        public void Initialize(TelegramTheme requested)
        {
            var settings = SettingsService.Current.Appearance;
            if (settings[requested].Type == TelegramThemeType.Custom && System.IO.File.Exists(settings[requested].Custom))
            {
                Update(ThemeCustomInfo.FromFile(settings[requested].Custom));
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

        public void Initialize(string path)
        {
            Update(ThemeCustomInfo.FromFile(path));
        }

        private bool _lastMica;

        private int? _lastAccent;
        private long? _lastBackground;

        private ChatTheme _lastTheme;

        public bool Update(ElementTheme elementTheme, ChatTheme theme)
        {
            var updated = false;
            var requested = elementTheme == ElementTheme.Dark ? TelegramTheme.Dark : TelegramTheme.Light;

            var settings = requested == TelegramTheme.Light ? theme?.LightSettings : theme?.DarkSettings;
            if (settings != null)
            {
                if (_lastAccent != settings.AccentColor)
                {
                    _lastTheme = theme;

                    var tint = SettingsService.Current.Appearance[requested].Type;
                    if (tint == TelegramThemeType.Classic || (tint == TelegramThemeType.Custom && requested == TelegramTheme.Light))
                    {
                        tint = TelegramThemeType.Day;
                    }
                    else if (tint == TelegramThemeType.Custom)
                    {
                        tint = TelegramThemeType.Tinted;
                    }

                    var accent = settings.AccentColor.ToColor();
                    var outgoing = settings.OutgoingMessageAccentColor.ToColor();

                    Update(ThemeAccentInfo.FromAccent(tint, accent, outgoing));
                }
                if (_lastBackground != settings.Background?.Id)
                {
                    updated = true;
                }

                _lastAccent = settings.AccentColor;
                _lastBackground = settings.Background?.Id;
            }
            else
            {
                if (_lastAccent != null)
                {
                    _lastTheme = null;
                    Initialize(requested);
                }
                if (_lastBackground != null)
                {
                    updated = true;
                }

                _lastAccent = null;
                _lastBackground = null;
            }

            return updated;
        }

        public void Update(ThemeInfoBase info)
        {
            if (info is ThemeCustomInfo custom)
            {
                Update(info.Parent, custom);
            }
            else if (info is ThemeAccentInfo colorized)
            {
                Update(info.Parent, colorized);
            }
            else
            {
                Update(info.Parent);
            }
        }

        private void Update(TelegramTheme requested, ThemeAccentInfo info = null)
        {
            try
            {
                ThemeOutgoing.Update(requested, info?.Values);

                var values = info?.Values;
                var shades = info?.Shades;

                var target = MergedDictionaries[0].ThemeDictionaries[requested == TelegramTheme.Light ? "Light" : "Dark"] as ResourceDictionary;
                var lookup = ThemeService.GetLookup(requested);

                foreach (var item in lookup)
                {
                    if (target.TryGet(item.Key, out SolidColorBrush brush))
                    {
                        Color value;
                        if (item.Value is AccentShade shade)
                        {
                            if (shades != null && shades.TryGetValue(shade, out Color accent))
                            {
                                value = accent;
                            }
                            else
                            {
                                value = ThemeInfoBase.Accents[TelegramThemeType.Day][shade];
                            }
                        }
                        else if (values != null && values.TryGetValue(item.Key, out Color themed))
                        {
                            value = themed;
                        }
                        else if (item.Value is Color color)
                        {
                            value = color;
                        }

                        brush.Color = value;
                    }
                }
            }
            catch { }
        }

        #region Acrylic patch

        private void UpdateAcrylicBrushes()
        {
            UpdateAcrylicBrushesLightTheme(MergedDictionaries[0].ThemeDictionaries["Light"] as ResourceDictionary);
            UpdateAcrylicBrushesDarkTheme(MergedDictionaries[0].ThemeDictionaries["Dark"] as ResourceDictionary);
        }

        private void UpdateAcrylicBrushesLightTheme(ResourceDictionary dictionary)
        {
            if (dictionary.TryGet("AcrylicBackgroundFillColorDefaultBrush", out AcrylicBrush acrylicBackgroundFillColorDefaultBrush))
            {
                acrylicBackgroundFillColorDefaultBrush.TintLuminosityOpacity = 0.85;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorDefaultBrush", out AcrylicBrush acrylicInAppFillColorDefaultBrush))
            {
                acrylicInAppFillColorDefaultBrush.TintLuminosityOpacity = 0.85;
            }
            if (dictionary.TryGet("AcrylicBackgroundFillColorDefaultInverseBrush", out AcrylicBrush acrylicBackgroundFillColorDefaultInverseBrush))
            {
                acrylicBackgroundFillColorDefaultInverseBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorDefaultInverseBrush", out AcrylicBrush acrylicInAppFillColorDefaultInverseBrush))
            {
                acrylicInAppFillColorDefaultInverseBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicBackgroundFillColorBaseBrush", out AcrylicBrush acrylicBackgroundFillColorBaseBrush))
            {
                acrylicBackgroundFillColorBaseBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorBaseBrush", out AcrylicBrush acrylicInAppFillColorBaseBrush))
            {
                acrylicInAppFillColorBaseBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AccentAcrylicBackgroundFillColorDefaultBrush", out AcrylicBrush accentAcrylicBackgroundFillColorDefaultBrush))
            {
                accentAcrylicBackgroundFillColorDefaultBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AccentAcrylicInAppFillColorDefaultBrush", out AcrylicBrush accentAcrylicInAppFillColorDefaultBrush))
            {
                accentAcrylicInAppFillColorDefaultBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AccentAcrylicBackgroundFillColorBaseBrush", out AcrylicBrush accentAcrylicBackgroundFillColorBaseBrush))
            {
                accentAcrylicBackgroundFillColorBaseBrush.TintLuminosityOpacity = 0.9;
            }
            if (dictionary.TryGet("AccentAcrylicInAppFillColorBaseBrush", out AcrylicBrush accentAcrylicInAppFillColorBaseBrush))
            {
                accentAcrylicInAppFillColorBaseBrush.TintLuminosityOpacity = 0.9;
            }
        }

        private void UpdateAcrylicBrushesDarkTheme(ResourceDictionary dictionary)
        {
            if (dictionary.TryGet("AcrylicBackgroundFillColorDefaultBrush", out AcrylicBrush acrylicBackgroundFillColorDefaultBrush))
            {
                acrylicBackgroundFillColorDefaultBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorDefaultBrush", out AcrylicBrush acrylicInAppFillColorDefaultBrush))
            {
                acrylicInAppFillColorDefaultBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicBackgroundFillColorDefaultInverseBrush", out AcrylicBrush acrylicBackgroundFillColorDefaultInverseBrush))
            {
                acrylicBackgroundFillColorDefaultInverseBrush.TintLuminosityOpacity = 0.85;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorDefaultInverseBrush", out AcrylicBrush acrylicInAppFillColorDefaultInverseBrush))
            {
                acrylicInAppFillColorDefaultInverseBrush.TintLuminosityOpacity = 0.85;
            }
            if (dictionary.TryGet("AcrylicBackgroundFillColorBaseBrush", out AcrylicBrush acrylicBackgroundFillColorBaseBrush))
            {
                acrylicBackgroundFillColorBaseBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AcrylicInAppFillColorBaseBrush", out AcrylicBrush acrylicInAppFillColorBaseBrush))
            {
                acrylicInAppFillColorBaseBrush.TintLuminosityOpacity = 0.96;
            }
            if (dictionary.TryGet("AccentAcrylicBackgroundFillColorDefaultBrush", out AcrylicBrush accentAcrylicBackgroundFillColorDefaultBrush))
            {
                accentAcrylicBackgroundFillColorDefaultBrush.TintLuminosityOpacity = 0.8;
            }
            if (dictionary.TryGet("AccentAcrylicInAppFillColorDefaultBrush", out AcrylicBrush accentAcrylicInAppFillColorDefaultBrush))
            {
                accentAcrylicInAppFillColorDefaultBrush.TintLuminosityOpacity = 0.8;
            }
            if (dictionary.TryGet("AccentAcrylicBackgroundFillColorBaseBrush", out AcrylicBrush accentAcrylicBackgroundFillColorBaseBrush))
            {
                accentAcrylicBackgroundFillColorBaseBrush.TintLuminosityOpacity = 0.8;
            }
            if (dictionary.TryGet("AccentAcrylicInAppFillColorBaseBrush", out AcrylicBrush accentAcrylicInAppFillColorBaseBrush))
            {
                accentAcrylicInAppFillColorBaseBrush.TintLuminosityOpacity = 0.8;
            }
        }

        #endregion

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
        private static Dictionary<string, SolidColorBrush> _light;
        public static Dictionary<string, SolidColorBrush> Light => _light ??= new()
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
        private static Dictionary<string, SolidColorBrush> _dark;
        public static Dictionary<string, SolidColorBrush> Dark => _dark ??= new()
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

            foreach (var item in Light)
            {
                light[item.Key] = item.Value;
            }

            foreach (var item in Dark)
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

            var target = parent == TelegramTheme.Dark ? Dark : Light;

            foreach (var value in values)
            {
                if (value.Key.EndsWith("Outgoing"))
                {
                    var key = value.Key.Substring(0, value.Key.Length - "Outgoing".Length);
                    
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
                Light["MessageForegroundBrush"].Color = ColorEx.FromHex(0x000000);
                Light["MessageForegroundLinkBrush"].Color = ColorEx.FromHex(0x168ACD);
                Light["MessageBackgroundBrush"].Color = ColorEx.FromHex(0xF0FDDF);
                Light["MessageSubtleLabelBrush"].Color = ColorEx.FromHex(0x6DC264);
                Light["MessageSubtleGlyphBrush"].Color = ColorEx.FromHex(0x5DC452);
                Light["MessageSubtleForegroundBrush"].Color = ColorEx.FromHex(0x6DC264);
                Light["MessageHeaderForegroundBrush"].Color = ColorEx.FromHex(0x3A8E26);
                Light["MessageHeaderBorderBrush"].Color = ColorEx.FromHex(0x5DC452);
                Light["MessageMediaForegroundBrush"].Color = ColorEx.FromHex(0xF0FDDF);
                Light["MessageMediaBackgroundBrush"].Color = ColorEx.FromHex(0x78C67F);
                Light["MessageOverlayBackgroundBrush"].Color = ColorEx.FromHex(0x54000000);
                Light["MessageCallForegroundBrush"].Color = ColorEx.FromHex(0x2AB32A);
                Light["MessageCallMissedForegroundBrush"].Color = ColorEx.FromHex(0xDD5849);
            }
            else
            {
                Dark["MessageForegroundBrush"].Color = ColorEx.FromHex(0xE4ECF2);
                Dark["MessageForegroundLinkBrush"].Color = ColorEx.FromHex(0x83CAFF);
                Dark["MessageBackgroundBrush"].Color = ColorEx.FromHex(0x2B5278);
                Dark["MessageSubtleLabelBrush"].Color = ColorEx.FromHex(0x7DA8D3);
                Dark["MessageSubtleGlyphBrush"].Color = ColorEx.FromHex(0x72BCFD);
                Dark["MessageSubtleForegroundBrush"].Color = ColorEx.FromHex(0x7DA8D3);
                Dark["MessageHeaderForegroundBrush"].Color = ColorEx.FromHex(0x90CAFF);
                Dark["MessageHeaderBorderBrush"].Color = ColorEx.FromHex(0x65B9F4);
                Dark["MessageMediaForegroundBrush"].Color = ColorEx.FromHex(0xFFFFFF);
                Dark["MessageMediaBackgroundBrush"].Color = ColorEx.FromHex(0x4C9CE2);
                Dark["MessageOverlayBackgroundBrush"].Color = ColorEx.FromHex(0x54000000);
                Dark["MessageCallForegroundBrush"].Color = ColorEx.FromHex(0x49A2F0);
                Dark["MessageCallMissedForegroundBrush"].Color = ColorEx.FromHex(0xED5050);
            }
        }
    }
}
