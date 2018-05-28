using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsProxiesViewModel : UnigramViewModelBase
    {
        public SettingsProxiesViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<ProxyViewModel>();

            AddCommand = new RelayCommand(AddExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.SendAsync(new GetProxies());
            if (response is Proxies proxies)
            {
                var items = proxies.ProxiesValue.Select(x => new ProxyViewModel(x)).ToList();

                Items.ReplaceWith(items);
                SelectedItem = items.FirstOrDefault(x => x.IsEnabled);

                Parallel.ForEach(items, async proxy =>
                {
                    var status = await ProtoService.SendAsync(new PingProxy(proxy.Id));
                    BeginOnUIThread(() =>
                    {
                        if (status is Seconds seconds)
                        {
                            proxy.Status = new ProxyStatus(seconds.SecondsValue);
                        }
                        else if (status is Error error)
                        {
                            proxy.Status = new ProxyStatus(error);
                        }
                    });
                });
            }
        }

        public MvxObservableCollection<ProxyViewModel> Items { get; private set; }

        private ProxyViewModel _selectedItem;
        public ProxyViewModel SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
            }
        }

        public RelayCommand AddCommand { get; }
        private async void AddExecute()
        {
            var response = await ProtoService.SendAsync(new AddProxy("192.168.1.55", 8888, false, new ProxyTypeMtproto("fdda254c78d9fa202ac536079e88b808")));
            if (response is Proxy proxy)
            {
                Items.Add(new ProxyViewModel(proxy));
            }
        }

        public RelayCommand<ProxyViewModel> RemoveCommand { get; }
        private async void RemoveExecute(ProxyViewModel proxy)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.DeleteProxy, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm == Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                var response = await ProtoService.SendAsync(new RemoveProxy(proxy.Id));
                if (response is Ok)
                {
                    Items.Remove(proxy);
                }
            }
        }
    }

    public class ProxyViewModel : BindableBase
    {
        private readonly Proxy _proxy;

        public ProxyViewModel(Proxy proxy)
        {
            _proxy = proxy;
        }

        public ProxyType Type => _proxy.Type;
        public bool IsEnabled => _proxy.IsEnabled;
        public int LastUsedDate => _proxy.LastUsedDate;
        public int Port => _proxy.Port;
        public string Server => _proxy.Server;
        public int Id => _proxy.Id;

        private ProxyStatus _status;
        public ProxyStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                Set(ref _status, value);
            }
        }
    }

    public class ProxyStatus
    {
        public ProxyStatus(double seconds)
        {
            Seconds = seconds;
        }

        public ProxyStatus(Error error)
        {
            Error = error;
        }

        public double Seconds { get; private set; }
        public Error Error { get; private set; }
    }
}
