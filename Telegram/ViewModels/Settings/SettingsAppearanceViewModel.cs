//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.Storage;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsAppearanceViewModel : ViewModelBase
    {
        private readonly IThemeService _themeService;

        public SettingsAppearanceViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService)
            : base(clientService, settingsService, aggregator)
        {
            _themeService = themeService;

            var fonts = Direct2D.Current.GetSystemFontFamilies(new[] { LocaleService.Current.Id, NativeUtils.GetCurrentCulture() })
                .OrderBy(x => x)
                .Select(x => new SettingsOptionFontFamily(x, x, x));

            FontFamilyOptions = new List<SettingsOptionFontFamily>(fonts);
            FontFamilyOptions.Insert(0, new SettingsOptionFontFamily(string.Empty, Strings.Default, Windows.UI.Xaml.Media.FontFamily.XamlAutoFontFamily.Source));

            ChatThemes = new RangeObservableCollection<ChatThemeViewModel>();

            var stored = AppSettings.Appearance.Scaling;

            if (AppSettings.Appearance.UseDefaultScaling)
            {
                stored = 0;
            }

            _scaling = stored;
        }

        public RangeObservableCollection<ChatThemeViewModel> ChatThemes { get; }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            ChatThemes.SwitchTo(await GetAppThemes());

            _selectedChatTheme = GetSelectedTheme();
            RaisePropertyChanged(nameof(SelectedChatTheme));
        }

        private ChatThemeViewModel _defaultTheme;
        private ChatThemeViewModel _customTheme;

        // Each base keeps its own 🎨 pointer, so a light/dark flip can add the card or take it away.
        private void ApplyCustomTheme(IList<ChatThemeViewModel> themes)
        {
            var target = NightModeService.Current.IsLightTheme()
                ? _customTheme.LightSettings
                : _customTheme.DarkSettings;

            if (target != null)
            {
                if (themes.Contains(_customTheme))
                {
                    return;
                }

                themes.Add(_customTheme);
            }
            else
            {
                themes.Remove(_customTheme);
            }
        }

        public void UpdateActualTheme(bool refresh)
        {
            if (ChatThemes.Empty())
            {
                return;
            }

            ApplyCustomTheme(ChatThemes);

            _selectedChatTheme = GetSelectedTheme();
            SelectionChanged = refresh;
            RaisePropertyChanged(nameof(SelectedChatTheme));
        }

        private ChatThemeViewModel GetSelectedTheme()
        {
            var requested = NightModeService.Current.IsDarkTheme() ? TelegramTheme.Dark : TelegramTheme.Light;
            var worn = AppSettings.Appearance.GetWorn(requested);

            // A variant or a theme file is what the 🎨 card points at: wearing one moves it there.
            if (worn.Kind != ThemeKind.Preset)
            {
                return ChatThemes.Contains(_customTheme) ? _customTheme : _defaultTheme;
            }

            return ChatThemes.FirstOrDefault(x => x.AreTheSame(worn.Id)) ?? _defaultTheme;
        }

        private async Task<List<ChatThemeViewModel>> GetAppThemes()
        {
            var defaultLight = await LoadPresetAsync(TelegramTheme.Light, ThemeData.DefaultThemeId, AppearanceSettings.GetHouse(TelegramTheme.Light));
            var defaultDark = await LoadPresetAsync(TelegramTheme.Dark, ThemeData.DefaultThemeId, AppearanceSettings.GetHouse(TelegramTheme.Dark));

            _defaultTheme = new ChatThemeViewModel(ClientService, ThemeData.DefaultThemeId, defaultLight, defaultDark, false);

            var customLight = await LoadRecentAsync(TelegramTheme.Light);
            var customDark = await LoadRecentAsync(TelegramTheme.Dark);

            _customTheme = new ChatThemeViewModel(ClientService, ThemeData.CustomThemeId, customLight, customDark, false);

            var themes = new List<ChatThemeViewModel>
            {
                _defaultTheme
            };

            foreach (var theme in ClientService.ChatThemes)
            {
                var lightSettings = await LoadPresetAsync(TelegramTheme.Light, theme.Name, theme.LightSettings);
                var darkSettings = await LoadPresetAsync(TelegramTheme.Dark, theme.Name, theme.DarkSettings);

                themes.Add(new ChatThemeViewModel(ClientService, theme.Name, lightSettings, darkSettings, false));
            }

            ApplyCustomTheme(themes);

            return themes;
        }

        /// <summary>
        /// A preset as <see cref="AppearanceSettings.WearPreset"/> would wear it: these colours, with
        /// the background it remembers, or the one it comes with if it was never worn.
        /// </summary>
        private async Task<ThemeSettings> LoadPresetAsync(TelegramTheme requested, string emoji, ThemeSettings colors)
        {
            if (colors == null)
            {
                return null;
            }

            var source = AppSettings.Appearance.TryGetBackground(requested, ThemeIdentity.Preset(emoji), out Background background)
                ? ThemeSettingsStore.WithBackground(colors, background)
                : null;

            return await ThemeData.LoadThemeSettings(ClientService, colors.BaseTheme, emoji, source, colors);
        }

        /// <summary>
        /// What the 🎨 card shows for one base: the variant it points at, or a theme file through the
        /// settings of its header - the cell can only colorize.
        /// </summary>
        private async Task<ThemeSettings> LoadRecentAsync(TelegramTheme requested)
        {
            if (!AppSettings.Appearance.TryGetRecent(requested, out ThemeIdentity identity))
            {
                return null;
            }

            ThemeSettings settings;

            if (identity.Kind == ThemeKind.Variant)
            {
                settings = AppSettings.Appearance.Variants.Get(identity.Type, identity.Id);

                if (settings == null)
                {
                    return null;
                }
            }
            else
            {
                ThemeCustomInfo info;

                try
                {
                    var file = await StorageFile.GetFileFromPathAsync(AppearanceSettings.GetThemeFilePath(identity.Id));
                    info = await ThemeCustomInfo.FromFileAsync(ClientService, file);
                }
                catch
                {
                    // Deleted behind our back: no card is the honest answer.
                    return null;
                }

                if (info == null)
                {
                    return null;
                }

                AppSettings.Appearance.TryGetBackground(requested, identity, out Background background);

                if (info.Settings != null)
                {
                    settings = ThemeSettingsStore.WithBackground(info.Settings, background);
                }
                else
                {
                    // A v1 file has no settings to colorize from, so its accent over the base's
                    // built-in is the closest the cell can draw.
                    var accent = info.AccentColor.ToValue();
                    settings = new ThemeSettings
                    {
                        BaseTheme = AppearanceSettings.GetHouse(requested).BaseTheme,
                        AccentColor = accent,
                        OutgoingMessageAccentColor = accent,
                        Background = background
                    };
                }
            }

            return await ThemeData.LoadThemeSettings(ClientService, settings.BaseTheme, ThemeData.CustomThemeId, settings, null);
        }

        private ChatThemeViewModel _selectedChatTheme;
        public ChatThemeViewModel SelectedChatTheme
        {
            get => _selectedChatTheme;
            set => SetChatTheme(value);
        }

        public bool SelectionChanged { get; private set; }

        private void SetChatTheme(ChatThemeViewModel chatTheme)
        {
            if (chatTheme == null || chatTheme.AreTheSame(_selectedChatTheme?.Type))
            {
                return;
            }

            var dark = NightModeService.Current.IsDarkTheme();
            var requested = dark ? TelegramTheme.Dark : TelegramTheme.Light;

            if (chatTheme == _customTheme)
            {
                if (!AppSettings.Appearance.WearRecent(requested))
                {
                    return;
                }
            }
            else if (!AppSettings.Appearance.WearPreset(requested, (chatTheme.Type as ChatThemeEmoji)?.Name, dark ? chatTheme.DarkSettings : chatTheme.LightSettings))
            {
                return;
            }

            // After the theme is worn: a theme with no background yet adopts the update this
            // produces, and it must be adopted by this theme rather than the previous one.
            ThemeData.SendDefaultBackground(ClientService, dark ? chatTheme.DarkSettings?.Background : chatTheme.LightSettings?.Background, dark);

            NightModeService.Current.Update(updateBackground: false);

            _selectedChatTheme = chatTheme;
            SelectionChanged = true;
            RaisePropertyChanged(nameof(SelectedChatTheme));
        }

        public NightMode NightMode => AppSettings.Appearance.NightMode;

        private string _emojiSet;
        public string EmojiSet
        {
            get => _emojiSet;
            set => Set(ref _emojiSet, value);
        }

        private string _emojiSetId;
        public string EmojiSetId
        {
            get => _emojiSetId;
            set => Set(ref _emojiSetId, value);
        }

        private int _scaling;
        public int Scaling
        {
            get => Array.IndexOf(_scalingIndexer, _scaling);
            set
            {
                if (value >= 0 && value < _scalingIndexer.Length && _scaling != _scalingIndexer[value])
                {
                    var scaling = _scalingIndexer[value];
                    if (scaling == 0)
                    {
                        NativeUtils.OverrideScaleForCurrentView(AppSettings.Appearance.Scaling = _scaling = NativeUtils.GetScaleForCurrentView());
                        AppSettings.Appearance.UseDefaultScaling = true;
                    }
                    else
                    {
                        NativeUtils.OverrideScaleForCurrentView(AppSettings.Appearance.Scaling = _scaling = scaling);
                        AppSettings.Appearance.UseDefaultScaling = false;
                    }

                    RaisePropertyChanged();
                }
            }
        }

        private readonly int[] _scalingIndexer = new[]
        {
            0,
            100,
            125,
            150,
            175,
            200,
            225,
            250
        };

        public List<SettingsOptionItem<int>> ScalingOptions { get; } = new()
        {
            new SettingsOptionItem<int>(0, Strings.Default),
            new SettingsOptionItem<int>(100, "100%"),
            new SettingsOptionItem<int>(125, "125%"),
            new SettingsOptionItem<int>(150, "150%"),
            new SettingsOptionItem<int>(175, "175%"),
            new SettingsOptionItem<int>(200, "200%"),
            new SettingsOptionItem<int>(225, "225%"),
            new SettingsOptionItem<int>(250, "250%"),
        };

        private readonly Dictionary<int, int> _indexToSize = new() { { 0, 12 }, { 1, 13 }, { 2, 14 }, { 3, 15 }, { 4, 16 }, { 5, 17 }, { 6, 18 } };
        private readonly Dictionary<int, int> _sizeToIndex = new() { { 12, 0 }, { 13, 1 }, { 14, 2 }, { 15, 3 }, { 16, 4 }, { 17, 5 }, { 18, 6 } };

        public double FontSize
        {
            get
            {
                var size = AppSettings.Appearance.MessageFontSize;
                if (_sizeToIndex.TryGetValue(size, out int index))
                {
                    return index;
                }

                return 2d;
            }
            set
            {
                var index = (int)Math.Round(value);
                if (_indexToSize.TryGetValue(index, out int size))
                {
                    AppSettings.Appearance.MessageFontSize = size;
                }

                RaisePropertyChanged();
            }
        }

        public int BubbleRadius
        {
            get => AppSettings.Appearance.BubbleRadius;
            set
            {
                AppSettings.Appearance.BubbleRadius = value;
                RaisePropertyChanged();
            }
        }

        public bool ForceNightMode
        {
            get => AppSettings.Appearance.ForceNightMode || NightModeService.Current.IsDarkTheme();
            set
            {
                // TODO: this should be probably unified with the code in RootWindow and might need some changes.
                if (AppSettings.Appearance.NightMode != NightMode.Disabled)
                {
                    AppSettings.Appearance.NightMode = NightMode.Disabled;
                    NightModeService.Current.UpdateTimer();

                    ShowToast(Strings.AutoNightModeOff, ToastPopupIcon.AutoNightOff);
                }

                AppSettings.Appearance.ForceNightMode = value;
                AppSettings.Appearance.RequestedTheme = value
                    ? TelegramTheme.Dark
                    : TelegramTheme.Light;

                NightModeService.Current.Update();

                RaisePropertyChanged();
                RaisePropertyChanged(nameof(NightMode));
            }
        }



        public bool SwipeToShare
        {
            get => AppSettings.SwipeToShare;
            set
            {
                AppSettings.SwipeToShare = value;
                RaisePropertyChanged();
            }
        }

        public bool SwipeToReply
        {
            get => AppSettings.SwipeToReply;
            set
            {
                AppSettings.SwipeToReply = value;
                RaisePropertyChanged();
            }
        }

        public bool SwipeToGoBack
        {
            get => AppSettings.SwipeToGoBack;
            set
            {
                AppSettings.SwipeToGoBack = value;
                RaisePropertyChanged();
            }
        }

        public bool DoubleClickToReply
        {
            get => AppSettings.Appearance.IsQuickReplySelected;
            set
            {
                if (AppSettings.Appearance.IsQuickReplySelected != value)
                {
                    AppSettings.Appearance.IsQuickReplySelected = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool DoubleClickToReact
        {
            get => !AppSettings.Appearance.IsQuickReplySelected;
            set
            {
                if (AppSettings.Appearance.IsQuickReplySelected == value)
                {
                    AppSettings.Appearance.IsQuickReplySelected = !value;
                    RaisePropertyChanged();
                }
            }
        }



        public bool FullScreenGallery
        {
            get => AppSettings.FullScreenGallery;
            set
            {
                AppSettings.FullScreenGallery = value;
                RaisePropertyChanged();
            }
        }

        public bool UseSystemSpellChecker
        {
            get => AppSettings.UseSystemSpellChecker;
            set
            {
                AppSettings.UseSystemSpellChecker = value;
                RaisePropertyChanged();
            }
        }

        public bool IsReplaceEmojiEnabled
        {
            get => AppSettings.IsReplaceEmojiEnabled;
            set
            {
                AppSettings.IsReplaceEmojiEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsAdaptiveWideEnabled
        {
            get => AppSettings.IsAdaptiveWideEnabled;
            set
            {
                AppSettings.IsAdaptiveWideEnabled = value;
                RaisePropertyChanged();
            }
        }

        public int FontFamily
        {
            get => FontFamilyOptions.FindIndex(x => x.Value == AppSettings.Appearance.FontFamily);
            set
            {
                if (value >= 0 && value < FontFamilyOptions.Count && AppSettings.Appearance.FontFamily != FontFamilyOptions[value].Value)
                {
                    AppSettings.Appearance.FontFamily = FontFamilyOptions[value].Value;
                    NightModeService.Current.Update(true, updateEmojiSet: true);

                    RaisePropertyChanged();
                }
            }
        }

        public List<SettingsOptionFontFamily> FontFamilyOptions { get; }

        public int SendBy
        {
            get => Array.IndexOf(_sendByIndexer, AppSettings.IsSendByEnterEnabled);
            set
            {
                if (value >= 0 && value < _sendByIndexer.Length && AppSettings.IsSendByEnterEnabled != _sendByIndexer[value])
                {
                    AppSettings.IsSendByEnterEnabled = _sendByIndexer[value];
                    RaisePropertyChanged();
                }
            }
        }

        private readonly bool[] _sendByIndexer = new[]
        {
            true,
            false
        };

        public List<SettingsOptionItem<bool>> SendByOptions { get; } = new()
        {
            new SettingsOptionItem<bool>(true, Strings.SendByEnterKey),
            new SettingsOptionItem<bool>(false, Strings.SendByEnterCtrl),
        };

        public int DistanceUnit
        {
            get => Array.IndexOf(_distanceUnitIndexer, AppSettings.DistanceUnits);
            set
            {
                if (value >= 0 && value < _distanceUnitIndexer.Length && AppSettings.DistanceUnits != _distanceUnitIndexer[value])
                {
                    AppSettings.DistanceUnits = _distanceUnitIndexer[value];
                    RaisePropertyChanged();
                }
            }
        }

        private readonly DistanceUnits[] _distanceUnitIndexer = new[]
        {
            DistanceUnits.Automatic,
            DistanceUnits.Kilometers,
            DistanceUnits.Miles
        };

        public List<SettingsOptionItem<DistanceUnits>> DistanceUnitOptions { get; } = new()
        {
            new SettingsOptionItem<DistanceUnits>(DistanceUnits.Automatic, Strings.DistanceUnitsAutomatic),
            new SettingsOptionItem<DistanceUnits>(DistanceUnits.Kilometers, Strings.DistanceUnitsKilometers),
            new SettingsOptionItem<DistanceUnits>(DistanceUnits.Miles, Strings.DistanceUnitsMiles),
        };

        public async void CreateTheme(ChatThemeViewModel theme)
        {
            var dark = NightModeService.Current.IsDarkTheme();
            var settings = dark ? theme.DarkSettings : theme.LightSettings;

            if (settings == null)
            {
                return;
            }

            // The light house is the bundled theme: it has nothing to colorize from, so it stays a
            // v1 file of the plain lookup.
            if (!dark && theme.AreTheSame(ThemeData.DefaultThemeId))
            {
                await _themeService.CreateThemeAsync(NavigationService, new ThemeBundledInfo { Name = Strings.ThemeClassic, Parent = TelegramTheme.Light });
                return;
            }

            var requested = dark ? TelegramTheme.Dark : TelegramTheme.Light;
            await _themeService.CreateThemeAsync(NavigationService, ThemeAccentInfo.FromSettings(requested, settings), settings);
        }

        public void OpenWallpaper()
        {
            NavigationService.Navigate(typeof(SettingsBackgroundsPage));
        }

        public void ChangeProfileColor()
        {
            NavigationService.Navigate(typeof(SettingsProfileColorPage));
        }

        public void OpenNightMode()
        {
            NavigationService.Navigate(typeof(SettingsNightModePage));
        }

        public void OpenThemes()
        {
            NavigationService.Navigate(typeof(SettingsThemesPage));
        }

        public void OpenStickers()
        {
            NavigationService.Navigate(typeof(SettingsStickersPage));
        }
    }

    public class SettingsOptionFontFamily : SettingsOptionItem<string>
    {
        public SettingsOptionFontFamily(string value, string text, string fontFamily)
            : base(value, text)
        {
            FontFamily = fontFamily;
        }

        public string FontFamily { get; init; }
    }

    public partial class ChatThemeViewModel
    {
        public IClientService ClientService { get; }

        public ThemeSettings DarkSettings { get; set; }

        public ThemeSettings LightSettings { get; set; }

        public ChatTheme Type { get; }

        public bool IsChannel { get; }

        public ChatThemeViewModel(IClientService clientService, EmojiChatTheme chatTheme, bool isChannel)
        {
            ClientService = clientService;
            DarkSettings = Copy(chatTheme.DarkSettings);
            LightSettings = Copy(chatTheme.LightSettings);
            Type = chatTheme.Name != "\u274C" ? new ChatThemeEmoji(chatTheme.Name) : null;
            IsChannel = isChannel;
        }

        public ChatThemeViewModel(IClientService clientService, GiftChatTheme chatTheme)
        {
            ClientService = clientService;
            DarkSettings = Copy(chatTheme.DarkSettings);
            LightSettings = Copy(chatTheme.LightSettings);
            Type = new ChatThemeGift(chatTheme);
        }

        private ThemeSettings Copy(ThemeSettings x)
        {
            if (x == null)
            {
                return null;
            }

            return new ThemeSettings(x.BaseTheme, x.AccentColor, x.Background, x.OutgoingMessageFill, x.AnimateOutgoingMessageFill, x.HasOutgoingMessageAccentColor, x.OutgoingMessageAccentColor);
        }

        public ChatThemeViewModel(IClientService clientService, string name, ThemeSettings lightSettings, ThemeSettings darkSettings, bool isChannel)
        {
            ClientService = clientService;
            DarkSettings = darkSettings;
            LightSettings = lightSettings;
            Type = name != "\u274C" ? new ChatThemeEmoji(name) : null;
            IsChannel = isChannel;
        }

        public EmojiChatTheme ToEmoji()
        {
            if (Type is ChatThemeEmoji emoji)
            {
                return new EmojiChatTheme(emoji.Name, LightSettings, DarkSettings);
            }

            return null;
        }
    }
}
