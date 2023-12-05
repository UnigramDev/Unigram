//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsAppearanceViewModel : ViewModelBase
    {
        private readonly IThemeService _themeService;

        private readonly Dictionary<int, int> _indexToSize = new Dictionary<int, int> { { 0, 12 }, { 1, 13 }, { 2, 14 }, { 3, 15 }, { 4, 16 }, { 5, 17 }, { 6, 18 } };
        private readonly Dictionary<int, int> _sizeToIndex = new Dictionary<int, int> { { 12, 0 }, { 13, 1 }, { 14, 2 }, { 15, 3 }, { 16, 4 }, { 17, 5 }, { 18, 6 } };

        public SettingsAppearanceViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService)
            : base(clientService, settingsService, aggregator)
        {
            _themeService = themeService;

            ChatThemes = new ObservableCollection<ChatThemeViewModel>();
        }

        public ObservableCollection<ChatThemeViewModel> ChatThemes { get; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            static Background GetDefaultBackground(bool dark)
            {
                var freeform = dark ? new[] { 0x6C7FA6, 0x2E344B, 0x7874A7, 0x333258 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };
                return new Background(0, true, dark, string.Empty,
                    new Document(string.Empty, "application/x-tgwallpattern", null, null, TdExtensions.GetLocalFile("Assets\\Background.tgv", "Background")),
                    new BackgroundTypePattern(new BackgroundFillFreeformGradient(freeform), dark ? 100 : 50, dark, false));
            }

            var defaultLight = new ThemeSettings
            {
                AccentColor = 0x158DCD,
                OutgoingMessageAccentColor = 0xF0FDDF,
                OutgoingMessageFill = new BackgroundFillSolid(0xF0FDDF),
                Background = GetDefaultBackground(false)
            };

            var defaultDark = new ThemeSettings
            {
                AccentColor = 0x71BAFA,
                OutgoingMessageAccentColor = 0x2B5278,
                OutgoingMessageFill = new BackgroundFillSolid(0x2B5278),
                Background = GetDefaultBackground(true)
            };

            var defaultTheme = new ChatThemeViewModel(ClientService, "\U0001F3E0", defaultLight, defaultDark);
            var themes = ClientService.GetChatThemes().Select(x => new ChatThemeViewModel(ClientService, x));

            ChatThemes.AddRange(new[] { defaultTheme }.Union(themes));

            _selectedChatTheme = ChatThemes.FirstOrDefault(x => x.Name == Settings.Appearance.ChatTheme?.Name) ?? defaultTheme;
            RaisePropertyChanged(nameof(SelectedChatTheme));

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        private ChatThemeViewModel _selectedChatTheme;
        public ChatThemeViewModel SelectedChatTheme
        {
            get => _selectedChatTheme;
            set => SetChatTheme(value);
        }

        private void SetChatTheme(ChatThemeViewModel chatTheme)
        {
            if (chatTheme == null || chatTheme.Name == _selectedChatTheme?.Name)
            {
                return;
            }

            void SetBackground(Background background, bool forDarkTheme)
            {
                if (background != null && chatTheme.Name != "\U0001F3E0")
                {
                    ClientService.Send(new SetBackground(new InputBackgroundRemote(background.Id), background.Type, forDarkTheme));
                }
                else
                {
                    ClientService.Send(new SetBackground(null, null, forDarkTheme));
                }
            }

            SetBackground(chatTheme.LightSettings.Background, false);
            SetBackground(chatTheme.DarkSettings.Background, true);

            Settings.Appearance.ChatTheme = chatTheme;
            Settings.Appearance.UpdateNightMode();

            _selectedChatTheme = chatTheme;
            RaisePropertyChanged(nameof(SelectedChatTheme));
        }

        public NightMode NightMode => Settings.Appearance.NightMode;

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

        public double FontSize
        {
            get
            {
                var size = Theme.Current.MessageFontSize;
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
                    Theme.Current.MessageFontSize = size;
                }

                RaisePropertyChanged();
            }
        }

        public int BubbleRadius
        {
            get => Settings.Appearance.BubbleRadius;
            set
            {
                Settings.Appearance.BubbleRadius = value;
                RaisePropertyChanged();
            }
        }

        public bool ForceNightMode
        {
            get => Settings.Appearance.ForceNightMode || Settings.Appearance.IsDarkTheme();
            set
            {
                // TODO: this should be probably unified with the code in RootPage and might need some changes.
                if (Settings.Appearance.NightMode != NightMode.Disabled)
                {
                    Settings.Appearance.NightMode = NightMode.Disabled;
                    Window.Current.ShowToast(Strings.AutoNightModeOff, new LocalFileSource("ms-appx:///Assets/Toasts/AutoNightOff.tgs"));
                }

                Settings.Appearance.ForceNightMode = value;
                Settings.Appearance.RequestedTheme = value
                    ? TelegramTheme.Dark
                    : TelegramTheme.Light;

                Settings.Appearance.UpdateNightMode();

                RaisePropertyChanged();
                RaisePropertyChanged(nameof(NightMode));
            }
        }



        public bool SwipeToShare
        {
            get => Settings.SwipeToShare;
            set
            {
                Settings.SwipeToShare = value;
                RaisePropertyChanged();
            }
        }

        public bool SwipeToReply
        {
            get => Settings.SwipeToReply;
            set
            {
                Settings.SwipeToReply = value;
                RaisePropertyChanged();
            }
        }



        public bool FullScreenGallery
        {
            get => Settings.FullScreenGallery;
            set
            {
                Settings.FullScreenGallery = value;
                RaisePropertyChanged();
            }
        }

        public bool UseSystemSpellChecker
        {
            get => Settings.UseSystemSpellChecker;
            set
            {
                Settings.UseSystemSpellChecker = value;
                RaisePropertyChanged();
            }
        }

        public bool IsReplaceEmojiEnabled
        {
            get => Settings.IsReplaceEmojiEnabled;
            set
            {
                Settings.IsReplaceEmojiEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsAdaptiveWideEnabled
        {
            get => Settings.IsAdaptiveWideEnabled;
            set
            {
                Settings.IsAdaptiveWideEnabled = value;
                RaisePropertyChanged();
            }
        }

        public int SendBy
        {
            get => Array.IndexOf(_sendByIndexer, Settings.IsSendByEnterEnabled);
            set
            {
                if (value >= 0 && value < _sendByIndexer.Length && Settings.IsSendByEnterEnabled != _sendByIndexer[value])
                {
                    Settings.IsSendByEnterEnabled = _sendByIndexer[value];
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
            get => Array.IndexOf(_distanceUnitIndexer, Settings.DistanceUnits);
            set
            {
                if (value >= 0 && value < _distanceUnitIndexer.Length && Settings.DistanceUnits != _distanceUnitIndexer[value])
                {
                    Settings.DistanceUnits = _distanceUnitIndexer[value];
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
            var dark = Settings.Appearance.IsDarkTheme();
            var settings = dark ? theme.DarkSettings : theme.LightSettings;

            var tint = Settings.Appearance[dark ? TelegramTheme.Dark : TelegramTheme.Light].Type;
            if (tint == TelegramThemeType.Classic || (tint == TelegramThemeType.Custom && !dark))
            {
                tint = TelegramThemeType.Day;
            }
            else if (tint == TelegramThemeType.Custom)
            {
                tint = TelegramThemeType.Tinted;
            }

            var accent = settings.AccentColor.ToColor();
            var outgoing = settings.OutgoingMessageAccentColor.ToColor();

            await _themeService.CreateThemeAsync(ThemeAccentInfo.FromAccent(tint, accent, outgoing));
        }

        public void OpenWallpaper()
        {
            NavigationService.Navigate(typeof(SettingsBackgroundsPage));
        }

        public async void ChangeProfileColor()
        {
            await ShowPopupAsync(new ChooseProfileColorPopup(ClientService, new MessageSenderUser(ClientService.Options.MyId)));
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

    public class ChatThemeViewModel
    {
        public IClientService ClientService { get; }

        public ThemeSettings DarkSettings { get; }

        public ThemeSettings LightSettings { get; }

        public string Name { get; }

        public ChatThemeViewModel(IClientService clientService, ChatTheme chatTheme)
        {
            ClientService = clientService;
            DarkSettings = chatTheme.DarkSettings;
            LightSettings = chatTheme.LightSettings;
            Name = chatTheme.Name;
        }

        public ChatThemeViewModel(IClientService clientService, string name, ThemeSettings lightSettings, ThemeSettings darkSettings)
        {
            ClientService = clientService;
            DarkSettings = darkSettings;
            LightSettings = lightSettings;
            Name = name;
        }

        public static implicit operator ChatTheme(ChatThemeViewModel chatTheme)
        {
            if (chatTheme == null)
            {
                return null;
            }

            return new ChatTheme(chatTheme.Name, chatTheme.LightSettings, chatTheme.DarkSettings);
        }
    }
}
