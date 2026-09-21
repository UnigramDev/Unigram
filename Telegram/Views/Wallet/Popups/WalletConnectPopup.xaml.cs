//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text.Json;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// A dApp asking to connect to this wallet, through TON Connect.
    /// </summary>
    /// <remarks>
    /// The session is created the moment this opens, before the user has agreed to anything:
    /// creating it is what makes the account fetch the dApp's manifest, and the manifest is what
    /// there is to agree to. Nothing is signed and nothing is disclosed by it - the wallet's half
    /// of the key exchange is a separate step, and that one does need the user.
    /// </remarks>
    public sealed partial class WalletConnectPopup : WalletPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly IEventAggregator _aggregator;

        private readonly TonConnectLink _link;

        private TonConnectSession _session;

        public WalletConnectPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, TonConnectLink link)
            : base(wallet, navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _link = link;

            _aggregator = navigationService.Session.Resolve<IEventAggregator>();

            PrimaryButtonContent = "[Connect]";
            SecondaryButtonContent = Strings.Cancel;

            IsPrimaryButtonEnabled = false;

            CreateAsync();
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

        protected override void UpdateWalletState(WalletState state)
        {
            // The card is what the user checks: which wallet is about to be handed over, by the
            // address engraved down its side.
            Card.Visibility = state.HasWallet ? Visibility.Visible : Visibility.Collapsed;
            Card.SetState(_clientService, state);

            Subtitle.Text = state.HasWallet
                ? "[Allow this app to see your wallet address]"
                : "[You need a wallet before an app can connect to one.]";

            UpdatePrimaryButton();
        }

        private async void CreateAsync()
        {
            if (!TryGetManifestUrl(_link.Request, out var manifestUrl) || string.IsNullOrEmpty(_link.ClientId))
            {
                // A request that names no dApp is one there is nothing to show and nothing to ask.
                Subtitle.Text = "[This connection request is not valid.]";
                return;
            }

            var response = await _clientService.SendAsync(new CreateTonConnectSession(_link.ClientId, manifestUrl));
            if (response is TonConnectSession session)
            {
                Apply(session);
            }
            else
            {
                Subtitle.Text = "[This connection request could not be opened.]";
            }
        }

        private void Handle(UpdateTonWalletTonConnectSession update)
        {
            // Several may be open at once, and the manifest arrives well after the session does.
            if (_session != null && update.Session.Id == _session.Id)
            {
                this.BeginOnUIThread(() => Apply(update.Session));
            }
        }

        private void Apply(TonConnectSession session)
        {
            _session = session;

            if (session.Manifest is TonConnectManifestInfo info)
            {
                Title.Text = string.Format("[Connect to {0}]", info.Name);
                Footer.Text = string.Format("[{0} won't be able to move funds without permission.]", info.Name);

                // The domain rather than the whole URL: it is what the manifest was fetched from,
                // and the part a user can actually recognise.
                Domain.Text = Uri.TryCreate(info.Url, UriKind.Absolute, out Uri url)
                    ? url.Host
                    : info.Url;

                if (Uri.TryCreate(info.IconUrl, UriKind.Absolute, out Uri icon))
                {
                    Icon.Source = new BitmapImage(icon);
                    IconRoot.Visibility = Visibility.Visible;
                }
            }
            else if (session.Manifest is TonConnectManifestFailed or TonConnectManifestInvalid)
            {
                // Named by a manifest that cannot be read, so there is nothing to tell the user
                // about who is asking - which is the one thing they have to judge.
                Title.Text = "[Unknown app]";
                Subtitle.Text = "[This app could not be identified, so connecting to it is not safe.]";
                Footer.Text = string.Empty;
            }

            UpdatePrimaryButton();
        }

        private void UpdatePrimaryButton()
        {
            IsPrimaryButtonEnabled = State.HasWallet
                && _session is { Manifest: TonConnectManifestInfo };
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            // TODO: the wallet's half of the key exchange, which is where this stops being a
            // question of consent and starts being one of crypto. TDLib wants a wallet_client_id
            // "calculated from the TON wallet private key and session parameters", and the engine's
            // own TON Connect session is written against the HTTP bridge rather than against this
            // transport - so which side derives it is an open question, not a detail.
        }

        /// <summary>
        /// The dApp's manifest URL, out of the connect request the link carries.
        /// </summary>
        private static bool TryGetManifestUrl(string request, out string manifestUrl)
        {
            manifestUrl = null;

            if (string.IsNullOrEmpty(request))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(request);

                if (document.RootElement.TryGetProperty("manifestUrl", out var value))
                {
                    manifestUrl = value.GetString();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("ton connect request is not valid json: " + ex.Message);
            }

            return !string.IsNullOrEmpty(manifestUrl);
        }
    }
}
