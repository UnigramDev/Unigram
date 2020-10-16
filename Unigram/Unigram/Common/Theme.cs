using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.Foundation.Metadata;
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
                Current = this;

                this.Add("MessageServiceForegroundBrush", GetBrushOrDefault("MessageServiceForegroundBrush", Colors.White));
                this.Add("MessageServiceBackgroundBrush", GetBrushOrDefault("MessageServiceBackgroundBrush", Color.FromArgb(0x66, 0x7A, 0x8A, 0x96)));
                this.Add("MessageServiceBackgroundPressedBrush", GetBrushOrDefault("MessageServiceBackgroundPressedBrush", Color.FromArgb(0x88, 0x7A, 0x8A, 0x96)));

                this.Add("MessageServiceBackgroundColor", GetColorOrDefault("MessageServiceBackgroundBrush", Color.FromArgb(0x66, 0x7A, 0x8A, 0x96)));

                this.Add("MessageFontSize", GetValueOrDefault("MessageFontSize", ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7) ? 14d : 15d));

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
            }
            catch { }
        }

        public void Initialize()
        {
            var theme = SettingsService.Current.Appearance.GetCalculatedApplicationTheme();
            if (theme == ApplicationTheme.Dark)
            {
                Initialize(TelegramTheme.Dark);
            }
            else
            {
                Initialize(TelegramTheme.Light);
            }
        }

        public void Initialize(TelegramTheme requested)
        {
            var settings = SettingsService.Current.Appearance;
            if (settings[requested].Type == TelegramThemeType.Custom && File.Exists(settings[requested].Custom))
            {
                UpdateCustom(settings[requested].Custom);
            }
            else if (ThemeAccentInfo.IsAccent(settings[requested].Type))
            {
                Update(ThemeAccentInfo.FromAccent(settings[requested].Type, settings.Accents[settings[requested].Type]));
            }
            else
            {
                Update();
            }
        }

        private void UpdateCustom(string path)
        {
            var lines = File.ReadAllLines(path);
            var dict = new ResourceDictionary();

            var flags = SettingsService.Current.Appearance.RequestedTheme;

            foreach (var line in lines)
            {
                if (line.StartsWith("name: "))
                {
                    continue;
                }
                else if (line.StartsWith("parent: "))
                {
                    flags = (TelegramTheme)int.Parse(line.Substring("parent: ".Length));
                }
                else if (line.Equals("!") || line.Equals("#"))
                {
                    continue;
                }
                else
                {
                    var split = line.Split(':');
                    var key = split[0].Trim();
                    var value = split[1].Trim();

                    if (value.StartsWith("#") && int.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexValue))
                    {
                        byte a = (byte)((hexValue & 0xff000000) >> 24);
                        byte r = (byte)((hexValue & 0x00ff0000) >> 16);
                        byte g = (byte)((hexValue & 0x0000ff00) >> 8);
                        byte b = (byte)(hexValue & 0x000000ff);

                        if (key.EndsWith("Brush"))
                        {
                            dict[key] = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                        }
                        else if (key.EndsWith("Color"))
                        {
                            dict[key] = Color.FromArgb(a, r, g, b);
                        }
                    }
                }
            }

            Update();

            MergedDictionaries[0].MergedDictionaries.Clear();
            MergedDictionaries[0].MergedDictionaries.Add(dict);

        }

        public void Update()
        {
            try
            {
                // Because of Compact, UpdateSource may be executed twice, but there is a bug in XAML and manually clear theme dictionaries here:
                // Prior to RS5, when ResourceDictionary.Source property is changed, XAML forgot to clear ThemeDictionaries.
                ThemeDictionaries.Clear();
                MergedDictionaries.Clear();

                MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/ThemeGreen.xaml") });
            }
            catch { }
        }

        public void Update(ThemeInfoBase info)
        {
            if (info is ThemeCustomInfo custom)
            {
                Update(custom.Values);
            }
            else if (info is ThemeAccentInfo colorized)
            {
                Update(colorized.Values);
            }
            else
            {
                Update();
            }
        }

        private void Update(IDictionary<string, Color> values)
        {
            try
            {
                Update();

                var dict = new ResourceDictionary();

                foreach (var item in values)
                {
                    if (item.Key.EndsWith("Brush"))
                    {
                        dict[item.Key] = new SolidColorBrush((Color)item.Value);
                    }
                    else if (item.Key.EndsWith("Color"))
                    {
                        dict[item.Key] = (Color)item.Value;
                    }
                }

                MergedDictionaries[0].MergedDictionaries.Clear();
                MergedDictionaries[0].MergedDictionaries.Add(dict);
            }
            catch { }
        }

        #region Settings

        public bool AddOrUpdateColor(string key, Color value)
        {
            bool valueChanged = false;

            //var hex = (value.A << 24) | (value.R << 16) | ((byte)(value.G >> 8) << 8) | ((byte)(value.B >> 8));
            var hex = (value.A << 24) | (value.R << 16) | (value.G << 8) | value.B;

            if (isolatedStore.Values.ContainsKey(key))
            {
                if ((int)isolatedStore.Values[key] != hex)
                {
                    isolatedStore.Values[key] = hex;
                    valueChanged = true;
                }
            }
            else
            {
                isolatedStore.Values.Add(key, hex);
                valueChanged = true;
            }

            if (valueChanged)
            {
                try
                {
                    if (this.ContainsKey(key))
                    {
                        this[key] = new SolidColorBrush(value);
                    }
                    else
                    {
                        this.Add(key, new SolidColorBrush(value));
                    }
                }
                catch { }
            }

            return valueChanged;
        }

        public bool AddOrUpdateValue(string key, Object value)
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

        public Brush GetBrushOrDefault(string key, Color defaultValue)
        {
            return new SolidColorBrush(GetColorOrDefault(key, defaultValue));
        }

        public Color GetColorOrDefault(string key, Color defaultValue)
        {
            Color value;

            if (isolatedStore.Values.ContainsKey(key))
            {
                var hex = (int)isolatedStore.Values[key];
                value = Color.FromArgb((byte)((hex >> 24) & 0xff), (byte)((hex >> 16) & 0xff), (byte)((hex >> 8) & 0xff), (byte)(hex & 0xff));
            }
            else
            {
                value = defaultValue;
            }

            return value;
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
}
