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
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsAppearanceViewModel : TLViewModelBase
    {
        private readonly IThemeService _themeService;
        private readonly IEmojiSetService _emojiSetService;

        private readonly Dictionary<int, int> _indexToSize = new Dictionary<int, int> { { 0, 12 }, { 1, 13 }, { 2, 14 }, { 3, 15 }, { 4, 16 }, { 5, 17 }, { 6, 18 } };
        private readonly Dictionary<int, int> _sizeToIndex = new Dictionary<int, int> { { 12, 0 }, { 13, 1 }, { 14, 2 }, { 15, 3 }, { 16, 4 }, { 17, 5 }, { 18, 6 } };

        public SettingsAppearanceViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService, IEmojiSetService emojiSetService)
            : base(clientService, settingsService, aggregator)
        {
            _themeService = themeService;
            _emojiSetService = emojiSetService;

            ChatThemes = new ObservableCollection<ChatTheme>();

            ThemeCreateCommand = new RelayCommand<ChatTheme>(ThemeCreateExecute);
        }

        public ObservableCollection<ChatTheme> ChatThemes { get; }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            static Background GetDefaultBackground(bool dark)
            {
                var freeform = dark ? new[] { 0x0D0E17, 0x090A0C, 0x181C28, 0x0E0F12 } : new[] { 0xDBDDBB, 0x6BA587, 0xD5D88D, 0x88B884 };
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

            var defaultTheme = new ChatTheme("\U0001F3E0", defaultLight, defaultDark);
            var themes = ClientService.GetChatThemes();

            ChatThemes.AddRange(new[] { defaultTheme }.Union(themes));

            _selectedChatTheme = ChatThemes.FirstOrDefault(x => x.Name == Settings.Appearance.ChatTheme?
            .Name) ?? defaultTheme;
            RaisePropertyChanged(nameof(SelectedChatTheme));

            var emojiSet = Settings.Appearance.EmojiSet;
            EmojiSet = emojiSet.Title;

            switch (emojiSet.Id)
            {
                case "microsoft":
                case "apple":
                    EmojiSetId = $"ms-appx:///Assets/Emoji/{emojiSet.Id}.png";
                    break;
                default:
                    EmojiSetId = $"ms-appdata:///local/emoji/{emojiSet.Id}.png";
                    break;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        private ChatTheme _selectedChatTheme;
        public ChatTheme SelectedChatTheme
        {
            get => _selectedChatTheme;
            set => SetChatTheme(value);
        }

        private void SetChatTheme(ChatTheme chatTheme)
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
                Settings.Appearance.ForceNightMode = value;
                Settings.Appearance.RequestedTheme = value
                    ? TelegramTheme.Dark
                    : TelegramTheme.Light;

                Settings.Appearance.UpdateNightMode();
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

        public bool DisableHighlightWords
        {
            get => Settings.DisableHighlightWords;
            set
            {
                Settings.DisableHighlightWords = value;
                RaisePropertyChanged();
            }
        }

        public bool IsSendByEnterEnabled
        {
            get => Settings.IsSendByEnterEnabled;
            set
            {
                Settings.IsSendByEnterEnabled = value;
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

        public DistanceUnits DistanceUnits
        {
            get => Settings.DistanceUnits;
            set
            {
                Settings.DistanceUnits = value;
                RaisePropertyChanged();
            }
        }

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
            new SettingsOptionItem<DistanceUnits>(DistanceUnits.Automatic, Strings.Resources.DistanceUnitsAutomatic),
            new SettingsOptionItem<DistanceUnits>(DistanceUnits.Kilometers, Strings.Resources.DistanceUnitsKilometers),
            new SettingsOptionItem<DistanceUnits>(DistanceUnits.Miles, Strings.Resources.DistanceUnitsMiles),
        };

        public async void ChangeEmojiSet()
        {
            await ShowPopupAsync(new SettingsEmojiSetPopup(ClientService, _emojiSetService, Aggregator));

            var emojiSet = Settings.Appearance.EmojiSet;
            EmojiSet = emojiSet.Title;

            switch (emojiSet.Id)
            {
                case "microsoft":
                case "apple":
                    EmojiSetId = $"ms-appx:///Assets/Emoji/{emojiSet.Id}.png";
                    break;
                default:
                    EmojiSetId = $"ms-appdata:///local/emoji/{emojiSet.Id}.png";
                    break;
            }
        }

        public RelayCommand<ChatTheme> ThemeCreateCommand { get; }
        private async void ThemeCreateExecute(ChatTheme theme)
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
}
