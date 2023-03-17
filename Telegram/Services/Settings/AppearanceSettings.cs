//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.Views;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace Telegram.Services.Settings
{
    public enum TelegramTheme
    {
        Light = 1 << 1,
        Dark = 1 << 2
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
        Automatic,
        System
    }

    public enum AccentShade
    {
        Default,
        Light1,
        Light2,
        Light3,
        Dark1,
        Dark2,
        Dark3
    }

    public readonly struct Acrylic
    {
        public static Acrylic<Color> Color(Color tint, Color fallback, double opacity, double? luminosity = null)
        {
            return new Acrylic<Color>(tint, fallback, opacity, luminosity);
        }

        public static Acrylic<AccentShade> Shade(AccentShade tint, AccentShade fallback, double opacity, double? luminosity = null)
        {
            return new Acrylic<AccentShade>(tint, fallback, opacity, luminosity);
        }
    }

    public readonly struct Acrylic<T> where T : struct
    {
        public T TintColor { get; }

        public T FallbackColor { get; }

        public double TintOpacity { get; }

        public double? TintLuminosityOpacity { get; }

        public Acrylic(T tint, T fallback, double opacity, double? tonality = null)
        {
            TintColor = tint;
            FallbackColor = fallback;
            TintOpacity = opacity;
            TintLuminosityOpacity = tonality;
        }
    }

    public class InstalledEmojiSet
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public int Version { get; set; }
    }

    public class AppearanceSettings : SettingsServiceBase
    {
        private readonly UISettings _uiSettings;

        private readonly Timer _nightModeTimer;

        public AppearanceSettings()
            : base("Theme")
        {
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += OnColorValuesChanged;

            _nightModeTimer = new Timer(CheckNightModeConditions, null, Timeout.Infinite, Timeout.Infinite);
            MigrateTheme();
            UpdateTimer();
        }

        private void OnColorValuesChanged(UISettings sender, object args)
        {
            UpdateNightMode(null);
        }

        public void UpdateTimer()
        {
            if (NightMode == NightMode.Scheduled && RequestedTheme == TelegramTheme.Light)
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

        private void CheckNightModeConditions(object state)
        {
            UpdateNightMode(false);
        }

        public void UpdateNightMode(bool? force = false, bool updateBackground = true)
        {
            // Same theme:
            // - false: update dictionaries
            // - null:  do nothing
            // - true:  as different theme
            // Different theme:
            // - false: update dictionaries, switch theme
            // - null:  switch theme
            // - true.  update dictionaries, double switch theme

            UpdateTimer();

            var conditions = CheckNightModeConditions();
            var theme = conditions == null
                ? GetActualTheme()
                : conditions == true
                ? ElementTheme.Dark
                : ElementTheme.Light;

            WindowContext.ForEach(window =>
            {
                if (force is not null)
                {
                    Theme.Current.Update(theme);
                }

                if (window.ActualTheme != theme || force is true)
                {
                    window.UpdateTitleBar();

                    // This should be no longer needed
                    if (force is true)
                    {
                        window.RequestedTheme = theme == ElementTheme.Dark
                            ? ElementTheme.Light
                            : ElementTheme.Dark;
                    }

                    window.RequestedTheme = theme;
                }
            });

            if (updateBackground)
            {
                var aggregator = TLContainer.Current.Resolve<IEventAggregator>();
                var clientService = TLContainer.Current.Resolve<IClientService>();

                var dark = theme == ElementTheme.Dark;
                aggregator.Publish(new UpdateSelectedBackground(dark, clientService.GetSelectedBackground(dark)));
            }
        }

        private string _emojiSet;
        public string EmojiSet
        {
            get => _emojiSet ??= GetValueOrDefault(_container, "EmojiSetId", "apple");
            set => AddOrUpdateValue(ref _emojiSet, _container, "EmojiSetId", value);
        }

        private void MigrateTheme()
        {
            if (_container.Values.TryGet("ThemePath", out string path))
            {
                if (path.EndsWith("Assets\\Themes\\DarkBlue.unigram-theme"))
                {
                    RequestedTheme = TelegramTheme.Dark;
                    this[TelegramTheme.Dark].Type = TelegramThemeType.Tinted;
                    Accents[TelegramThemeType.Tinted] = ThemeInfoBase.Accents[TelegramThemeType.Tinted][AccentShade.Default];
                }
                else if (path.Length > 0 && System.IO.File.Exists(path))
                {
                    this[RequestedTheme].Type = TelegramThemeType.Custom;
                    this[RequestedTheme].Custom = path;
                }

                _container.Values.Remove("ThemePath");
            }
            else if (_container.Values.TryGet("ThemeType", out int type))
            {
                this[RequestedTheme].Type = (TelegramThemeType)type;

                if ((TelegramThemeType)type == TelegramThemeType.Custom && _container.Values.TryGet("ThemeCustom", out string custom))
                {
                    if (custom.Length > 0 && System.IO.File.Exists(custom))
                    {
                        this[RequestedTheme].Type = TelegramThemeType.Custom;
                        this[RequestedTheme].Custom = custom;
                    }
                }

                _container.Values.Remove("ThemeCustom");
                _container.Values.Remove("ThemeType");
            }
        }

        private ThemeSettingsBase _themeLight;
        private ThemeSettingsBase _themeDark;

        public ThemeSettingsBase this[TelegramTheme type]
        {
            get
            {
                if (type == TelegramTheme.Light)
                {
                    return _themeLight ??= new ThemeSettingsBase(_container, TelegramTheme.Light);
                }

                return _themeDark ??= new ThemeSettingsBase(_container, TelegramTheme.Dark);
            }
        }

        private ThemeTypeSettingsBase _accents;
        public ThemeTypeSettingsBase Accents => _accents ??= new ThemeTypeSettingsBase(_container);

        private TelegramTheme? _requestedTheme;
        public TelegramTheme RequestedTheme
        {
            get
            {
                if (_requestedTheme == null)
                {
                    _requestedTheme = (TelegramTheme)GetValueOrDefault(_container, "Theme", (int)GetSystemTheme());
                }

                return _requestedTheme ?? GetSystemTheme();
            }
            set
            {
                _requestedTheme = value;
                AddOrUpdateValue(_container, "Theme", (int)value);
            }
        }

        private NightMode? _nightMode;
        public NightMode NightMode
        {
            get
            {
                if (_nightMode == null)
                {
                    _nightMode = (NightMode)GetValueOrDefault(_container, "NightMode", (int)NightMode.Disabled);
                }

                return _nightMode ?? NightMode.Disabled;
            }
            set
            {
                _nightMode = value;
                AddOrUpdateValue(_container, "NightMode", (int)value);
                UpdateTimer();
            }
        }

        private bool? _forceNightMode;
        public bool ForceNightMode
        {
            get => _forceNightMode ??= GetValueOrDefault(_container, "ForceNightMode", false);
            set => AddOrUpdateValue(ref _forceNightMode, _container, "ForceNightMode", value);
        }

        private bool? _isLocationBased;
        public bool IsLocationBased
        {
            get
            {
                if (_isLocationBased == null)
                {
                    _isLocationBased = GetValueOrDefault(_container, "IsLocationBased", false);
                }

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
                {
                    _location = new Location { Latitude = GetValueOrDefault("Latitude", 0d), Longitude = GetValueOrDefault("Longitude", 0d) };
                }

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
                {
                    _town = GetValueOrDefault("Town", string.Empty);
                }

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
                {
                    _threshold = GetValueOrDefault("Threshold", 0.25f);
                }

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
            if (ForceNightMode)
            {
                return true;
            }
            else if (NightMode == NightMode.Scheduled && RequestedTheme == TelegramTheme.Light)
            {
                TimeSpan start;
                TimeSpan end;

                if (IsLocationBased && Location.Latitude != 0 && Location.Longitude != 0)
                {
                    var t = SunDate.CalculateSunriseSunset(Location.Latitude, Location.Longitude);
                    start = new TimeSpan(t[1] / 60, t[1] - (t[1] / 60) * 60, 0);
                    end = new TimeSpan(t[0] / 60, t[0] - (t[0] / 60) * 60, 0);
                }
                else
                {
                    start = start.Add(From);
                    end = end.Add(To);
                }

                return DateTime.Now.TimeOfDay.IsBetween(start, end);
            }
            else if (NightMode == NightMode.System)
            {
                return GetSystemTheme() == TelegramTheme.Dark;
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
            var theme = RequestedTheme;
            return theme == TelegramTheme.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }

        public TelegramTheme GetSystemTheme()
        {
            var app = BootStrapper.Current as App;
            var current = app.UISettings.GetColorValue(UIColorType.Background);

            return current == Colors.Black ? TelegramTheme.Dark : TelegramTheme.Light;
        }

        public bool IsLightTheme()
        {
            return GetCalculatedApplicationTheme() == ApplicationTheme.Light;
        }

        public bool IsDarkTheme()
        {
            return GetCalculatedApplicationTheme() == ApplicationTheme.Dark;
        }

        public ApplicationTheme GetCalculatedApplicationTheme()
        {
            var conditions = CheckNightModeConditions();
            var theme = conditions == null
                ? GetApplicationTheme()
                : conditions == true
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;

            return theme;
        }

        public ApplicationTheme GetApplicationTheme()
        {
            var theme = RequestedTheme;
            return theme == TelegramTheme.Dark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
        }



        private static int? _bubbleRadius;
        public int BubbleRadius
        {
            get => _bubbleRadius ??= GetValueOrDefault("BubbleRadius", 15);
            set => AddOrUpdateValue(ref _bubbleRadius, "BubbleRadius", value);
        }

        private bool? _isQuickReplySelected;
        public bool IsQuickReplySelected
        {
            get => _isQuickReplySelected ??= GetValueOrDefault("IsQuickReplySelected", true);
            set => AddOrUpdateValue(ref _isQuickReplySelected, "IsQuickReplySelected", value);
        }

        private bool _chatThemeLoaded;

        private ChatTheme _chatTheme;
        public ChatTheme ChatTheme
        {
            get => _chatTheme ??= LoadChatTheme();
            set => SaveChatTheme(value);
        }


        private void SaveChatTheme(ChatTheme theme)
        {
            if (theme?.Name == "\U0001F3E0")
            {
                theme = null;
            }

            if (theme != null)
            {
                var light = _container.CreateContainer("ChatThemeLight", ApplicationDataCreateDisposition.Always);
                var dark = _container.CreateContainer("ChatThemeDark", ApplicationDataCreateDisposition.Always);

                AddOrUpdateValue("ChatThemeName", theme.Name);
                SaveChatThemeSettings(light, theme.LightSettings);
                SaveChatThemeSettings(dark, theme.DarkSettings);
            }
            else
            {
                _container.Values.Remove("ChatThemeName");
                _container.DeleteContainer("ChatThemeLight");
                _container.DeleteContainer("ChatThemeDark");
            }

            _chatTheme = theme;
        }

        private void SaveChatThemeSettings(ApplicationDataContainer container, ThemeSettings settings)
        {
            AddOrUpdateValue(container, "OutgoingMessageAccentColor", settings.OutgoingMessageAccentColor);
            AddOrUpdateValue(container, "OutgoingMessageFill", TdBackground.ToString(settings.OutgoingMessageFill));
            AddOrUpdateValue(container, "AnimateOutgoingMessageFill", settings.AnimateOutgoingMessageFill);
            AddOrUpdateValue(container, "AccentColor", settings.AccentColor);
        }

        private ChatTheme LoadChatTheme()
        {
            if (_chatThemeLoaded)
            {
                return _chatTheme;
            }

            _chatThemeLoaded = true;

            var name = GetValueOrDefault<string>("ChatThemeName", null);
            if (name != null)
            {
                var light = _container.CreateContainer("ChatThemeLight", ApplicationDataCreateDisposition.Always);
                var dark = _container.CreateContainer("ChatThemeDark", ApplicationDataCreateDisposition.Always);

                return new ChatTheme
                {
                    Name = name,
                    LightSettings = LoadChatThemeSettings(light),
                    DarkSettings = LoadChatThemeSettings(dark)
                };
            }

            return null;
        }

        private ThemeSettings LoadChatThemeSettings(ApplicationDataContainer container)
        {
            return new ThemeSettings
            {
                OutgoingMessageAccentColor = GetValueOrDefault(container, "OutgoingMessageAccentColor", 0),
                OutgoingMessageFill = TdBackground.FromString(GetValueOrDefault(container, "OutgoingMessageFill", string.Empty)),
                AnimateOutgoingMessageFill = GetValueOrDefault(container, "AnimateOutgoingMessageFill", false),
                AccentColor = GetValueOrDefault(container, "AccentColor", 0)
            };
        }
    }

    public class ThemeTypeSettingsBase : SettingsServiceBase
    {
        public ThemeTypeSettingsBase(ApplicationDataContainer container)
            : base(container)
        {
        }

        public Color this[TelegramThemeType type]
        {
            get => ColorEx.FromHex(GetValueOrDefault(ConvertToKey(type, "Accent"), ColorEx.ToHex(ThemeInfoBase.Accents[type][AccentShade.Default])), true);
            set => AddOrUpdateValue(ConvertToKey(type, "Accent"), ColorEx.ToHex(value));
        }

        private string ConvertToKey(TelegramThemeType type, string key)
        {
            return $"{type}{key}";
        }
    }

    public class ThemeSettingsBase : SettingsServiceBase
    {
        private readonly TelegramTheme _prefix;

        public ThemeSettingsBase(ApplicationDataContainer container, TelegramTheme prefix)
            : base(container)
        {
            _prefix = prefix;
        }

        private TelegramThemeType? _type;
        public TelegramThemeType Type
        {
            get
            {
                if (_type == null)
                {
                    _type = (TelegramThemeType)GetValueOrDefault(_container, $"ThemeType{_prefix}", 0);

                    if (_prefix == TelegramTheme.Dark && (_type == TelegramThemeType.Classic || _type == TelegramThemeType.Day))
                    {
                        _type = TelegramThemeType.Night;
                    }
                    else if (_prefix == TelegramTheme.Light && (_type == TelegramThemeType.Night || _type == TelegramThemeType.Tinted))
                    {
                        _type = TelegramThemeType.Classic;
                    }
                }

                return _type ?? TelegramThemeType.Classic;
            }
            set
            {
                if (_prefix == TelegramTheme.Dark && (value == TelegramThemeType.Classic || value == TelegramThemeType.Day))
                {
                    value = TelegramThemeType.Night;
                }
                else if (_prefix == TelegramTheme.Light && (value == TelegramThemeType.Night || value == TelegramThemeType.Tinted))
                {
                    value = TelegramThemeType.Classic;
                }

                _type = value;
                AddOrUpdateValue(_container, $"ThemeType{_prefix}", (int)value);
            }
        }

        private string _custom;
        public string Custom
        {
            get
            {
                if (_custom == null)
                {
                    _custom = GetValueOrDefault(_container, $"ThemeCustom{_prefix}", string.Empty);
                }

                return _custom ?? string.Empty;
            }
            set
            {
                _custom = value ?? string.Empty;
                AddOrUpdateValue(_container, $"ThemeCustom{_prefix}", value);
            }
        }
    }
}
