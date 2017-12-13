using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace Unigram.Common
{
    public class Theme : ResourceDictionary
    {
        public static Theme Current { get; private set; }

        private readonly ApplicationDataContainer isolatedStore;

        public Theme()
        {
            isolatedStore = ApplicationData.Current.LocalSettings.CreateContainer("Theme", ApplicationDataCreateDisposition.Always);
            Current = this;
            Update();

            this.Add("MessageServiceForegroundBrush", GetBrushOrDefault("MessageServiceForegroundBrush", Colors.White));
            this.Add("MessageServiceBackgroundBrush", GetBrushOrDefault("MessageServiceBackgroundBrush", Color.FromArgb(0x66, 0x7A, 0x8A, 0x96)));
            this.Add("MessageServiceBackgroundPressedBrush", GetBrushOrDefault("MessageServiceBackgroundPressedBrush", Color.FromArgb(0x88, 0x7A, 0x8A, 0x96)));

            this.Add("MessageFontSize", GetValueOrDefault("MessageFontSize", 15d));
        }

        public void Update()
        {
            var accent = App.Current.Resources.MergedDictionaries.FirstOrDefault(x => x.Source.AbsoluteUri.EndsWith("Accent.xaml"));
            if (accent == null)
            {
                return;
            }

            var fileName = FileUtils.GetFileName("colors.palette");
            if (File.Exists(fileName))
            {
                var text = File.ReadAllText(fileName);

                try
                {
                    var dictionary = XamlReader.Load(text) as ResourceDictionary;
                    if (dictionary == null)
                    {
                        return;
                    }

                    accent.MergedDictionaries.Clear();
                    accent.MergedDictionaries.Add(dictionary);
                }
                catch
                {
                    File.Delete(fileName);
                    Update();
                }
            }
            else
            {
                accent.MergedDictionaries.Clear();
            }
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
                if (this.ContainsKey(key))
                {
                    this[key] = new SolidColorBrush(value);
                }
                else
                {
                    this.Add(key, new SolidColorBrush(value));
                }
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
                if (this.ContainsKey(key))
                {
                    this[key] = value;
                }
                else
                {
                    this.Add(key, value);
                }
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
