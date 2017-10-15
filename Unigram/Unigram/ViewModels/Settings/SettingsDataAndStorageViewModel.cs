using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsDataAndStorageViewModel : UnigramViewModelBase
    {
        public SettingsDataAndStorageViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) : base(protoService, cacheService, aggregator)
        {
            AutoDownloads = new ObservableCollection<SettingsDataAutoDownload>
            {
                new SettingsDataAutoDownload("When using mobile data", NetworkType.Mobile),
                new SettingsDataAutoDownload("When connected on Wi-Fi", NetworkType.WiFi),
                new SettingsDataAutoDownload("When roaming", NetworkType.Roaming),
            };
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            foreach (var item in AutoDownloads)
            {
                item.Refresh();
            }

            return Task.CompletedTask;
        }

        public ObservableCollection<SettingsDataAutoDownload> AutoDownloads { get; private set; }

        public RelayCommand<NetworkType> AutoDownloadCommand => new RelayCommand<NetworkType>(AutoDownloadExecute);
        private async void AutoDownloadExecute(NetworkType network)
        {
            var confirm = await SettingsDownloadView.Current.ShowAsync(ApplicationSettings.Current.AutoDownload[network]);
            if (confirm == ContentDialogBaseResult.OK)
            {
                ApplicationSettings.Current.AutoDownload[network] = SettingsDownloadView.Current.SelectedItems;

                foreach (var item in AutoDownloads)
                {
                    item.Refresh();
                }
            }
        }
    }

    public class SettingsDataAutoDownload : BindableBase
    {
        public SettingsDataAutoDownload(string title, NetworkType network)
        {
            Title = title;
            Type = network;
        }

        public string Title { get; private set; }

        public NetworkType Type { get; private set; }

        private AutoDownloadType _flags;
        public AutoDownloadType Flags
        {
            get
            {
                return _flags;
            }
            set
            {
                Set(ref _flags, value);
            }
        }

        public void Refresh()
        {
            Flags = ApplicationSettings.Current.AutoDownload[Type];
        }
    }
}
