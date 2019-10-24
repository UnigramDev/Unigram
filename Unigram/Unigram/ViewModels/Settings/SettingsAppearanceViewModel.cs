using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.UI.Xaml;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.Foundation.Metadata;
using Unigram.Controls;
using Windows.UI.Xaml.Controls;
using Unigram.Views.Settings;
using Unigram.Collections;
using Windows.UI.Xaml.Navigation;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.Storage;
using Template10.Services.NavigationService;
using Unigram.Services.Updates;
using Unigram.Controls.Views;
using Template10.Common;

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

            UseDefaultLayout = !Settings.UseThreeLinesLayout;
            UseThreeLinesLayout = Settings.UseThreeLinesLayout;

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

        public override Task OnNavigatingFromAsync(NavigatingEventArgs args)
        {
            if (UseThreeLinesLayout != Settings.UseThreeLinesLayout)
            {
                Settings.UseThreeLinesLayout = UseThreeLinesLayout;
                Aggregator.Publish(new UpdateChatListLayout(UseThreeLinesLayout));
            }

            return base.OnNavigatingFromAsync(args);
        }

        private string _emojiSet;
        public string EmojiSet
        {
            get { return _emojiSet; }
            set { Set(ref _emojiSet, value); }
        }

        private string _emojiSetId;
        public string EmojiSetId
        {
            get { return _emojiSetId; }
            set { Set(ref _emojiSetId, value); }
        }

        public double FontSize
        {
            get
            {
                var size = (int)Theme.Current.GetValueOrDefault("MessageFontSize", ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7) ? 14d : 15d);
                if (_sizeToIndex.TryGetValue(size, out int index))
                {
                    return (double)index;
                }

                return 2d;
            }
            set
            {
                var index = (int)Math.Round(value);
                if (_indexToSize.TryGetValue(index, out int size))
                {
                    Theme.Current.AddOrUpdateValue("MessageFontSize", (double)size);
                }

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



        public bool AreAnimationsEnabled
        {
            get
            {
                return Settings.AreAnimationsEnabled;
            }
            set
            {
                Settings.AreAnimationsEnabled = value;
                RaisePropertyChanged(() => AreAnimationsEnabled);
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
            var dialog = new TLContentDialog();
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = DistanceUnits.Automatic, Content = Strings.Resources.DistanceUnitsAutomatic, IsChecked = DistanceUnits == DistanceUnits.Automatic });
            stack.Children.Add(new RadioButton { Tag = DistanceUnits.Kilometers, Content = Strings.Resources.DistanceUnitsKilometers, IsChecked = DistanceUnits == DistanceUnits.Kilometers });
            stack.Children.Add(new RadioButton { Tag = DistanceUnits.Miles, Content = Strings.Resources.DistanceUnitsMiles, IsChecked = DistanceUnits == DistanceUnits.Miles });

            dialog.Title = Strings.Resources.DistanceUnitsTitle;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var mode = DistanceUnits.Automatic;
                foreach (RadioButton current in stack.Children)
                {
                    if (current.IsChecked == true)
                    {
                        mode = (DistanceUnits)current.Tag;
                        break;
                    }
                }

                DistanceUnits = mode;
            }
        }

        public RelayCommand EmojiSetCommand { get; }
        private async void EmojiSetExecute()
        {
            await new SettingsEmojiSetView(ProtoService, _emojiSetService, Aggregator).ShowQueuedAsync();

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

        #region Layouts

        private bool _useDefaultLayout;
        public bool UseDefaultLayout
        {
            get { return _useDefaultLayout; }
            set { Set(ref _useDefaultLayout, value); }
        }

        private bool _useThreeLinesLayout;
        public bool UseThreeLinesLayout
        {
            get { return _useThreeLinesLayout; }
            set { Set(ref _useThreeLinesLayout, value); }
        }

        #endregion
    }
}
