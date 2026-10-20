//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// A login that also asks for the wallet, through TON Connect.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="WalletConnectPopup"/>, the connect result is not sent from here: it goes
    /// to the server with the login itself, so this only prepares it and hands it back. Nothing is
    /// created before the user agrees, because registering the wallet's key with a session is one
    /// way.
    /// </remarks>
    public sealed partial class WalletOAuthPopup : WalletPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly OauthLinkInfo _info;

        public WalletOAuthPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, OauthLinkInfo info)
            : base(wallet, clientService.Session.Aggregator)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _info = info;

            if (clientService.TryGetUser(info.BotUserId, out User botUser))
            {
                Photo.Source = ProfilePictureSource.User(clientService, botUser);
                Title.Text = string.Format(Strings.WalletConnectToApp, botUser.FullName());
            }

            Domain.Text = info.Domain;

            PrimaryButtonContent = Strings.WalletConnect;
            SecondaryButtonContent = Strings.Cancel;

            IsPrimaryButtonEnabled = false;
        }

        public TonConnectConnectData ConnectData { get; private set; }

        protected override void UpdateWalletState(WalletState state)
        {
            Card.Visibility = state.HasWallet ? Visibility.Visible : Visibility.Collapsed;
            Card.SetState(_clientService, state);

            Subtitle.Text = state.HasWallet
                ? Strings.WalletConnectProofInfo
                : Strings.WalletConnectNoWallet;

            IsPrimaryButtonEnabled = state.HasWallet;
        }

        private async void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            var deferral = args.GetDeferral();

            args.Cancel = true;
            IsPrimaryButtonPending = true;

            try
            {
                using var lease = _wallet.CreateLease(_navigationService, _info.Domain);

                if (!await WalletHelper.EnsureBoundAsync(_clientService, _wallet, _navigationService, lease))
                {
                    return;
                }

                var response = await _clientService.SendAsync(new CreateTonConnectSession(_info.DappClientId, _info.TonConnectManifestUrl));
                if (response is TonConnectSession session)
                {
                    // The proof is bound to the domain the login is for, which is the one on screen.
                    ConnectData = await _wallet.PrepareOauthConnectAsync(session, _info.Domain, _info.TonProofPayload, lease);
                }

                if (ConnectData != null)
                {
                    Hide();
                }
                else
                {
                    ShowError(Strings.WalletConnectFailed);
                }
            }
            catch (WalletAccessDeniedException)
            {
                // They were asked for the key and said no.
            }
            catch (Exception ex)
            {
                Logger.Error("ton connect login could not be prepared: " + ex.Message);
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
            // Another attempt is safe: every one starts from a session of its own.
            Subtitle.Text = message;
        }
    }
}
