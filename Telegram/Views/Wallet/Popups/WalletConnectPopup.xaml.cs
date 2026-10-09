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

        // Whether the dApp has had its answer. Closing without one, however it closes, is a no.
        private bool _answered;

        public WalletConnectPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, InternalLinkTypeTonConnect link)
            : base(wallet, navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _link = link;

            _aggregator = navigationService.Session.Resolve<IEventAggregator>();

            PrimaryButtonContent = Strings.WalletConnect;
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

            if (!_answered && _session != null)
            {
                _answered = true;
                _ = _wallet.DeclineConnectAsync(_session, Refusal(), _link.TraceId);
            }

            base.OnUnloaded();
        }

        private WalletConnectRefusal Refusal()
        {
            return _session.Manifest switch
            {
                TonConnectManifestFailed => WalletConnectRefusal.ManifestNotFound,
                TonConnectManifestInvalid => WalletConnectRefusal.ManifestContent,
                TonConnectManifestInfo when _domain == null => WalletConnectRefusal.ManifestContent,
                _ => WalletConnectRefusal.Declined
            };
        }

        protected override void UpdateWalletState(WalletState state)
        {
            // The card is what the user checks: which wallet is about to be handed over, by the
            // address engraved down its side.
            Card.Visibility = state.HasWallet ? Visibility.Visible : Visibility.Collapsed;
            Card.SetState(_clientService, state);

            Subtitle.Text = state.HasWallet
                ? RequestedText()
                : Strings.WalletConnectNoWallet;

            UpdatePrimaryButton();
        }

        private async void CreateAsync()
        {
            if (string.IsNullOrEmpty(_link.ConnectRequest?.ManifestUrl) || string.IsNullOrEmpty(_link.DappClientId))
            {
                // A request that names no dApp is one there is nothing to show and nothing to ask.
                Subtitle.Text = Strings.WalletTonConnectRequestUnavailable;
                return;
            }

            var response = await _clientService.SendAsync(new CreateTonConnectSession(_link.DappClientId, _link.ConnectRequest.ManifestUrl));
            if (response is TonConnectSession session)
            {
                Apply(session);
            }
            else
            {
                Subtitle.Text = Strings.WalletTonConnectRequestUnavailable;
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

            // The url inside the manifest is whatever the manifest says, so on its own it proves
            // nothing: it counts only when it names the host the manifest was fetched from. That
            // host is what is shown and what a proof is bound to.
            var host = WalletHelper.TonConnectHost(_link.ConnectRequest.ManifestUrl);

            if (session.Manifest is TonConnectManifestInfo info && host != null && host == WalletHelper.TonConnectHost(info.Url))
            {
                Title.Text = string.Format(Strings.WalletConnectToApp, WalletHelper.DappName(info.Name, host));
                Footer.Text = Strings.WalletConnectPermissionInfo;

                _domain = host;

                Domain.Text = _domain;

                if (info.Icon?.DocumentValue != null)
                {
                    Photo.Source = new ProfilePictureSourcePhoto(_clientService, session.Id, info.Icon.DocumentValue, info.Icon.Minithumbnail);
                    Photo.Visibility = Visibility.Visible;
                }
            }
            else if (session.Manifest is TonConnectManifestInfo or TonConnectManifestFailed or TonConnectManifestInvalid)
            {
                _domain = null;

                // Named by a manifest that cannot be read, so there is nothing to tell the user
                // about who is asking - which is the one thing they have to judge.
                Title.Text = Strings.WalletConnectToDApp;
                Subtitle.Text = string.Format(Strings.WalletTonConnectManifestLoadFailed, Uri.TryCreate(_link.ConnectRequest.ManifestUrl, UriKind.Absolute, out Uri manifest)
                    ? manifest.Host
                    : _link.ConnectRequest.ManifestUrl);
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
                    return Strings.WalletConnectProofInfo;
                }
            }

            return Strings.WalletConnectInfo;
        }

        private void UpdatePrimaryButton()
        {
            IsPrimaryButtonEnabled = State.HasWallet
                && _session is { Manifest: TonConnectManifestInfo }
                && _domain != null;
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
                using var lease = _wallet.CreateLease(_navigationService, _domain);

                if (!await WalletHelper.EnsureBoundAsync(_clientService, _wallet, _navigationService, lease))
                {
                    return;
                }

                var result = await _wallet.ConnectAsync(session, _link.ConnectRequest, _domain, _link.TraceId, lease);
                if (result.IsConnected)
                {
                    // Before the hide: Unloaded comes after it, and would refuse what was accepted.
                    _answered = true;
                    Hide();
                }
                else
                {
                    ShowError(Strings.WalletConnectFailed);
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
                ShowError(Strings.WalletConnectFailed);
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
