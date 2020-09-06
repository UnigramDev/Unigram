using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Native.Calls;
using Unigram.Services;
using Unigram.Services.Settings;
using Unigram.Views.Popups;
using Unigram.Views.Settings;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
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
            DownloadLocationCommand = new RelayCommand(DownloadLocationExecute);
            UseLessDataCommand = new RelayCommand(UseLessDataExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //foreach (var item in AutoDownloads)
            //{
            //    item.Refresh();
            //}

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

        public RelayCommand DownloadLocationCommand { get; }
        private async void DownloadLocationExecute()
        {
            var dialog = new ContentPopup();
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 1, Content = "Temp folder, cleared on logout or uninstall", IsChecked = FilesDirectory == null });
            stack.Children.Add(new RadioButton { Tag = 2, Content = "Custom folder, cleared only manually", IsChecked = FilesDirectory != null });

            dialog.Title = "Choose download location";
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var mode = 1;
                var path = FilesDirectory + string.Empty;
                foreach (RadioButton current in stack.Children)
                {
                    if (current.IsChecked == true)
                    {
                        mode = (int)current.Tag;
                        break;
                    }
                }

                switch (mode)
                {
                    case 0:
                        break;
                    case 1:
                        FilesDirectory = null;
                        break;
                    case 2:
                        var picker = new FolderPicker();
                        picker.SuggestedStartLocation = PickerLocationId.Downloads;
                        picker.FileTypeFilter.Add("*");

                        var folder = await picker.PickSingleFolderAsync();
                        if (folder != null)
                        {
                            StorageApplicationPermissions.FutureAccessList.AddOrReplace("FilesDirectory", folder);
                            FilesDirectory = folder.Path;
                        }

                        break;
                }

                if (string.Equals(path, FilesDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                ProtoService.Send(new Close());
            }
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

                RaisePropertyChanged(() => AutoDownloadEnabled);
                RaisePropertyChanged(() => AutoDownload);
            }
        }
    }
}
