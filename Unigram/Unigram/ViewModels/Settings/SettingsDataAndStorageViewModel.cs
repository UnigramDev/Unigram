using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Native.Calls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.Views.Popups;
using Unigram.Views.Settings;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsDataAndStorageViewModel : TLViewModelBase
    {
        public SettingsDataAndStorageViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            AutoDownloadCommand = new RelayCommand<AutoDownloadType>(AutoDownloadExecute);
            ResetAutoDownloadCommand = new RelayCommand(ResetAutoDownloadExecute);
            StoragePathCommand = new RelayCommand<bool>(StoragePathExecute);
            UseLessDataCommand = new RelayCommand(UseLessDataExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            return Task.CompletedTask;
        }

        public VoipDataSaving UseLessData
        {
            get
            {
                return Settings.UseLessData;
            }
            set
            {
                Settings.UseLessData = value;
                RaisePropertyChanged();
            }
        }

        public Services.Settings.AutoDownloadSettings AutoDownload => Settings.AutoDownload;

        public bool AutoDownloadEnabled
        {
            get
            {
                return !Settings.AutoDownload.Disabled;
            }
            set
            {
                Settings.AutoDownload = Settings.AutoDownload.UpdateDisabled(!value);
                RaisePropertyChanged();
            }
        }


        public bool IsAutoPlayAnimationsEnabled
        {
            get
            {
                return Settings.IsAutoPlayAnimationsEnabled;
            }
            set
            {
                Settings.IsAutoPlayAnimationsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsAutoPlayVideosEnabled
        {
            get
            {
                return Settings.IsAutoPlayVideosEnabled;
            }
            set
            {
                Settings.IsAutoPlayVideosEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsStreamingEnabled
        {
            get
            {
                return SettingsService.Current.IsStreamingEnabled;
            }
            set
            {
                SettingsService.Current.IsStreamingEnabled = value;
                RaisePropertyChanged();
            }
        }

        public string FilesDirectory
        {
            get
            {
                return Settings.FilesDirectory;
            }
            set
            {
                Settings.FilesDirectory = value;
                RaisePropertyChanged();
            }
        }

        public RelayCommand UseLessDataCommand { get; }
        private async void UseLessDataExecute()
        {
            var items = new[]
            {
                new SelectRadioItem(VoipDataSaving.Never, Strings.Resources.UseLessDataNever, UseLessData == VoipDataSaving.Never),
                new SelectRadioItem(VoipDataSaving.Mobile, Strings.Resources.UseLessDataOnMobile, UseLessData == VoipDataSaving.Mobile),
                new SelectRadioItem(VoipDataSaving.Always, Strings.Resources.UseLessDataAlways, UseLessData == VoipDataSaving.Always),
            };

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.VoipUseLessData;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is VoipDataSaving index)
            {
                UseLessData = index;
            }
        }

        public RelayCommand<bool> StoragePathCommand { get; }
        private async void StoragePathExecute(bool reset)
        {
            var path = FilesDirectory;
            if (reset)
            {
                FilesDirectory = null;
            }
            else
            {
                try
                {
                    var picker = new FolderPicker();
                    picker.SuggestedStartLocation = PickerLocationId.Downloads;
                    picker.FileTypeFilter.Add("*");

                    var folder = await picker.PickSingleFolderAsync();
                    if (folder != null)
                    {
                        StorageApplicationPermissions.MostRecentlyUsedList.AddOrReplace("FilesDirectory", folder);
                        FilesDirectory = folder.Path;
                    }
                }
                catch { }
            }

            if (string.Equals(path, FilesDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // TODO: do this seamlessly
            //var confirm = await MessagePopup.ShowAsync("Do you want to restart the app now?", Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            //if (confirm == ContentDialogResult.Primary)
            //{
            //    await CoreApplication.RequestRestartAsync(string.Empty);
            //}

            ProtoService.Close(true);
        }

        public RelayCommand<AutoDownloadType> AutoDownloadCommand { get; }
        public void AutoDownloadExecute(AutoDownloadType type)
        {
            NavigationService.Navigate(typeof(SettingsDataAutoPage), type);
        }

        public RelayCommand ResetAutoDownloadCommand { get; }
        private async void ResetAutoDownloadExecute()
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ResetAutomaticMediaDownloadAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new GetAutoDownloadSettingsPresets());
                if (response is AutoDownloadSettingsPresets presets)
                {
                    Settings.AutoDownload = Services.Settings.AutoDownloadSettings.FromPreset(presets.High);
                }
                else
                {
                    Settings.AutoDownload = Services.Settings.AutoDownloadSettings.Default;
                }

                RaisePropertyChanged(nameof(AutoDownloadEnabled));
                RaisePropertyChanged(nameof(AutoDownload));
            }
        }
    }
}
