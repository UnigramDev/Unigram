using System;
using System.Threading;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation;
using Unigram.Views;
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

    public enum TelegramThemeType
    {
        Classic = 0,
        Day = 1,
        Night = 2,
        Tinted = 3,
        Custom = 4
    }

    public enum NightMode
    {
        Disabled,
        Scheduled,
        Automatic
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
            MigrateTheme();
            UpdateTimer();
        }

        public void UpdateTimer()
        {
            if (NightMode == NightMode.Scheduled && RequestedTheme == ElementTheme.Light)
            {
                var start = DateTime.Today;
                var end = DateTime.Today;

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

        private async void CheckNightModeConditions(object state)
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
                    await window.Dispatcher.DispatchAsync(() =>
                    {
                        window.UpdateTitleBar();

                        if (window.Content is FrameworkElement element)
                        {
                            element.RequestedTheme = theme;
                        }
                    });
                }

                var aggregator = TLContainer.Current.Resolve<IEventAggregator>();
                var protoService = TLContainer.Current.Resolve<IProtoService>();

                aggregator.Publish(new UpdateSelectedBackground(true, protoService.GetSelectedBackground(true)));
                aggregator.Publish(new UpdateSelectedBackground(false, protoService.GetSelectedBackground(false)));
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

        private void MigrateTheme()
        {
            if (_container.Values.TryGet("ThemePath", out string path))
            {
                if (path.EndsWith("Assets\\Themes\\DarkBlue.unigram-theme"))
                {
                    RequestedThemeType = TelegramThemeType.Tinted;
                    Accents[TelegramThemeType.Tinted] = ThemeAccentInfo.Accents[TelegramThemeType.Tinted];
                }
                else if (path.Length > 0 && System.IO.File.Exists(path))
                {
                    RequestedThemeType = TelegramThemeType.Custom;
                    RequestedThemeCustom = path;
                }

                _container.Values.Remove("ThemePath");
            }
            else if (RequestedTheme == ElementTheme.Default)
            {
                var system = GetSystemTheme();
                if (system == TelegramAppTheme.Dark)
                {
                    RequestedTheme = ElementTheme.Dark;
                    RequestedThemeType = TelegramThemeType.Night;
                    Accents[TelegramThemeType.Night] = default;
                }
                else if (system == TelegramAppTheme.Light)
                {
                    RequestedTheme = ElementTheme.Light;
                    RequestedThemeType = TelegramThemeType.Day;
                    Accents[TelegramThemeType.Day] = default;
                }
            }
        }

        private TelegramThemeType? _requestedThemeType;
        public TelegramThemeType RequestedThemeType
        {
            get
            {
                if (_requestedThemeType == null)
                    _requestedThemeType = (TelegramThemeType)GetValueOrDefault(_container, "ThemeType", 0);

                return _requestedThemeType ?? TelegramThemeType.Classic;
            }
            set
            {
                _requestedThemeType = value;
                AddOrUpdateValue(_container, "ThemeType", (int)value);
            }
        }

        private string _requestedThemeCustom;
        public string RequestedThemeCustom
        {
            get
            {
                if (_requestedThemeCustom == null)
                    _requestedThemeCustom = GetValueOrDefault(_container, "ThemeCustom", string.Empty);

                return _requestedThemeCustom ?? string.Empty;
            }
            set
            {
                _requestedThemeCustom = value ?? string.Empty;
                AddOrUpdateValue(_container, "ThemeCustom", value);
            }
        }

        private ThemeSettingsBase _accents;
        public ThemeSettingsBase Accents => _accents ??= new ThemeSettingsBase(_container);

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

        private Location _location;
        public Location Location
        {
            get
            {
                if (_location == null)
                    _location = new Location { Latitude = GetValueOrDefault("Latitude", 0d), Longitude = GetValueOrDefault("Longitude", 0d) };

                return _location ?? new Location();
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
                TimeSpan start;
                TimeSpan end;

                if (IsLocationBased && Location.Latitude != 0 && Location.Longitude != 0)
                {
                    var t = SunDate.CalculateSunriseSunset(Location.Latitude, Location.Longitude);
                    start = new TimeSpan(t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
                    end = new TimeSpan(t[1] / 60, t[1] - (t[1] / 60) * 60, 0);
                }
                else
                {
                    start = start.Add(From);
                    end = end.Add(To);
                }

                var now = DateTime.Now.TimeOfDay;

                // see if start comes before end
                if (start < end)
                    return start <= now && now <= end;

                // start is after end, so do the inverse comparison
                return !(end < now && now < start);
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
            return GetCalculatedApplicationTheme() == ApplicationTheme.Light;

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
            var conditions = CheckNightModeConditions();
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



        private static int? _bubbleRadius;
        public int BubbleRadius
        {
            get
            {
                if (_bubbleRadius == null)
                    _bubbleRadius = GetValueOrDefault("BubbleRadius", 15);

                return _bubbleRadius ?? 15;
            }
            set
            {
                _bubbleRadius = value;
                AddOrUpdateValue("BubbleRadius", value);
            }
        }
    }

    public class ThemeSettingsBase : SettingsServiceBase
    {
        public ThemeSettingsBase(ApplicationDataContainer container)
            : base(container)
        {
        }

        public Color this[TelegramThemeType type]
        {
            get => ColorEx.FromHex(GetValueOrDefault(ConvertToKey(type, "Accent"), ColorEx.ToHex(ThemeAccentInfo.Accents[type])));
            set => AddOrUpdateValue(ConvertToKey(type, "Accent"), ColorEx.ToHex(value));
        }

        private string ConvertToKey(TelegramThemeType type, string key)
        {
            return $"{type}{key}";
        }
    }
}
