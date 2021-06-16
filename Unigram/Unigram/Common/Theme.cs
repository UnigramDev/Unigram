using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
                Current = this;

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
            //MergedDictionaries.Add(dict);
        }

        public void Update()
        {
            MergedDictionaries.Clear();
            MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Themes/ThemeGreen.xaml") });
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
                        dict[item.Key] = new SolidColorBrush(item.Value);
                    }
                    else if (item.Key.EndsWith("Color"))
                    {
                        dict[item.Key] = item.Value;
                    }
                }

                MergedDictionaries[0].MergedDictionaries.Clear();
                MergedDictionaries[0].MergedDictionaries.Add(dict);
                //MergedDictionaries.Add(dict);
            }
            catch { }
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
}
