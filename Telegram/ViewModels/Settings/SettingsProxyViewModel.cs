//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsProxyViewModel : ViewModelBase, IHandle
    {
        private readonly IProxyService _proxyService;
        private bool _ready;

        public ContentPopup Popup { get; set; }

        public SettingsProxyViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IProxyService proxyService)
            : base(clientService, settingsService, aggregator)
        {
            _proxyService = proxyService;
            _type = settingsService.EnabledProxyId;

            Items = new MvxObservableCollection<ProxyViewModel>();
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            BridgeApplicationContext.LoopbackExempt(true);

            var response = await _proxyService.GetProxiesAsync();
            if (response is AddedProxies proxies)
            {
                Items.ReplaceWith(proxies.Proxies.Select(x => new ProxyViewModel(x)));

                Parallel.ForEach(Items, async (item) =>
                {
                    await UpdateAsync(item);
                });
            }

            _ready = true;
            Handle(ClientService.ConnectionState);
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

            var status = await ClientService.SendAsync(new PingProxy(proxy.Proxy));
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
            BeginOnUIThread(() => Handle(update.State));
        }

        public void Handle(UpdateOption update)
        {
            if (update.Name == OptionsService.R.EnabledProxyId)
            {
                BeginOnUIThread(() => Handle(ClientService.ConnectionState));
            }
        }

        private void Handle(ConnectionState state)
        {
            var enabledProxyId = Settings.EnabledProxyId;

            SetType(enabledProxyId, false);

            foreach (var item in Items)
            {
                item.IsEnabled = item.Id == enabledProxyId;

                if (!item.IsEnabled)
                {
                    if (item.Error != null)
                    {
                        item.Status = new ConnectionStatusError(item.Error);
                    }
                    else if (item.Seconds > 0)
                    {
                        item.Status = new ConnectionStatusReady(false, item.Seconds);
                    }
                    else
                    {
                        item.Status = new ConnectionStatusChecking();
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

        private int _type;
        public int Type
        {
            get => _type;
            set => SetType(value);
        }

        private async void SetType(int value, bool update = true)
        {
            if (_ready is false)
            {
                return;
            }

            if (value > 0 && update && Items.Empty())
            {
                Add();
                RaisePropertyChanged(nameof(IsDisabled));
                RaisePropertyChanged(nameof(IsSystem));
                //RaisePropertyChanged(nameof(IsCustom));

                return;
            }

            if (Set(ref _type, value, nameof(Type)))
            {
                RaisePropertyChanged(nameof(IsDisabled));
                RaisePropertyChanged(nameof(IsSystem));
                //RaisePropertyChanged(nameof(IsCustom));

                if (update)
                {
                    if (IsDisabled)
                    {
                        _proxyService.DisableProxy();
                    }
                    else if (IsSystem)
                    {
                        _proxyService.EnableSystemProxy();
                    }
                    //else if (IsCustom)
                    //{
                    //    _proxyService.EnableProxy(Settings.LastProxyId);
                    //}

                    Handle(ClientService.ConnectionState);
                }
            }
        }

        public bool IsDisabled
        {
            get => _type == 0;
            set
            {
                if (value)
                {
                    SetType(0);
                }
            }
        }

        public bool IsSystem
        {
            get => _type == -1;
            set
            {
                if (value)
                {
                    SetType(-1);
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
            var popup = new ProxyPopup(ClientService);

            var confirm = await ShowPopupAsync2(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = _proxyService.AddProxy(popup.Proxy, false);
            if (response is AddedProxy proxy)
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

            SetType(proxy.Id, false);

            Settings.LastProxyId = proxy.Id;

            _proxyService.EnableProxy(proxy.Id);
            Handle(ClientService.ConnectionState);
        }

        public async void Edit(ProxyViewModel connection)
        {
            SelectedItems.Clear();

            var popup = new ProxyPopup(ClientService, connection);

            var confirm = await ShowPopupAsync2(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = _proxyService.EditProxy(connection.Id, popup.Proxy);
            if (response is AddedProxy proxy)
            {
                var index = Items.IndexOf(connection);
                Items.Remove(connection);

                var edited = new ProxyViewModel(proxy);
                Items.Insert(index, edited);
                await UpdateAsync(edited);
            }

            Handle(ClientService.ConnectionState);
        }

        public async void Delete(ProxyViewModel proxy)
        {
            SelectedItems.Clear();

            var confirm = await ShowPopupAsync2(Strings.DeleteProxyConfirm, Strings.DeleteProxyTitle, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            _proxyService.RemoveProxy(proxy.Id);
            Items.Remove(proxy);

            Handle(ClientService.ConnectionState);
        }

        public async void DeleteSelected()
        {
            var selected = SelectedItems.ToList();

            var confirm = await ShowPopupAsync2(Strings.DeleteProxyMultiConfirm, Strings.DeleteProxyTitle, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            SelectedItems.Clear();

            foreach (var proxy in selected)
            {
                if (proxy.Id == Settings.EnabledProxyId)
                {
                    SetType(0, false);
                }

                _proxyService.RemoveProxy(proxy.Id);
                Items.Remove(proxy);
            }

            Handle(ClientService.ConnectionState);
        }

        public async void Share(ProxyViewModel proxy)
        {
            SelectedItems.Clear();

            var response = await ClientService.SendAsync(new GetInternalLink(new InternalLinkTypeProxy(proxy.Proxy), true));
            if (response is HttpUrl httpUrl)
            {
                await ShowPopupAsync(new ChooseChatsPopup(), new ChooseChatsConfigurationPostLink(httpUrl));
            }
        }

        public async void Copy(ProxyViewModel proxy)
        {
            SelectedItems.Clear();

            var response = await ClientService.SendAsync(new GetInternalLink(new InternalLinkTypeProxy(proxy.Proxy), true));
            if (response is HttpUrl httpUrl)
            {
                MessageHelper.CopyLink(XamlRoot, httpUrl.Url);
            }
        }

        public async void CopySelected()
        {
            var selected = SelectedItems.ToList();
            SelectedItems.Clear();

            var builder = new StringBuilder();

            foreach (var proxy in selected)
            {
                var response = await ClientService.SendAsync(new GetInternalLink(new InternalLinkTypeProxy(proxy.Proxy), true));
                if (response is HttpUrl httpUrl)
                {
                    builder.AppendLine(httpUrl.Url);
                }
            }

            MessageHelper.CopyLink(XamlRoot, builder.ToString());
        }

        public async Task<ContentDialogResult> ShowPopupAsync2(string message, string title = null, string primary = null, string secondary = null, bool destructive = false)
        {
            if (Popup == null)
            {
                return await ShowPopupAsync(message, title, primary, secondary, null, destructive);
            }

            if (Popup != null)
            {
                Popup.IsFinalized = false;
                Popup.Hide();
                Popup.IsFinalized = true;
            }

            var confirm = await ShowPopupAsync(target: null, message, title, primary, secondary, destructive);

            if (Popup != null)
            {
                ShowPopup(Popup);
            }

            return confirm;
        }

        public async Task<ContentDialogResult> ShowPopupAsync2(ContentPopup popup)
        {
            if (Popup != null)
            {
                Popup.IsFinalized = false;
                Popup.Hide();
                Popup.IsFinalized = true;
            }

            var confirm = await ShowPopupAsync(popup);

            if (Popup != null)
            {
                ShowPopup(Popup);
            }

            return confirm;
        }
    }

    public partial class ProxyViewModel : BindableBase
    {
        private readonly AddedProxy _proxy;

        public ProxyViewModel(AddedProxy proxy)
        {
            _proxy = proxy;
            _enabled = proxy.IsEnabled;
        }

        public Proxy Proxy => _proxy.Proxy;
        public ProxyType Type => _proxy.Proxy.Type;

        private bool _enabled;
        public bool IsEnabled
        {
            get => _enabled;
            set => Set(ref _enabled, value);
        }

        public int LastUsedDate => _proxy.LastUsedDate;
        public int Port => _proxy.Proxy.Port;
        public string Server => _proxy.Proxy.Server;
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

    public partial class ConnectionStatusChecking : ConnectionStatus
    {
    }

    public partial class ConnectionStatusConnecting : ConnectionStatus
    {
    }

    public partial class ConnectionStatusReady : ConnectionStatus
    {
        public ConnectionStatusReady(bool connected, double seconds)
        {
            IsConnected = connected;
            Seconds = seconds;
        }

        public bool IsConnected { get; private set; }
        public double Seconds { get; private set; }
    }

    public partial class ConnectionStatusError : ConnectionStatus
    {
        public ConnectionStatusError(Error error)
        {
            Error = error;
        }

        public Error Error { get; private set; }
    }
}
