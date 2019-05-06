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
    public class SettingsAppearanceViewModel : TLViewModelBase
    {
        private readonly IThemeService _themeService;

        private readonly Dictionary<int, int> _indexToSize = new Dictionary<int, int> { { 0, 12 }, { 1, 13 }, { 2, 14 }, { 3, 15 }, { 4, 16 }, { 5, 17 }, { 6, 18 } };
        private readonly Dictionary<int, int> _sizeToIndex = new Dictionary<int, int> { { 12, 0 }, { 13, 1 }, { 14, 2 }, { 15, 3 }, { 16, 4 }, { 17, 5 }, { 18, 6 } };

        public SettingsAppearanceViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _themeService = themeService;

            Items = new MvxObservableCollection<ThemeInfoBase>();

            UseDefaultLayout = !Settings.UseThreeLinesLayout;
            UseThreeLinesLayout = Settings.UseThreeLinesLayout;

            EmojiSetCommand = new RelayCommand(EmojiSetExecute);

            NewThemeCommand = new RelayCommand(NewThemeExecute);

            ThemeCreateCommand = new RelayCommand<ThemeInfoBase>(ThemeCreateExecute);
            ThemeShareCommand = new RelayCommand<ThemeCustomInfo>(ThemeShareExecute);
            ThemeEditCommand = new RelayCommand<ThemeCustomInfo>(ThemeEditExecute);
            ThemeDeleteCommand = new RelayCommand<ThemeCustomInfo>(ThemeDeleteExecute);
        }

        public MvxObservableCollection<ThemeInfoBase> Items { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var emojiSet = Settings.Appearance.EmojiSet;
            var emojiSetId = Settings.Appearance.EmojiSetId;

            if (emojiSet.Length > 0 && emojiSetId.Length > 0)
            {
                EmojiSet = emojiSet;
                EmojiSetId = $"ms-appx:///Assets/Emoji/{emojiSetId}.png";
            }
            else
            {
                EmojiSet = "Microsoft";
                EmojiSetId = $"ms-appx:///Assets/Emoji/microsoft.png";
            }

            Items.ReplaceWith(await _themeService.GetThemesAsync());
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

        public async Task SetThemeAsync(ThemeInfoBase info)
        {
            await _themeService.SetThemeAsync(info);
            RaisePropertyChanged(() => IsNightModeAvailable);

            Items.ReplaceWith(await _themeService.GetThemesAsync());
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

        public bool IsNightModeAvailable
        {
            get
            {
                return Settings.Appearance.RequestedTheme.HasFlag(ElementTheme.Light);
            }
        }

        public NightMode NightMode
        {
            get
            {
                return Settings.Appearance.NightMode;
            }
        }



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



        public RelayCommand EmojiSetCommand { get; }
        private async void EmojiSetExecute()
        {
            await new SettingsEmojiSetView().ShowQueuedAsync();

            var emojiSet = Settings.Appearance.EmojiSet;
            var emojiSetId = Settings.Appearance.EmojiSetId;

            if (emojiSet.Length > 0 && emojiSetId.Length > 0)
            {
                EmojiSet = emojiSet;
                EmojiSetId = $"ms-appx:///Assets/Emoji/{emojiSetId}.png";
            }
            else
            {
                EmojiSet = "Microsoft";
                EmojiSetId = $"ms-appx:///Assets/Emoji/microsoft.png";
            }
        }

        public RelayCommand NewThemeCommand { get; }
        private void NewThemeExecute()
        {
            var existing = Items.FirstOrDefault(x =>
            {
                if (x is ThemeCustomInfo custom)
                {
                    return string.Equals(SettingsService.Current.Appearance.RequestedThemePath, custom.Path, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    return SettingsService.Current.Appearance.RequestedTheme == (x.Parent.HasFlag(TelegramTheme.Light) ? ElementTheme.Light : ElementTheme.Dark);
                }
            });

            if (existing != null)
            {
                ThemeCreateExecute(existing);
            }
        }

        #region Themes

        public RelayCommand<ThemeInfoBase> ThemeCreateCommand { get; }
        private async void ThemeCreateExecute(ThemeInfoBase theme)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.CreateNewThemeAlert, Strings.Resources.NewTheme, Strings.Resources.CreateTheme, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var input = new InputDialog();
            input.Title = Strings.Resources.NewTheme;
            input.Header = Strings.Resources.EnterThemeName;
            input.Text = $"{theme.Name} #2";
            input.IsPrimaryButtonEnabled = true;
            input.IsSecondaryButtonEnabled = true;
            input.PrimaryButtonText = Strings.Resources.OK;
            input.SecondaryButtonText = Strings.Resources.Cancel;

            confirm = await input.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var preparing = new ThemeCustomInfo { Name = input.Text, Parent = theme.Parent };
            var fileName = Client.Execute(new CleanFileName(theme.Name)) as Text;

            if (theme is ThemeCustomInfo custom)
            {
                foreach (var item in custom.Values)
                {
                    preparing.Values[item.Key] = item.Value;
                }
            }

            var file = await ApplicationData.Current.LocalFolder.CreateFileAsync("themes\\" + fileName.TextValue + ".unigram-theme", CreationCollisionOption.GenerateUniqueName);
            await _themeService.SerializeAsync(file, preparing);

            preparing.Path = file.Path;

            ThemeEditExecute(preparing);
        }

        public RelayCommand<ThemeCustomInfo> ThemeShareCommand { get; }
        private async void ThemeShareExecute(ThemeCustomInfo theme)
        {
            await ShareView.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(theme.Path), null, null));
        }

        public RelayCommand<ThemeCustomInfo> ThemeEditCommand { get; }
        private async void ThemeEditExecute(ThemeCustomInfo theme)
        {
            await SetThemeAsync(theme);

            //NavigationService.Navigate(typeof(SettingsThemePage), theme.Path);
            if (Window.Current.Content is Views.Host.RootPage root)
            {
                root.ShowEditor(theme);
            }
        }

        public RelayCommand<ThemeCustomInfo> ThemeDeleteCommand { get; }
        private async void ThemeDeleteExecute(ThemeCustomInfo theme)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.DeleteThemeAlert, Strings.Resources.AppName, Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(theme.Path);
                await file.DeleteAsync();
            }
            catch { }

            if (Settings.Appearance.RequestedThemePath == theme.Path)
            {
                await SetThemeAsync(new ThemeBundledInfo { Parent = theme.Parent });
            }
            else
            {
                Items.ReplaceWith(await _themeService.GetThemesAsync());
            }
        }

        #endregion

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
