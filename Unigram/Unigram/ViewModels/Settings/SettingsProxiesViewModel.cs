using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsProxiesViewModel : TLViewModelBase, IHandle<UpdateConnectionState>, IHandle<UpdateOption>
    {
        public SettingsProxiesViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<ConnectionViewModel>();

            AddCommand = new RelayCommand(AddExecute);
            EnableCommand = new RelayCommand<ConnectionViewModel>(EnableExecute);
            EditCommand = new RelayCommand<ConnectionViewModel>(EditExecute);
            RemoveCommand = new RelayCommand<ProxyViewModel>(RemoveExecute);
            ShareCommand = new RelayCommand<ProxyViewModel>(ShareExecute);

            aggregator.Subscribe(this);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.SendAsync(new GetProxies());
            if (response is Proxies proxies)
            {
                var items = proxies.ProxiesValue.Select(x => new ProxyViewModel(x) as ConnectionViewModel).ToList();
                items.Insert(0, new ConnectionViewModel());

                Items.ReplaceWith(items);
                SelectedItem = items.OfType<ProxyViewModel>().FirstOrDefault(x => x.IsEnabled) ?? items.FirstOrDefault();

                if (_selectedItem != null)
                {
                    _selectedItem.IsEnabled = true;
                }

                Parallel.ForEach(items, async (item) =>
                {
                    await UpdateAsync(item);
                });
            }
        }

        private async Task UpdateAsync(ConnectionViewModel proxy)
        {
            var status = await ProtoService.SendAsync(new PingProxy(proxy.Id));
            BeginOnUIThread(() =>
            {
                if (status is Seconds seconds)
                {
                    proxy.Seconds = seconds.SecondsValue;
                    proxy.Error = null;
                    proxy.Status = new ConnectionStatusReady(proxy.IsEnabled, seconds.SecondsValue);
                }
                else if (status is Error error)
                {
                    proxy.Seconds = 0;
                    proxy.Error = error;
                    proxy.Status = new ConnectionStatusError(error);
                }
            });
        }

        public void Handle(UpdateConnectionState update)
        {
            BeginOnUIThread(() => Handle(update.State, CacheService.Options.EnabledProxyId));
        }

        public void Handle(UpdateOption update)
        {
            if (string.Equals(update.Name, "enabled_proxy_id", StringComparison.OrdinalIgnoreCase))
            {
                BeginOnUIThread(() => Handle(CacheService.GetConnectionState(), CacheService.Options.EnabledProxyId));
            }
        }

        private void Handle(ConnectionState state, int enabledProxyId)
        {
            foreach (var item in Items)
            {
                item.IsEnabled = item.Id == enabledProxyId;

                if (!item.IsEnabled)
                {
                    if (item.Error != null)
                    {
                        item.Status = new ConnectionStatusError(item.Error);
                    }
                    else
                    {
                        item.Status = new ConnectionStatusReady(false, item.Seconds);
                    }

                    continue;
                }

                switch (state)
                {
                    case ConnectionStateWaitingForNetwork waitingForNetwork:
                        //ShowStatus(Strings.Resources.WaitingForNetwork);
                        break;
                    case ConnectionStateConnecting connecting:
                    case ConnectionStateConnectingToProxy connectingToProxy:
                        item.Status = new ConnectionStatusConnecting();
                        break;
                    case ConnectionStateUpdating updating:
                    case ConnectionStateReady ready:
                        item.Status = new ConnectionStatusReady(true, item.Seconds);
                        break;
                }
            }
        }

        public MvxObservableCollection<ConnectionViewModel> Items { get; private set; }

        private ConnectionViewModel _selectedItem;
        public ConnectionViewModel SelectedItem
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
            var dialog = new ProxyPopup();
            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new AddProxy(dialog.Server, dialog.Port, false, dialog.Type));
            if (response is Proxy proxy)
            {
                var connection = new ProxyViewModel(proxy);
                Items.Add(connection);
                await UpdateAsync(connection);
            }
        }

        public RelayCommand<ConnectionViewModel> EnableCommand { get; }
        private async void EnableExecute(ConnectionViewModel connection)
        {
            MarkAsEnabled(connection);

            if (connection is ProxyViewModel proxy)
            {
                await ProtoService.SendAsync(new EnableProxy(proxy.Id));
            }
            else
            {
                await ProtoService.SendAsync(new DisableProxy());
            }

            Handle(CacheService.GetConnectionState(), CacheService.Options.EnabledProxyId);
        }

        public RelayCommand<ConnectionViewModel> EditCommand { get; }
        private async void EditExecute(ConnectionViewModel connection)
        {
            var dialog = new ProxyPopup(connection as ProxyViewModel);
            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new EditProxy(connection.Id, dialog.Server, dialog.Port, false, dialog.Type));
            if (response is Proxy proxy)
            {
                var index = Items.IndexOf(connection);
                Items.Remove(connection);

                var edited = new ProxyViewModel(proxy);
                Items.Insert(index, edited);
                await UpdateAsync(edited);
            }

            Handle(CacheService.GetConnectionState(), CacheService.Options.EnabledProxyId);
        }

        public RelayCommand<ProxyViewModel> RemoveCommand { get; }
        private async void RemoveExecute(ProxyViewModel proxy)
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.DeleteProxy, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new RemoveProxy(proxy.Id));
            if (response is Ok)
            {
                Items.Remove(proxy);
            }

            Handle(CacheService.GetConnectionState(), CacheService.Options.EnabledProxyId);
        }

        public RelayCommand<ProxyViewModel> ShareCommand { get; }
        private async void ShareExecute(ProxyViewModel proxy)
        {
            var response = await ProtoService.SendAsync(new GetProxyLink(proxy.Id));
            if (response is Text text && Uri.TryCreate(text.TextValue, UriKind.Absolute, out Uri uri))
            {
                await SharePopup.GetForCurrentView().ShowAsync(uri, Strings.Resources.Proxy);
            }
        }

        private void MarkAsEnabled(ConnectionViewModel connection)
        {
            foreach (var item in Items)
            {
                item.IsEnabled = item.Id == connection.Id;
            }
        }
    }

    public class ConnectionViewModel : BindableBase
    {
        private ConnectionStatus _status = new ConnectionStatusChecking();
        public ConnectionStatus Status
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

        public double Seconds { get; set; }
        public Error Error { get; set; }

        public virtual bool IsEnabled { get; set; }
        public virtual int Id { get; private set; }
    }

    public class ProxyViewModel : ConnectionViewModel
    {
        private readonly Proxy _proxy;

        public ProxyViewModel(Proxy proxy)
        {
            _proxy = proxy;
        }

        public ProxyType Type => _proxy.Type;
        public override bool IsEnabled { get => _proxy.IsEnabled; set => _proxy.IsEnabled = value; }
        public int LastUsedDate => _proxy.LastUsedDate;
        public int Port => _proxy.Port;
        public string Server => _proxy.Server;
        public override int Id => _proxy.Id;
    }

    public interface ConnectionStatus
    {
    }

    public class ConnectionStatusChecking : ConnectionStatus
    {
    }

    public class ConnectionStatusConnecting : ConnectionStatus
    {
    }

    public class ConnectionStatusReady : ConnectionStatus
    {
        public ConnectionStatusReady(bool connected, double seconds)
        {
            IsConnected = connected;
            Seconds = seconds;
        }

        public bool IsConnected { get; private set; }
        public double Seconds { get; private set; }
    }

    public class ConnectionStatusError : ConnectionStatus
    {
        public ConnectionStatusError(Error error)
        {
            Error = error;
        }

        public Error Error { get; private set; }
    }
}
