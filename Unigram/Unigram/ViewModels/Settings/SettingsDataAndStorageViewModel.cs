using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Native.Calls;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.Views.Popups;
using Unigram.Views.Settings.Popups;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Settings
{
    public class SettingsDataAndStorageViewModel : TLViewModelBase
    {
        public SettingsDataAndStorageViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            AutoDownloadCommand = new RelayCommand<AutoDownloadType>(AutoDownloadExecute);
            ResetAutoDownloadCommand = new RelayCommand(ResetAutoDownloadExecute);
            StoragePathCommand = new RelayCommand<bool>(StoragePathExecute);
        }

        public int UseLessData
        {
            get => Array.IndexOf(_useLessDataIndexer, Settings.UseLessData);
            set
            {
                if (Settings.UseLessData != _useLessDataIndexer[value])
                {
                    Settings.UseLessData = _useLessDataIndexer[value];
                    RaisePropertyChanged();
                }
            }
        }

        private readonly VoipDataSaving[] _useLessDataIndexer = new[]
        {
            VoipDataSaving.Never,
            VoipDataSaving.Mobile,
            VoipDataSaving.Always
        };

        public List<SettingsOptionItem<VoipDataSaving>> UseLessDataOptions => new List<SettingsOptionItem<VoipDataSaving>>
        {
            new SettingsOptionItem<VoipDataSaving>(VoipDataSaving.Never, Strings.Resources.UseLessDataNever),
            new SettingsOptionItem<VoipDataSaving>(VoipDataSaving.Mobile, Strings.Resources.UseLessDataOnMobile),
            new SettingsOptionItem<VoipDataSaving>(VoipDataSaving.Always, Strings.Resources.UseLessDataAlways),
        };


        public Services.Settings.AutoDownloadSettings AutoDownload => Settings.AutoDownload;

        public bool AutoDownloadEnabled
        {
            get => !Settings.AutoDownload.Disabled;
            set
            {
                Settings.AutoDownload = Settings.AutoDownload.UpdateDisabled(!value);
                RaisePropertyChanged();
            }
        }


        public bool IsAutoPlayAnimationsEnabled
        {
            get => Settings.IsAutoPlayAnimationsEnabled;
            set
            {
                Settings.IsAutoPlayAnimationsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsAutoPlayVideosEnabled
        {
            get => Settings.IsAutoPlayVideosEnabled;
            set
            {
                Settings.IsAutoPlayVideosEnabled = value;
                RaisePropertyChanged();
            }
        }

        public bool IsStreamingEnabled
        {
            get => SettingsService.Current.IsStreamingEnabled;
            set
            {
                SettingsService.Current.IsStreamingEnabled = value;
                RaisePropertyChanged();
            }
        }

        public string FilesDirectory
        {
            get => Settings.FilesDirectory;
            set
            {
                Settings.FilesDirectory = value;
                RaisePropertyChanged();
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

            ClientService.Close(true);
        }

        public RelayCommand<AutoDownloadType> AutoDownloadCommand { get; }
        public async void AutoDownloadExecute(AutoDownloadType type)
        {
            await NavigationService.ShowAsync(typeof(SettingsDataAutoPopup), type);
            RaisePropertyChanged(nameof(AutoDownload));
        }

        public RelayCommand ResetAutoDownloadCommand { get; }
        private async void ResetAutoDownloadExecute()
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ResetAutomaticMediaDownloadAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new GetAutoDownloadSettingsPresets());
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
