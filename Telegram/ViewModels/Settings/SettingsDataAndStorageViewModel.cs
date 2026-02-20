//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsDataAndStorageViewModel : ViewModelBase
    {
        private readonly IStorageService _storageService;

        public SettingsDataAndStorageViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _storageService = storageService;

            AutoDownloadPhotos = new SettingsDataAutoViewModel(clientService, settingsService, aggregator, AutoDownloadType.Photos);
            AutoDownloadVideos = new SettingsDataAutoViewModel(clientService, settingsService, aggregator, AutoDownloadType.Videos);
            AutoDownloadDocuments = new SettingsDataAutoViewModel(clientService, settingsService, aggregator, AutoDownloadType.Documents);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            ClientService.Send(new GetStorageStatisticsFast(), result =>
            {
                if (result is StorageStatisticsFast statistics)
                {
                    BeginOnUIThread(() => StorageUsage = FileSizeConverter.Convert(statistics.FilesSize, true));
                }
            });

            ClientService.Send(new GetNetworkStatistics(false), result =>
            {
                if (result is NetworkStatistics statistics)
                {
                    var sum = statistics.Entries
                        .OfType<NetworkStatisticsEntryFile>()
                        .Sum(x => x.ReceivedBytes + x.SentBytes);

                    BeginOnUIThread(() => NetworkUsage = FileSizeConverter.Convert(sum, true));
                }
            });

            if (IsDownloadFolderEnabled)
            {
                DownloadFolder = await _storageService.GetDownloadFolderAsync();
            }
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

        private string _storageUsage;
        public string StorageUsage
        {
            get => _storageUsage;
            set => Set(ref _storageUsage, value);
        }

        private string _networkUsage;
        public string NetworkUsage
        {
            get => _networkUsage;
            set => Set(ref _networkUsage, value);
        }

        public Services.Settings.AutoDownloadSettings AutoDownload => Settings.AutoDownload;

        public bool AutoDownloadDefault => AutoDownload.IsDefault;

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

        public bool HasDownloadFolder => ApiInfo.HasDownloadFolder;

        public bool IsDownloadFolderEnabled
        {
            get => SettingsService.Current.IsDownloadFolderEnabled;
            set
            {
                if (SettingsService.Current.IsDownloadFolderEnabled != value)
                {
                    SettingsService.Current.IsDownloadFolderEnabled = value;
                    RaisePropertyChanged();

                    UpdateDownloadFolder(value);
                }
            }
        }

        private async void UpdateDownloadFolder(bool value)
        {
            if (value)
            {
                DownloadFolder = await _storageService.GetDownloadFolderAsync();
            }
            else
            {
                DownloadFolder = null;
            }
        }

        private DownloadFolder _downloadFolder;
        public DownloadFolder DownloadFolder
        {
            get => _downloadFolder;
            set => Set(ref _downloadFolder, value);
        }

        public async void ChooseDownloadFolder()
        {
            try
            {
                var picker = new FolderPicker();
                picker.SuggestedStartLocation = PickerLocationId.Downloads;
                picker.FileTypeFilter.Add("*");

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    IsDownloadFolderEnabled = true;
                    DownloadFolder = await _storageService.SetDownloadFolderAsync(folder);
                }
            }
            catch
            {
                ResetDownloadFolder();
            }
        }

        public async void ResetDownloadFolder()
        {
            DownloadFolder = await _storageService.SetDownloadFolderAsync(null);
        }

        public SettingsDataAutoViewModel AutoDownloadPhotos { get; }

        public SettingsDataAutoViewModel AutoDownloadVideos { get; }

        public SettingsDataAutoViewModel AutoDownloadDocuments { get; }

        public async void ResetAutoDownload()
        {
            var confirm = await ShowPopupAsync(Strings.ResetAutomaticMediaDownloadAlert, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                //var response = await ClientService.SendAsync(new GetAutoDownloadSettingsPresets());
                //if (response is AutoDownloadSettingsPresets presets)
                //{
                //    Settings.AutoDownload = Services.Settings.AutoDownloadSettings.FromPreset(presets.High);
                //}
                //else
                {
                    Settings.AutoDownload = Services.Settings.AutoDownloadSettings.Default;
                }

                RaisePropertyChanged(nameof(AutoDownloadEnabled));
                RaisePropertyChanged(nameof(AutoDownloadDefault));
                RaisePropertyChanged(nameof(AutoDownload));
            }
        }

        public async void ClearDrafts()
        {
            var confirm = await ShowPopupAsync(Strings.AreYouSureClearDrafts, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var clear = await ClientService.SendAsync(new ClearAllDraftMessages(true));
            if (clear is Error)
            {
                // TODO
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

        public void OpenProxy()
        {
            NavigationService.Navigate(typeof(SettingsProxyPage));
        }
    }
}
