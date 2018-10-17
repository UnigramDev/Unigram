using System;
using Windows.Storage;

namespace Unigram.Services.Settings
{
    [Flags]
    public enum TelegramTheme
    {
        Default = 1 << 0,
        Light = 1 << 1,
        Dark = 1 << 2,

        Brand = 1 << 3,
        Custom = 1 << 4,
    }

    public class AppearanceSettings : SettingsServiceBase
    {
        public AppearanceSettings()
            : base(ApplicationData.Current.LocalSettings.CreateContainer("Theme", ApplicationDataCreateDisposition.Always))
        {

        }

        private TelegramTheme? _requestedTheme;
        public TelegramTheme RequestedTheme
        {
            get
            {
                if (_requestedTheme == null)
                {
                    _requestedTheme = (TelegramTheme)GetValueOrDefault(_container, "Theme", (int)(TelegramTheme.Default | TelegramTheme.Brand));
                }

                return _requestedTheme ?? (TelegramTheme.Default | TelegramTheme.Brand);
            }
            set
            {
                _requestedTheme = value;
                AddOrUpdateValue(_container, "Theme", (int)value);
            }
        }
    }
}
