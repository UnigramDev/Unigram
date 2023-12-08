//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public enum SettingsProxyType
    {
        Disabled,
        System,
        Custom
    }

    public class SettingsProxyViewModel : ViewModelBase, IHandle
    {
        private readonly INetworkService _networkService;
        private int _systemProxyId;

        public SettingsProxyViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, INetworkService networkService)
            : base(clientService, settingsService, aggregator)
        {
            _networkService = networkService;

            Items = new MvxObservableCollection<ProxyViewModel>();
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            SystemTray.LoopbackExempt(true);

            var systemId = _systemProxyId = await _networkService.GetSystemProxyId();

            if (ClientService.Options.EnabledProxyId == 0)
            {
                SetType(SettingsProxyType.Disabled, false);
            }
            else if (ClientService.Options.EnabledProxyId == systemId)
            {
                SetType(SettingsProxyType.System, false);
            }
            else
            {
                SetType(SettingsProxyType.Custom, false);
            }

            var response = await ClientService.SendAsync(new GetProxies());
            if (response is Proxies proxies)
            {
                Items.ReplaceWith(proxies.ProxiesValue.Where(x => x.Id != systemId).Select(x => new ProxyViewModel(x)));

                Parallel.ForEach(Items, async (item) =>
                {
                    await UpdateAsync(item);
                });

                _networkService.ProxyChanged += OnSystemProxyChanged;
            }

            Handle(ClientService.ConnectionState, ClientService.Options.EnabledProxyId);
        }

        protected override void OnNavigatedFrom(NavigationState pageState, bool suspending)
        {
            _networkService.ProxyChanged -= OnSystemProxyChanged;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateConnectionState>(this, Handle)
                .Subscribe<UpdateOption>(Handle);
        }

        private async Task UpdateAsync(ProxyViewModel proxy)
        {
            var proxyId = proxy.Id;
            //if (proxy is SystemProxyViewModel && !HttpProxyWatcher.Current.IsEnabled)
            //{
            //    proxyId = 0;
            //}

            var status = await ClientService.SendAsync(new PingProxy(proxyId));
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

        private void OnSystemProxyChanged(object sender, Proxy proxy)
        {
            //var connection = Items.FirstOrDefault(x => x is SystemProxyViewModel);
            //if (connection == null || proxy == null)
            //{
            //    return;
            //}

            //BeginOnUIThread(async () =>
            //{
            //    var selected = SelectedItem == connection;
            //    var index = Items.IndexOf(connection);
            //    if (index != 1)
            //    {
            //        return;
            //    }

            //    var edited = new SystemProxyViewModel(proxy);
            //    Items.Remove(connection);
            //    Items.Insert(index, edited);

            //    if (selected)
            //    {
            //        SelectedItem = edited;
            //    }

            //    await UpdateAsync(edited);
            //});
        }

        public void Handle(UpdateConnectionState update)
        {
            BeginOnUIThread(() => Handle(update.State, ClientService.Options.EnabledProxyId));
        }

        public void Handle(UpdateOption update)
        {
            if (string.Equals(update.Name, "enabled_proxy_id", StringComparison.OrdinalIgnoreCase))
            {
                BeginOnUIThread(() => Handle(ClientService.ConnectionState, ClientService.Options.EnabledProxyId));
            }
        }

        private void Handle(ConnectionState state, long enabledProxyId)
        {
            if (enabledProxyId == 0)
            {
                SetType(SettingsProxyType.Disabled, false);
            }
            else if (enabledProxyId == _systemProxyId)
            {
                SetType(SettingsProxyType.System, false);
            }
            else
            {
                SetType(SettingsProxyType.Custom, false);
            }

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
                    case ConnectionStateWaitingForNetwork:
                        //ShowStatus(Strings.WaitingForNetwork);
                        break;
                    case ConnectionStateConnecting:
                    case ConnectionStateConnectingToProxy:
                        item.Status = new ConnectionStatusConnecting();
                        break;
                    case ConnectionStateUpdating:
                    case ConnectionStateReady:
                        item.Status = new ConnectionStatusReady(true, item.Seconds);
                        break;
                }
            }
        }

        private SettingsProxyType _type;
        public SettingsProxyType Type
        {
            get => _type;
            set => SetType(value);
        }

        private async void SetType(SettingsProxyType value, bool update = true)
        {
            if (value == SettingsProxyType.Custom && Items.Empty())
            {
                Add();
                RaisePropertyChanged(nameof(IsDisabled));
                RaisePropertyChanged(nameof(IsSystem));
                RaisePropertyChanged(nameof(IsCustom));

                return;
            }

            if (Set(ref _type, value, nameof(Type)))
            {
                RaisePropertyChanged(nameof(IsDisabled));
                RaisePropertyChanged(nameof(IsSystem));
                RaisePropertyChanged(nameof(IsCustom));

                if (update)
                {
                    if (IsDisabled)
                    {
                        _networkService.UseSystemProxy = false;
                        await ClientService.SendAsync(new DisableProxy());
                    }
                    else if (IsSystem)
                    {
                        _networkService.UseSystemProxy = true;
                        await ClientService.SendAsync(new EnableProxy(_systemProxyId));
                    }
                    else if (IsCustom)
                    {
                        _networkService.UseSystemProxy = false;
                        await ClientService.SendAsync(new EnableProxy(Settings.LastProxyId));
                    }

                    Handle(ClientService.ConnectionState, ClientService.Options.EnabledProxyId);
                }
            }
        }

        public bool IsDisabled
        {
            get => _type == SettingsProxyType.Disabled;
            set
            {
                if (value)
                {
                    SetType(SettingsProxyType.Disabled);
                }
            }
        }

        public bool IsSystem
        {
            get => _type == SettingsProxyType.System;
            set
            {
                if (value)
                {
                    SetType(SettingsProxyType.System);
                }
            }
        }

        public bool IsCustom
        {
            get => _type == SettingsProxyType.Custom;
            set
            {
                if (value)
                {
                    SetType(SettingsProxyType.Custom);
                }
            }
        }

        public MvxObservableCollection<ProxyViewModel> Items { get; private set; }

        public MvxObservableCollection<ProxyViewModel> SelectedItems { get; private set; } = new();

        public void Select(ProxyViewModel item)
        {
            SelectedItems.Add(item);
        }

        public async void Add()
        {
            var popup = new ProxyPopup();
            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new AddProxy(popup.Server, popup.Port, false, popup.Type));
            if (response is Proxy proxy)
            {
                var connection = new ProxyViewModel(proxy);
                Items.Add(connection);
                Enable(connection);

                await UpdateAsync(connection);
            }
        }

        public async void Enable(ProxyViewModel proxy)
        {
            SelectedItems.Clear();

            SetType(SettingsProxyType.Custom, false);

            Settings.LastProxyId = proxy.Id;

            _networkService.UseSystemProxy = false;
            await ClientService.SendAsync(new EnableProxy(proxy.Id));

            Handle(ClientService.ConnectionState, ClientService.Options.EnabledProxyId);
        }

        public async void Edit(ProxyViewModel connection)
        {
            SelectedItems.Clear();

            var popup = new ProxyPopup(connection);
            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new EditProxy(connection.Id, popup.Server, popup.Port, false, popup.Type));
            if (response is Proxy proxy)
            {
                var index = Items.IndexOf(connection);
                Items.Remove(connection);

                var edited = new ProxyViewModel(proxy);
                Items.Insert(index, edited);
                await UpdateAsync(edited);
            }

            Handle(ClientService.ConnectionState, ClientService.Options.EnabledProxyId);
        }

        public async void Delete(ProxyViewModel proxy)
        {
            SelectedItems.Clear();

            var confirm = await ShowPopupAsync(Strings.DeleteProxyConfirm, Strings.DeleteProxyTitle, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new RemoveProxy(proxy.Id));
            if (response is Ok)
            {
                Items.Remove(proxy);
            }

            Handle(ClientService.ConnectionState, ClientService.Options.EnabledProxyId);
        }

        public async void DeleteSelected()
        {
            var selected = SelectedItems.ToList();
            SelectedItems.Clear();

            var confirm = await ShowPopupAsync(Strings.DeleteProxyMultiConfirm, Strings.DeleteProxyTitle, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            foreach (var proxy in selected)
            {
                if (proxy.Id == ClientService.Options.EnabledProxyId)
                {
                    SetType(SettingsProxyType.Disabled, false);
                }

                var response = await ClientService.SendAsync(new RemoveProxy(proxy.Id));
                if (response is Ok)
                {
                    Items.Remove(proxy);
                }
            }

            Handle(ClientService.ConnectionState, ClientService.Options.EnabledProxyId);
        }

        public async void Share(ProxyViewModel proxy)
        {
            SelectedItems.Clear();

            var response = await ClientService.SendAsync(new GetProxyLink(proxy.Id));
            if (response is HttpUrl httpUrl)
            {
                await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationPostLink(httpUrl));
            }
        }
    }

    public class ProxyViewModel : BindableBase
    {
        private readonly Proxy _proxy;

        public ProxyViewModel(Proxy proxy)
        {
            _proxy = proxy;
            _enabled = proxy.IsEnabled;
        }

        public ProxyType Type => _proxy.Type;

        private bool _enabled;
        public bool IsEnabled
        {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        public int LastUsedDate => _proxy.LastUsedDate;
        public int Port => _proxy.Port;
        public string Server => _proxy.Server;
        public int Id => _proxy.Id;

        public string DisplayName => $"{Server}:{Port}";

        private ConnectionStatus _status = new ConnectionStatusChecking();
        public ConnectionStatus Status
        {
            get => _status;
            set => Set(ref _status, value);
        }

        public double Seconds { get; set; }
        public Error Error { get; set; }
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
