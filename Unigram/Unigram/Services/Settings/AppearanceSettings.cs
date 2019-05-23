using System;
using System.Threading;
using Template10.Common;
using Unigram.Common;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Unigram.Services.Settings
{
    [Flags]
    public enum TelegramTheme
    {
        Light = 1 << 1,
        Dark = 1 << 2,
    }

    [Flags]
    public enum TelegramAppTheme
    {
        Default = 1 << 0,
        Light = 1 << 1,
        Dark = 1 << 2,
    }

    public class InstalledEmojiSet
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public int Version { get; set; }
    }

    public class AppearanceSettings : SettingsServiceBase
    {
        private Timer _nightModeTimer;

        public AppearanceSettings()
            : base(ApplicationData.Current.LocalSettings.CreateContainer("Theme", ApplicationDataCreateDisposition.Always))
        {
            _nightModeTimer = new Timer(CheckNightModeConditions, null, Timeout.Infinite, Timeout.Infinite);
            UpdateTimer();
        }

        public void UpdateTimer()
        {
            if (NightMode == NightMode.Scheduled && RequestedTheme == ElementTheme.Light)
            {
                var start = DateTime.Now.Date;
                var end = DateTime.Now.Date;

                if (IsLocationBased && Location.Latitude != 0 && Location.Longitude != 0)
                {
                    var t = SunDate.CalculateSunriseSunset(Location.Latitude, Location.Longitude);
                    var sunrise = new TimeSpan(t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
                    var sunset = new TimeSpan(t[1] / 60, t[1] - (t[1] / 60) * 60, 0);

                    start = start.Add(sunset);
                    end = end.Add(sunrise);

                    if (sunrise > DateTime.Now.TimeOfDay)
                    {
                        start = start.AddDays(-1);
                    }
                    else if (sunrise < sunset)
                    {
                        end = end.AddDays(1);
                    }
                }
                else
                {
                    start = start.Add(From);
                    end = end.Add(To);

                    if (From < DateTime.Now.TimeOfDay)
                    {
                        start = start.AddDays(-1);
                    }
                    else if (To < From)
                    {
                        end = end.AddDays(1);
                    }
                }

                var now = DateTime.Now;
                if (now < start)
                {
                    _nightModeTimer.Change(start - now, TimeSpan.Zero);
                }
                else if (now < end)
                {
                    _nightModeTimer.Change(end - now, TimeSpan.Zero);
                }
                else
                {
                    _nightModeTimer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }
            else
            {
                _nightModeTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void CheckNightModeConditions(object state)
        {
            UpdateTimer();

            if (RequestedTheme != ElementTheme.Default)
            {
                var conditions = CheckNightModeConditions();
                var theme = conditions == null
                    ? RequestedTheme
                    : conditions == true
                    ? ElementTheme.Dark
                    : ElementTheme.Light;

                foreach (TLWindowContext window in WindowContext.ActiveWrappers)
                {
                    window.Dispatcher.Dispatch(() =>
                    {
                        window.UpdateTitleBar();

                        if (window.Content is FrameworkElement element)
                        {
                            element.RequestedTheme = theme;
                        }
                    });
                }
            }
        }

        private InstalledEmojiSet _emojiSet;
        public InstalledEmojiSet EmojiSet
        {
            get
            {
                if (_emojiSet == null)
                    _emojiSet = new InstalledEmojiSet
                    {
                        Id = GetValueOrDefault(_container, "EmojiSetId", "apple"),
                        Title = GetValueOrDefault(_container, "EmojiSet", "Apple"),
                        Version = GetValueOrDefault(_container, "EmojiSetVersion", 1),
                    };

                return _emojiSet;
            }
            set
            {
                _emojiSet = value ?? GetDefaultEmojiSet();
                AddOrUpdateValue(_container, "EmojiSetId", value?.Id ?? "apple");
                AddOrUpdateValue(_container, "EmojiSet", value?.Title ?? "Apple");
                AddOrUpdateValue(_container, "EmojiSetVersion", value?.Version ?? 1);
            }
        }

        private InstalledEmojiSet GetDefaultEmojiSet()
        {
            return new InstalledEmojiSet
            {
                Id = "apple",
                Title = "Apple",
                Version = 1
            };
        }

        private string _requestedThemePath;
        public string RequestedThemePath
        {
            get
            {
                if (_requestedThemePath == null)
                    _requestedThemePath = GetValueOrDefault(_container, "ThemePath", string.Empty);

                return _requestedThemePath ?? string.Empty;
            }
            set
            {
                _requestedThemePath = value ?? string.Empty;
                AddOrUpdateValue(_container, "ThemePath", value);
            }
        }

        private ElementTheme? _requestedTheme;
        public ElementTheme RequestedTheme
        {
            get
            {
                if (_requestedTheme == null)
                {
                    var theme = (TelegramAppTheme)GetValueOrDefault(_container, "Theme", (int)GetSystemTheme());
                    if (theme.HasFlag(TelegramAppTheme.Dark))
                    {
                        _requestedTheme = ElementTheme.Dark;
                    }
                    else if (theme.HasFlag(TelegramAppTheme.Light))
                    {
                        _requestedTheme = ElementTheme.Light;
                    }
                    else
                    {
                        _requestedTheme = ElementTheme.Default;
                    }
                }

                return _requestedTheme ?? (GetSystemTheme() == TelegramAppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light);
            }
            set
            {
                _requestedTheme = value;

                var theme = value == ElementTheme.Default
                    ? TelegramAppTheme.Default
                    : value == ElementTheme.Dark
                    ? TelegramAppTheme.Dark
                    : TelegramAppTheme.Light;

                AddOrUpdateValue(_container, "Theme", (int)theme);
            }
        }

        private NightMode? _nightMode;
        public NightMode NightMode
        {
            get
            {
                if (_nightMode == null)
                    _nightMode = (NightMode)GetValueOrDefault(_container, "NightMode", (int)NightMode.Disabled);

                return _nightMode ?? NightMode.Disabled;
            }
            set
            {
                _nightMode = value;
                AddOrUpdateValue(_container, "NightMode", (int)value);
                UpdateTimer();
            }
        }

        private bool? _isLocationBased;
        public bool IsLocationBased
        {
            get
            {
                if (_isLocationBased == null)
                    _isLocationBased = GetValueOrDefault(_container, "IsLocationBased", false);

                return _isLocationBased ?? false;
            }
            set
            {
                _isLocationBased = value;
                AddOrUpdateValue(_container, "IsLocationBased", value);
            }
        }

        private TimeSpan? _from;
        public TimeSpan From
        {
            get
            {
                if (_from == null)
                {
                    var value = GetValueOrDefault("From", 22 * 60 + 0);
                    var currentHour = value / 60;

                    _from = new TimeSpan(currentHour, value - currentHour * 60, 0);
                }

                return _from ?? new TimeSpan(22, 0, 0);
            }
            set
            {
                _from = value;
                AddOrUpdateValue("From", value.Hours * 60 + value.Minutes);
            }
        }

        private TimeSpan? _to;
        public TimeSpan To
        {
            get
            {
                if (_to == null)
                {
                    var value = GetValueOrDefault("To", 9 * 60 + 0);
                    var currentHour = value / 60;

                    _to = new TimeSpan(currentHour, value - currentHour * 60, 0);
                }

                return _to ?? new TimeSpan(9, 0, 0);
            }
            set
            {
                _to = value;
                AddOrUpdateValue("To", value.Hours * 60 + value.Minutes);
            }
        }

        private BasicGeoposition? _location;
        public BasicGeoposition Location
        {
            get
            {
                if (_location == null)
                    _location = new BasicGeoposition { Latitude = GetValueOrDefault("Latitude", 0d), Longitude = GetValueOrDefault("Longitude", 0d) };

                return _location ?? new BasicGeoposition();
            }
            set
            {
                _location = value;
                AddOrUpdateValue("Latitude", value.Latitude);
                AddOrUpdateValue("Longitude", value.Longitude);
            }
        }

        private string _town;
        public string Town
        {
            get
            {
                if (_town == null)
                    _town = GetValueOrDefault("Town", string.Empty);

                return _town;
            }
            set
            {
                _town = value;
                AddOrUpdateValue("Town", value);
            }
        }

        private float? _threshold;
        public float Threshold
        {
            get
            {
                if (_threshold == null)
                    _threshold = GetValueOrDefault("Threshold", 0.25f);

                return _threshold ?? 0.25f;
            }
            set
            {
                _threshold = value;
                AddOrUpdateValue("Threshold", value);
            }
        }

        public bool? CheckNightModeConditions()
        {
            if (NightMode == NightMode.Scheduled && RequestedTheme == ElementTheme.Light)
            {
                var start = DateTime.Now.Date;
                var end = DateTime.Now.Date;

                if (IsLocationBased && Location.Latitude != 0 && Location.Longitude != 0)
                {
                    var t = SunDate.CalculateSunriseSunset(Location.Latitude, Location.Longitude);
                    var sunrise = new TimeSpan(t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
                    var sunset = new TimeSpan(t[1] / 60, t[1] - (t[1] / 60) * 60, 0);

                    start = start.Add(sunset);
                    end = end.Add(sunrise);

                    if (sunrise > DateTime.Now.TimeOfDay)
                    {
                        start = start.AddDays(-1);
                    }
                    else if (sunrise < sunset)
                    {
                        end = end.AddDays(1);
                    }
                }
                else
                {
                    start = start.Add(From);
                    end = end.Add(To);

                    if (From < DateTime.Now.TimeOfDay)
                    {
                        start = start.AddDays(-1);
                    }
                    else if (To < From)
                    {
                        end = end.AddDays(1);
                    }
                }

                var now = DateTime.Now;
                if (now >= start && now < end)
                {
                    return true;
                }
            }

            return null;
        }

        public ElementTheme GetCalculatedElementTheme()
        {
            var conditions = CheckNightModeConditions();
            var theme = conditions == null
                ? GetActualTheme()
                : conditions == true
                ? ElementTheme.Dark
                : ElementTheme.Light;

            return theme;
        }

        public ElementTheme GetActualTheme()
        {
            var app = App.Current as App;
            var current = app.UISettings.GetColorValue(UIColorType.Background);

            var theme = RequestedTheme;
            if (theme == ElementTheme.Dark || (theme == ElementTheme.Default && current == Colors.Black))
            {
                return ElementTheme.Dark;
            }
            //else if (theme == ElementTheme.Light || (theme == ElementTheme.Default && current == Colors.White))
            //{
            //    return ElementTheme.Light;
            //}

            return ElementTheme.Light;
        }

        public TelegramAppTheme GetSystemTheme()
        {
            var app = App.Current as App;
            var current = app.UISettings.GetColorValue(UIColorType.Background);

            return current == Colors.Black ? TelegramAppTheme.Dark : TelegramAppTheme.Light;
        }

        public bool IsLightTheme()
        {
            var app = App.Current as App;
            var current = app.UISettings.GetColorValue(UIColorType.Background);

            var theme = RequestedTheme;
            if (theme == ElementTheme.Dark || (theme == ElementTheme.Default && current == Colors.Black))
            {
                return false;
            }
            //else if (theme == ElementTheme.Light || (theme == ElementTheme.Default && current == Colors.White))
            //{
            //    return ElementTheme.Light;
            //}

            return true;
        }

        public bool IsDarkTheme()
        {
            return !IsLightTheme();
        }

        public ApplicationTheme GetCalculatedApplicationTheme()
        {
            var conditions = SettingsService.Current.Appearance.CheckNightModeConditions();
            var theme = conditions == null
                ? SettingsService.Current.Appearance.GetApplicationTheme()
                : conditions == true
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;

            return theme;
        }

        public ApplicationTheme GetApplicationTheme()
        {
            var theme = RequestedTheme;
            return theme.HasFlag(ElementTheme.Dark)
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
        }
    }

    public enum NightMode
    {
        Disabled,
        Scheduled,
        Automatic
    }
}
