//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Settings.Popups;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsDataAndStorageViewModel : ViewModelBase
    {
        private readonly IStorageService _storageService;

        public SettingsDataAndStorageViewModel(IClientService clientService, ISettingsService settingsService, IStorageService storageService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _storageService = storageService;
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            DownloadFolder = await _storageService.GetDownloadFolderAsync();
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
                SettingsService.Current.IsDownloadFolderEnabled = value;
                RaisePropertyChanged();
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

        public void AutoDownloadPhotos()
        {
            OpenAutoDownload(AutoDownloadType.Photos);
        }

        public void AutoDownloadVideos()
        {
            OpenAutoDownload(AutoDownloadType.Videos);
        }

        public void AutoDownloadDocuments()
        {
            OpenAutoDownload(AutoDownloadType.Documents);
        }

        private async void OpenAutoDownload(AutoDownloadType type)
        {
            await ShowPopupAsync(typeof(SettingsDataAutoPopup), type);
            RaisePropertyChanged(nameof(AutoDownload));
            RaisePropertyChanged(nameof(AutoDownloadDefault));
        }

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
