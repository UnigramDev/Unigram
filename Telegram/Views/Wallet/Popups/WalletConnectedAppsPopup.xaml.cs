//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.ObjectModel;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// The dApps this wallet is connected to through TON Connect, and the way to disconnect them.
    /// </summary>
    public sealed partial class WalletConnectedAppsPopup : WalletPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly IEventAggregator _aggregator;

        private readonly ObservableCollection<TonConnectSession> _sessions = new();

        public WalletConnectedAppsPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService)
            : base(wallet, navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _aggregator = navigationService.Session.Resolve<IEventAggregator>();

            Title = Strings.WalletConnectedApps;
            ScrollingHost.ItemsSource = _sessions;

            LoadAsync();
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();

            _aggregator.Subscribe<UpdateTonWalletTonConnectSession>(this, Handle);
        }

        protected override void OnUnloaded()
        {
            _aggregator.Unsubscribe(this);

            base.OnUnloaded();
        }

        // Nothing here is drawn from the wallet's state: the sessions arrive through their own update.
        protected override void UpdateWalletState(WalletState state)
        {
        }

        private async void LoadAsync()
        {
            var sessions = await _wallet.GetConnectedAppsAsync();

            _sessions.Clear();

            foreach (var session in sessions)
            {
                _sessions.Add(session);
            }
        }

        private void Handle(UpdateTonWalletTonConnectSession update)
        {
            this.BeginOnUIThread(() => Apply(update.Session));
        }

        // A session that stops being ready leaves the list, whichever side closed it, and one that
        // becomes ready joins it - a dApp connected on another device while this was open.
        private void Apply(TonConnectSession session)
        {
            var ready = session.State is TonConnectSessionStateReady;

            for (int i = 0; i < _sessions.Count; i++)
            {
                if (_sessions[i].Id == session.Id)
                {
                    if (ready)
                    {
                        _sessions[i] = session;
                    }
                    else
                    {
                        _sessions.RemoveAt(i);
                    }

                    return;
                }
            }

            if (ready)
            {
                _sessions.Add(session);
            }
        }

        #region Recycling

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Session_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell cell)
            {
                cell.UpdateTonConnectSession(_clientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TonConnectSession session)
            {
                Disconnect(session);
            }
        }

        private void Session_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (ScrollingHost.ItemFromContainer(sender) is TonConnectSession session)
            {
                var flyout = new MenuFlyout();
                flyout.CreateFlyoutItem(Disconnect, session, Strings.WalletDisconnect, Icons.Delete, destructive: true);
                flyout.ShowAt(sender, args);
            }
        }

        private async void Disconnect(TonConnectSession session)
        {
            var name = session.Manifest is TonConnectManifestInfo info
                ? WalletHelper.DappName(info.Name, Strings.WalletUnknown)
                : Strings.WalletUnknown;

            var confirm = await MessagePopup.ShowNestedAsync(XamlRoot, string.Format(Strings.WalletDisconnectConfirm, name), Strings.WalletDisconnect, Strings.WalletDisconnect, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                using var lease = _wallet.CreateLease(_navigationService);

                if (!await WalletHelper.EnsureBoundAsync(_clientService, _wallet, _navigationService, lease))
                {
                    return;
                }

                if (await _wallet.DisconnectAppAsync(session, lease))
                {
                    // Not waiting for the update that closes it: the row should go when asked to.
                    // By id, because an update may have replaced the item in the meantime.
                    for (int i = 0; i < _sessions.Count; i++)
                    {
                        if (_sessions[i].Id == session.Id)
                        {
                            _sessions.RemoveAt(i);
                            break;
                        }
                    }
                }
                else
                {
                    _navigationService.ShowToast(Strings.WalletDisconnectFailed, ToastPopupIcon.Error);
                }
            }
            catch (WalletAccessDeniedException)
            {
                // Asked for the key and declined.
            }
            catch (Exception ex)
            {
                Logger.Error("ton connect session could not be disconnected: " + ex.Message);
                _navigationService.ShowToast(Strings.WalletDisconnectFailed, ToastPopupIcon.Error);
            }
        }
    }
}
