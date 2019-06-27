using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.Storage;
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

            NewThemeCommand = new RelayCommand(NewThemeExecute);

            ThemeCreateCommand = new RelayCommand<ThemeInfoBase>(ThemeCreateExecute);
            ThemeShareCommand = new RelayCommand<ThemeCustomInfo>(ThemeShareExecute);
            ThemeEditCommand = new RelayCommand<ThemeCustomInfo>(ThemeEditExecute);
            ThemeDeleteCommand = new RelayCommand<ThemeCustomInfo>(ThemeDeleteExecute);
        }

        public MvxObservableCollection<ThemeInfoBase> Items { get; private set; }
        public MvxObservableCollection<ThemeInfoBase> Custom { get; private set; }

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
            Items.ReplaceWith(await _themeService.GetThemesAsync(false));
            Custom.ReplaceWith(await _themeService.GetThemesAsync(true));

            AreCustomThemesAvailable = Custom.Count > 0;
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
                await RefreshThemesAsync();
            }
        }

        #endregion
    }
}
