using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Template10.Common;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Strings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsDataAndStorageViewModel : UnigramViewModelBase
    {
        public SettingsDataAndStorageViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) : base(protoService, cacheService, aggregator)
        {
            AutoDownloads = new ObservableCollection<SettingsDataAutoDownload>
            {
                new SettingsDataAutoDownload(Strings.Android.WhenUsingMobileData, NetworkType.Mobile),
                new SettingsDataAutoDownload(Strings.Android.WhenConnectedOnWiFi, NetworkType.WiFi),
                new SettingsDataAutoDownload(Strings.Android.WhenRoaming, NetworkType.Roaming),
            };

            AutoDownloadCommand = new RelayCommand<NetworkType>(AutoDownloadExecute);
            UseLessDataCommand = new RelayCommand(UseLessDataExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            foreach (var item in AutoDownloads)
            {
                item.Refresh();
            }

            return Task.CompletedTask;
        }

        public libtgvoip.DataSavingMode UseLessData
        {
            get
            {
                return ApplicationSettings.Current.UseLessData;
            }
            set
            {
                ApplicationSettings.Current.UseLessData = value;
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<SettingsDataAutoDownload> AutoDownloads { get; private set; }

        public RelayCommand<NetworkType> AutoDownloadCommand { get; }
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

        public RelayCommand UseLessDataCommand { get; }
        private async void UseLessDataExecute()
        {
            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 0, Content = Strings.Android.UseLessDataNever, IsChecked = UseLessData == libtgvoip.DataSavingMode.Never });
            stack.Children.Add(new RadioButton { Tag = 1, Content = Strings.Android.UseLessDataOnMobile, IsChecked = UseLessData == libtgvoip.DataSavingMode.MobileOnly });
            stack.Children.Add(new RadioButton { Tag = 2, Content = Strings.Android.UseLessDataAlways, IsChecked = UseLessData == libtgvoip.DataSavingMode.Always });

            dialog.Title = Strings.Android.VoipUseLessData;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Android.OK;
            dialog.SecondaryButtonText = Strings.Android.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var mode = 1;
                foreach (RadioButton current in stack.Children)
                {
                    if (current.IsChecked == true)
                    {
                        mode = (int)current.Tag;
                        break;
                    }
                }

                UseLessData = (libtgvoip.DataSavingMode)mode;
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
