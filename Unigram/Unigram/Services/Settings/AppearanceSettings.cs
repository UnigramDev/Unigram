using System;
using System.Threading;
using Template10.Common;
using Unigram.Common;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.UI.Xaml;

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
        private Timer _nightModeTimer;

        public AppearanceSettings()
            : base(ApplicationData.Current.LocalSettings.CreateContainer("Theme", ApplicationDataCreateDisposition.Always))
        {
            _nightModeTimer = new Timer(CheckNightModeConditions, null, Timeout.Infinite, Timeout.Infinite);
            UpdateTimer();
        }

        public void UpdateTimer()
        {
            if (IsNightModeEnabled && RequestedTheme.HasFlag(TelegramTheme.Light))
            {
                var from = DateTime.Now.Date;
                var to = DateTime.Now.Date;

                if (IsLocationBased && Location.Latitude != 0 && Location.Longitude != 0)
                {
                    var t = SunDate.CalculateSunriseSunset(Location.Latitude, Location.Longitude);
                    var sunrise = new TimeSpan(t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
                    var sunset = new TimeSpan(t[1] / 60, t[1] - (t[1] / 60) * 60, 0);

                    from = from.Add(sunset);
                    to = to.AddDays(1).Add(sunrise);
                }
                else
                {
                    from = from.Add(From);
                    to = to.Add(To);

                    if (To < From)
                    {
                        to = to.AddDays(1);
                    }
                }

                var now = DateTime.Now;
                if (now < from)
                {
                    _nightModeTimer.Change(from - now, TimeSpan.Zero);
                }
                else if (now < to)
                {
                    _nightModeTimer.Change(to - now, TimeSpan.Zero);
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

            if (!RequestedTheme.HasFlag(TelegramTheme.Default))
            {
                var conditions = CheckNightModeConditions();
                var theme = conditions == null
                    ? GetElementTheme()
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

        private TelegramTheme? _requestedTheme;
        public TelegramTheme RequestedTheme
        {
            get
            {
                if (_requestedTheme == null)
                    _requestedTheme = (TelegramTheme)GetValueOrDefault(_container, "Theme", (int)(TelegramTheme.Default | TelegramTheme.Brand));

                return _requestedTheme ?? (TelegramTheme.Default | TelegramTheme.Brand);
            }
            set
            {
                _requestedTheme = value;
                AddOrUpdateValue(_container, "Theme", (int)value);
            }
        }

        private bool? _isNightModeEnabled;
        public bool IsNightModeEnabled
        {
            get
            {
                if (_isNightModeEnabled == null)
                    _isNightModeEnabled = GetValueOrDefault(_container, "IsNightModeEnabled", false);

                return _isNightModeEnabled ?? false;
            }
            set
            {
                _isNightModeEnabled = value;
                AddOrUpdateValue(_container, "IsNightModeEnabled", value);
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

        public bool? CheckNightModeConditions()
        {
            if (IsNightModeEnabled && RequestedTheme.HasFlag(TelegramTheme.Light))
            {
                var from = DateTime.Now.Date;
                var to = DateTime.Now.Date;

                if (IsLocationBased && Location.Latitude != 0 && Location.Longitude != 0)
                {
                    var t = SunDate.CalculateSunriseSunset(Location.Latitude, Location.Longitude);
                    var sunrise = new TimeSpan(t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
                    var sunset = new TimeSpan(t[1] / 60, t[1] - (t[1] / 60) * 60, 0);

                    from = from.Add(sunset);
                    to = to.AddDays(1).Add(sunrise);
                }
                else
                {
                    from = from.Add(From);
                    to = to.Add(To);

                    if (To < From)
                    {
                        to = to.AddDays(1);
                    }
                }

                var now = DateTime.Now;
                if (now >= from && now < to)
                {
                    return true;
                }

                return false;
            }

            return null;
        }

        public ElementTheme GetCalculatedElementTheme()
        {
            var conditions = CheckNightModeConditions();
            var theme = conditions == null
                ? GetElementTheme()
                : conditions == true
                ? ElementTheme.Dark
                : ElementTheme.Light;

            return theme;
        }

        public ElementTheme GetElementTheme()
        {
            var theme = RequestedTheme;
            return theme.HasFlag(TelegramTheme.Default)
                ? ElementTheme.Default
                : theme.HasFlag(TelegramTheme.Dark)
                ? ElementTheme.Dark
                : ElementTheme.Light;
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
            return theme.HasFlag(TelegramTheme.Dark)
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
        }
    }
}
