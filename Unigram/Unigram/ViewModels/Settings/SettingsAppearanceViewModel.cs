﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsAppearanceViewModel : SettingsThemesViewModel
    {
        private readonly IEmojiSetService _emojiSetService;

        private readonly Dictionary<int, int> _indexToSize = new Dictionary<int, int> { { 0, 12 }, { 1, 13 }, { 2, 14 }, { 3, 15 }, { 4, 16 }, { 5, 17 }, { 6, 18 } };
        private readonly Dictionary<int, int> _sizeToIndex = new Dictionary<int, int> { { 12, 0 }, { 13, 1 }, { 14, 2 }, { 15, 3 }, { 16, 4 }, { 17, 5 }, { 18, 6 } };

        public SettingsAppearanceViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService, IEmojiSetService emojiSetService)
            : base(protoService, cacheService, settingsService, aggregator, themeService)
        {
            _emojiSetService = emojiSetService;

            DistanceUnitsCommand = new RelayCommand(DistanceUnitsExecute);
            EmojiSetCommand = new RelayCommand(EmojiSetExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
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

            await base.OnNavigatedToAsync(parameter, mode, state);
        }

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
            get
            {
                return Settings.Appearance.BubbleRadius;
            }
            set
            {
                Settings.Appearance.BubbleRadius = value;
                RaisePropertyChanged();
            }
        }

        //public bool IsSystemTheme
        //{
        //    get
        //    {
        //        return !Settings.Appearance.RequestedTheme.HasFlag(TelegramTheme.Brand);
        //    }
        //    set
        //    {
        //        Settings.Appearance.RequestedTheme = value ? GetRawTheme() : (GetRawTheme() | TelegramTheme.Brand);
        //        RaisePropertyChanged();
        //        RaisePropertyChanged(() => RequestedTheme);
        //    }
        //}



        public bool FullScreenGallery
        {
            get
            {
                return Settings.FullScreenGallery;
            }
            set
            {
                Settings.FullScreenGallery = value;
                RaisePropertyChanged();
            }
        }

        public bool DisableHighlightWords
        {
            get
            {
                return Settings.DisableHighlightWords;
            }
            set
            {
                Settings.DisableHighlightWords = value;
                RaisePropertyChanged();
            }
        }

        public bool IsSendByEnterEnabled
        {
            get
            {
                return Settings.IsSendByEnterEnabled;
            }
            set
            {
                Settings.IsSendByEnterEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsLargeEmojiEnabled
        {
            get
            {
                return Settings.IsLargeEmojiEnabled;
            }
            set
            {
                Settings.IsLargeEmojiEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsReplaceEmojiEnabled
        {
            get
            {
                return Settings.IsReplaceEmojiEnabled;
            }
            set
            {
                Settings.IsReplaceEmojiEnabled = value;
                RaisePropertyChanged();
            }
        }

        public DistanceUnits DistanceUnits
        {
            get
            {
                return Settings.DistanceUnits;
            }
            set
            {
                Settings.DistanceUnits = value;
                RaisePropertyChanged();
            }
        }

        public RelayCommand DistanceUnitsCommand { get; }
        private async void DistanceUnitsExecute()
        {
            var items = new[]
            {
                new SelectRadioItem(DistanceUnits.Automatic, Strings.Resources.DistanceUnitsAutomatic, DistanceUnits == DistanceUnits.Automatic),
                new SelectRadioItem(DistanceUnits.Kilometers, Strings.Resources.DistanceUnitsKilometers, DistanceUnits == DistanceUnits.Kilometers),
                new SelectRadioItem(DistanceUnits.Miles, Strings.Resources.DistanceUnitsMiles, DistanceUnits == DistanceUnits.Miles),
            };

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.DistanceUnitsTitle;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is DistanceUnits index)
            {
                DistanceUnits = index;
            }
        }

        public RelayCommand EmojiSetCommand { get; }
        private async void EmojiSetExecute()
        {
            await new SettingsEmojiSetPopup(ProtoService, _emojiSetService, Aggregator).ShowQueuedAsync();

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
    }
}
