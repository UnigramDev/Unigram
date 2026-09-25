//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Windows.UI.Xaml;

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

        private readonly InternalLinkTypeTonConnect _link;

        private TonConnectSession _session;
        private string _domain;

        public WalletConnectPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, InternalLinkTypeTonConnect link)
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
                ? RequestedText()
                : "[You need a wallet before an app can connect to one.]";

            UpdatePrimaryButton();
        }

        private async void CreateAsync()
        {
            if (string.IsNullOrEmpty(_link.ConnectRequest?.ManifestUrl) || string.IsNullOrEmpty(_link.DappClientId))
            {
                // A request that names no dApp is one there is nothing to show and nothing to ask.
                Subtitle.Text = "[This connection request is not valid.]";
                return;
            }

            var response = await _clientService.SendAsync(new CreateTonConnectSession(_link.DappClientId, _link.ConnectRequest.ManifestUrl));
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
                // and the part a user can actually recognise. Kept, because a proof is bound to the
                // domain and must be bound to the one that was on screen.
                _domain = Uri.TryCreate(info.Url, UriKind.Absolute, out Uri url)
                    ? url.Host
                    : info.Url;

                Domain.Text = _domain;

                if (info.Icon?.DocumentValue != null)
                {
                    Photo.Source = new ProfilePictureSourcePhoto(_clientService, session.Id, info.Icon.DocumentValue, info.Icon.Minithumbnail);
                    Photo.Visibility = Visibility.Visible;
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

        /// <summary>
        /// What the dApp is asking for, which is not always only the address.
        /// </summary>
        /// <remarks>
        /// A proof is a signature, and the one thing a user has to be told apart from the address:
        /// it signs them in, and it is not a transfer. The wording comes from the spec's own
        /// guidance rather than from the item name.
        /// </remarks>
        private string RequestedText()
        {
            foreach (var item in _link.ConnectRequest.Items)
            {
                if (item is TonConnectConnectItemProof)
                {
                    return "[Allow this app to see your wallet address and sign you in. This is not a transfer.]";
                }
            }

            return "[Allow this app to see your wallet address]";
        }

        private void UpdatePrimaryButton()
        {
            IsPrimaryButtonEnabled = State.HasWallet
                && _session is { Manifest: TonConnectManifestInfo };
        }

        /// <summary>
        /// Hands the request to the wallet, which does everything the protocol asks for.
        /// </summary>
        /// <remarks>
        /// The domain goes with it because it is the one that was on screen: a <c>ton_proof</c> is
        /// bound to the domain the user approved, and computing it again here could differ from
        /// what they saw.
        /// </remarks>
        private async void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();
            var session = _session;

            if (session == null)
            {
                deferral.Complete();
                return;
            }

            args.Cancel = true;
            IsPrimaryButtonPending = true;

            try
            {
                if (!await WalletHelper.EnsureBoundAsync(_clientService, _wallet, _navigationService))
                {
                    return;
                }

                var result = await _wallet.ConnectAsync(_navigationService, session, _link.ConnectRequest, _domain, _link.TraceId);
                if (result.IsConnected)
                {
                    Hide();
                }
                else
                {
                    ShowError("[This app could not be connected.]");
                }
            }
            catch (WalletAccessDeniedException)
            {
                // They were asked for the key and said no. Nothing to tell them that they did not
                // just say.
            }
            catch (Exception ex)
            {
                Logger.Error("ton connect could not be accepted: " + ex.Message);
                ShowError("[This app could not be connected.]");
            }
            finally
            {
                IsPrimaryButtonPending = false;
                deferral.Complete();
            }
        }

        private void ShowError(string message)
        {
            Subtitle.Text = message;
            IsPrimaryButtonEnabled = false;
        }
    }
}
