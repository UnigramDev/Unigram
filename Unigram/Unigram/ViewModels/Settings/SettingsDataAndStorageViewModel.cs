using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Common;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Unigram.Services;
using Unigram.Views.Settings;
using Telegram.Td.Api;

namespace Unigram.ViewModels.Settings
{
    public class SettingsDataAndStorageViewModel : UnigramViewModelBase
    {
        public SettingsDataAndStorageViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            //AutoDownloads = new ObservableCollection<SettingsDataAutoDownload>
            //{
            //    new SettingsDataAutoDownload(Strings.Resources.WhenUsingMobileData, NetworkType.Mobile),
            //    new SettingsDataAutoDownload(Strings.Resources.WhenConnectedOnWiFi, NetworkType.WiFi),
            //    new SettingsDataAutoDownload(Strings.Resources.WhenRoaming, NetworkType.Roaming),
            //};

            //AutoDownloadCommand = new RelayCommand<NetworkType>(AutoDownloadExecute);
            AutoDownloadCommand = new RelayCommand<AutoDownloadType>(AutoDownloadExecute);
            ResetAutoDownloadCommand = new RelayCommand(ResetAutoDownloadExecute);
            UseLessDataCommand = new RelayCommand(UseLessDataExecute);
            ProxyCommand = new RelayCommand(ProxyExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //foreach (var item in AutoDownloads)
            //{
            //    item.Refresh();
            //}

            return Task.CompletedTask;
        }

        public libtgvoip.DataSavingMode UseLessData
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

        public bool AutoDownloadEnabled
        {
            get
            {
                return !ProtoService.Preferences.Disabled;
            }
            set
            {
                ProtoService.SetPreferences(ProtoService.Preferences.UpdateDisabled(!value));
                RaisePropertyChanged();
            }
        }

        //public ObservableCollection<SettingsDataAutoDownload> AutoDownloads { get; private set; }

        //public RelayCommand<NetworkType> AutoDownloadCommand { get; }
        //private async void AutoDownloadExecute(NetworkType network)
        //{
        //    var confirm = await SettingsDownloadView.Current.ShowAsync(ApplicationSettings.Current.AutoDownload[network]);
        //    if (confirm == ContentDialogBaseResult.OK)
        //    {
        //        ApplicationSettings.Current.AutoDownload[network] = SettingsDownloadView.Current.SelectedItems;

        //        foreach (var item in AutoDownloads)
        //        {
        //            item.Refresh();
        //        }
        //    }
        //}

        public RelayCommand UseLessDataCommand { get; }
        private async void UseLessDataExecute()
        {
            var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
            var stack = new StackPanel();
            stack.Margin = new Thickness(12, 16, 12, 0);
            stack.Children.Add(new RadioButton { Tag = 0, Content = Strings.Resources.UseLessDataNever, IsChecked = UseLessData == libtgvoip.DataSavingMode.Never });
            stack.Children.Add(new RadioButton { Tag = 1, Content = Strings.Resources.UseLessDataOnMobile, IsChecked = UseLessData == libtgvoip.DataSavingMode.MobileOnly });
            stack.Children.Add(new RadioButton { Tag = 2, Content = Strings.Resources.UseLessDataAlways, IsChecked = UseLessData == libtgvoip.DataSavingMode.Always });

            dialog.Title = Strings.Resources.VoipUseLessData;
            dialog.Content = stack;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

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

        public RelayCommand<AutoDownloadType> AutoDownloadCommand { get; }
        public void AutoDownloadExecute(AutoDownloadType type)
        {
            NavigationService.Navigate(typeof(SettingsDataAutoPage), type);
        }

        public RelayCommand ResetAutoDownloadCommand { get; }
        private async void ResetAutoDownloadExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.ResetAutomaticMediaDownloadAlert, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                ProtoService.SetPreferences(AutoDownloadPreferences.Default);
                RaisePropertyChanged(() => AutoDownloadEnabled);
            }
        }

        public RelayCommand ProxyCommand { get; }
        private async void ProxyExecute()
        {
            var proxy = Settings.Proxy;

            var dialog = new ProxyView(true);
            dialog.Server = proxy.Server;
            dialog.Port = proxy.Port.ToString();
            dialog.Username = proxy.Username;
            dialog.Password = proxy.Password;
            dialog.IsProxyEnabled = proxy.IsEnabled;
            dialog.IsCallsProxyEnabled = proxy.IsCallsEnabled;

            var enabled = proxy.IsEnabled == true;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var server = proxy.Server = dialog.Server ?? string.Empty;
                var port = proxy.Port = Extensions.TryParseOrDefault(dialog.Port, 1080);
                var username = proxy.Username = dialog.Username ?? string.Empty;
                var password = proxy.Password = dialog.Password ?? string.Empty;
                var newValue = proxy.IsEnabled = dialog.IsProxyEnabled;
                proxy.IsCallsEnabled = dialog.IsCallsProxyEnabled;

                if (newValue || newValue != enabled)
                {
                    if (newValue)
                    {
                        //ProtoService.Send(new SetProxy(new ProxySocks5(server, port, username, password)));
                    }
                    else
                    {
                        //ProtoService.Send(new SetProxy(new ProxyEmpty()));
                    }
                }
            }
        }
    }

    //public class SettingsDataAutoDownload : BindableBase
    //{
    //    public SettingsDataAutoDownload(string title, NetworkType network)
    //    {
    //        Title = title;
    //        Type = network;
    //    }

    //    public string Title { get; private set; }

    //    public NetworkType Type { get; private set; }

    //    private AutoDownloadType _flags;
    //    public AutoDownloadType Flags
    //    {
    //        get
    //        {
    //            return _flags;
    //        }
    //        set
    //        {
    //            Set(ref _flags, value);
    //        }
    //    }

    //    public void Refresh()
    //    {
    //        Flags = ApplicationSettings.Current.AutoDownload[Type];
    //    }
    //}
}
