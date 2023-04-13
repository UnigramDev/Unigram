//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Native.Calls;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Settings.Popups;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Settings
{
    public class SettingsDataAndStorageViewModel : TLViewModelBase
    {
        public SettingsDataAndStorageViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            AutoDownloadCommand = new RelayCommand<AutoDownloadType>(AutoDownloadExecute);
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

        public List<SettingsOptionItem<VoipDataSaving>> UseLessDataOptions { get; } = new()
        {
            new SettingsOptionItem<VoipDataSaving>(VoipDataSaving.Never, Strings.UseLessDataNever),
            new SettingsOptionItem<VoipDataSaving>(VoipDataSaving.Mobile, Strings.UseLessDataOnMobile),
            new SettingsOptionItem<VoipDataSaving>(VoipDataSaving.Always, Strings.UseLessDataAlways),
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
            await ShowPopupAsync(typeof(SettingsDataAutoPopup), type);
            RaisePropertyChanged(nameof(AutoDownload));
        }

        public async void ResetAutoDownload()
        {
            var confirm = await ShowPopupAsync(Strings.ResetAutomaticMediaDownloadAlert, Strings.AppName, Strings.OK, Strings.Cancel);
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

        public void OpenStorage()
        {
            NavigationService.Navigate(typeof(SettingsStoragePage));
        }

        public void OpenStats()
        {
            NavigationService.Navigate(typeof(SettingsNetworkPage));
        }

        public async void OpenDownloads()
        {
            await new DownloadsPopup(SessionId, NavigationService).ShowQueuedAsync();
        }

        public void OpenProxy()
        {
            NavigationService.Navigate(typeof(SettingsProxiesPage));
        }
    }
}
