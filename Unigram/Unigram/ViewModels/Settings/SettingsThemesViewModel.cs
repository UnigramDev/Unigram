using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.Views.Popups;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsThemesViewModel : TLViewModelBase
    {
        private readonly IThemeService _themeService;

        public SettingsThemesViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _themeService = themeService;

            Items = new MvxObservableCollection<ThemeInfoBase>();
            Custom = new MvxObservableCollection<ThemeInfoBase>();
            Accents = new MvxObservableCollection<ThemeAccentInfo>();

            NewThemeCommand = new RelayCommand(NewThemeExecute);
            AccentThemeCommand = new RelayCommand(AccentThemeExecute);

            ThemeCreateCommand = new RelayCommand<ThemeInfoBase>(ThemeCreateExecute);
            ThemeShareCommand = new RelayCommand<ThemeCustomInfo>(ThemeShareExecute);
            ThemeEditCommand = new RelayCommand<ThemeCustomInfo>(ThemeEditExecute);
            ThemeDeleteCommand = new RelayCommand<ThemeCustomInfo>(ThemeDeleteExecute);
        }

        public MvxObservableCollection<ThemeInfoBase> Items { get; private set; }
        public MvxObservableCollection<ThemeInfoBase> Custom { get; private set; }
        public MvxObservableCollection<ThemeAccentInfo> Accents { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await RefreshThemesAsync();
        }

        public async Task SetThemeAsync(ThemeInfoBase info)
        {
            await _themeService.SetThemeAsync(info);
            RaisePropertyChanged(() => IsNightModeAvailable);

            await RefreshThemesAsync();
        }

        private async Task RefreshThemesAsync()
        {
            Items.ReplaceWith(_themeService.GetThemes());
            Custom.ReplaceWith(await _themeService.GetCustomThemesAsync());

            var type = Settings.Appearance.RequestedThemeType;
            if (ThemeAccentInfo.IsAccent(type))
            {
                var accent = Settings.Appearance.Accents[type];
                if (_defaultAccents[type].Contains(accent))
                {
                    Accents.ReplaceWith(_defaultAccents[type]
                        .Select(x => ThemeAccentInfo.FromAccent(type, x)));
                }
                else
                {
                    Accents.ReplaceWith(_defaultAccents[type]
                        .Take(_defaultAccents[type].Length - 1)
                        .Union(new[] { accent })
                        .Select(x => ThemeAccentInfo.FromAccent(type, x)));
                }
            }
            else
            {
                Accents.Clear();
            }

            AreCustomThemesAvailable = Custom.Count > 0;
        }

        private readonly Dictionary<TelegramThemeType, Color[]> _defaultAccents = new Dictionary<TelegramThemeType, Color[]>
        {
            {
                TelegramThemeType.Tinted, new Color[]
                {
                    ColorEx.FromHex(0xFF5288C1),
                    ColorEx.FromHex(0xFF58bfe8),
                    ColorEx.FromHex(0xFF466f42),
                    ColorEx.FromHex(0xFFaa6084),
                    ColorEx.FromHex(0xFFa46d3c),
                    ColorEx.FromHex(0xFF917bbd),
                    ColorEx.FromHex(0xFFab5149),
                    ColorEx.FromHex(0xFF697b97),
                    ColorEx.FromHex(0xFF9b834b),
                }
            },
            {
                TelegramThemeType.Night, new Color[]
                {
                    ColorEx.FromHex(0xFF5288C1),
                    ColorEx.FromHex(0xFF58bfe8),
                    ColorEx.FromHex(0xFF466f42),
                    ColorEx.FromHex(0xFFaa6084),
                    ColorEx.FromHex(0xFFa46d3c),
                    ColorEx.FromHex(0xFF917bbd),
                    ColorEx.FromHex(0xFFab5149),
                    ColorEx.FromHex(0xFF697b97),
                    ColorEx.FromHex(0xFF9b834b),
                }
            },
            {
                TelegramThemeType.Day, new Color[]
                {
                    ColorEx.FromHex(0xFF40A7E3),
                    ColorEx.FromHex(0xFF45bce7),
                    ColorEx.FromHex(0xFF52b440),
                    ColorEx.FromHex(0xFFd46c99),
                    ColorEx.FromHex(0xFFdf8a49),
                    ColorEx.FromHex(0xFF9978c8),
                    ColorEx.FromHex(0xFFc55245),
                    ColorEx.FromHex(0xFF687b98),
                    ColorEx.FromHex(0xFFdea922),
                }
            }
        };

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

        private bool _areCustomThemesAvailable;
        public bool AreCustomThemesAvailable
        {
            get { return _areCustomThemesAvailable; }
            set { Set(ref _areCustomThemesAvailable, value); }
        }



        public RelayCommand NewThemeCommand { get; }
        private void NewThemeExecute()
        {
            var existing = Items.FirstOrDefault(x =>
            {
                if (x is ThemeCustomInfo custom)
                {
                    return string.Equals(SettingsService.Current.Appearance.RequestedThemeCustom, custom.Path, StringComparison.OrdinalIgnoreCase);
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

        public RelayCommand AccentThemeCommand { get; }
        private async void AccentThemeExecute()
        {
            var type = Settings.Appearance.RequestedThemeType;
            if (ThemeAccentInfo.IsAccent(type))
            {
                var accent = Settings.Appearance.Accents[type];
                if (accent == default)
                {
                    accent = BootStrapper.Current.UISettings.GetColorValue(UIColorType.Accent);
                }

                var dialog = new SelectColorPopup();
                dialog.Color = accent;

                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    await SetThemeAsync(ThemeAccentInfo.FromAccent(type, dialog.Color));
                }
            }
        }

        #region Themes

        public RelayCommand<ThemeInfoBase> ThemeCreateCommand { get; }
        private async void ThemeCreateExecute(ThemeInfoBase theme)
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.CreateNewThemeAlert, Strings.Resources.NewTheme, Strings.Resources.CreateTheme, Strings.Resources.Cancel);
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
            else if (theme is ThemeAccentInfo accent)
            {
                foreach (var item in accent.Values)
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
            await SharePopup.GetForCurrentView().ShowAsync(new InputMessageDocument(new InputFileLocal(theme.Path), null, false, null));
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
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.DeleteThemeAlert, Strings.Resources.AppName, Strings.Resources.Delete, Strings.Resources.Cancel);
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

            if (Settings.Appearance.RequestedThemeCustom == theme.Path)
            {
                await SetThemeAsync(new ThemeBundledInfo { Parent = theme.Parent });
            }
            else
            {
                await RefreshThemesAsync();
            }
        }

        #endregion
    }
}
