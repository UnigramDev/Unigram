//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// A dApp asking to spend from this wallet, and the two ways of looking at it.
    /// </summary>
    /// <remarks>
    /// Two pages, one sheet. The first is the card with the amount on it and the recipient
    /// engraved, which is what somebody who trusts the dApp needs and nothing more. The second is
    /// what the request literally says, and under it the emulation - what the chain would actually
    /// do - because the dApp's description of its own request is a claim rather than a fact.
    ///
    /// Only <c>sendTransaction</c> reaches here. <c>signData</c> and <c>signMessage</c> are
    /// answered with an error without a sheet.
    /// </remarks>
    public sealed partial class WalletRequestPopup : WalletPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly MessageTonConnectRequest _message;
        private readonly long _messageId;

        private WalletRequest _request;
        private bool _details;

        // Held from Review until the sheet closes. Reading the request and answering it are one
        // act as far as the user is concerned, and asking them twice for the same credential -
        // once to see what is being requested, once to agree to it - is asking twice for nothing.
        private WalletVault.WalletVaultLease _lease;

        // The manifest's, not the message's. See UpdateSession.
        private string _domain;

        public WalletRequestPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, long messageId, MessageTonConnectRequest message)
            : base(wallet, navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _messageId = messageId;
            _message = message;

            SecondaryButtonText = Strings.WalletCancel;

            var name = WalletHelper.DappName(message.DappName, Strings.WalletUnknown);

            Photo.Source = ProfilePictureSourceText.GetNameForChat(name);

            // Not the name: the domain line is for the domain, and the name is the dApp's to
            // choose - one called "fragment.com" would sit where the real domain goes.
            Domain.Text = string.Empty;

            ReviewLabel.Text = string.Format(Strings.WalletAppRequestsTransfer, name);

            UpdateDetails();
        }

        /// <summary>
        /// Everything that needs the wallet key, which is everything past the dApp's name.
        /// </summary>
        /// <remarks>
        /// Behind a button on purpose. The request is encrypted to a session derived from the
        /// wallet key, so it cannot be read without a confirmation - and a dApp must not be able to
        /// raise that prompt on its own, before the user has been told who is asking.
        /// </remarks>
        private async void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            if (_request != null)
            {
                AnswerAsync(args, true);
                return;
            }

            var deferral = args.GetDeferral();

            args.Cancel = true;
            IsPrimaryButtonPending = true;

            try
            {
                _lease ??= _wallet.CreateLease(_navigationService);

                if (await WalletHelper.EnsureBoundAsync(_clientService, _wallet, _navigationService, _lease))
                {
                    _request = await _wallet.GetRequestAsync(_messageId, _message, _lease);
                }
            }
            catch (WalletNotBoundException)
            {
                // Nothing to sign with, and binding was refused a moment ago.
            }
            catch (Exception ex)
            {
                Logger.Error("ton connect request could not be read: " + ex.Message);
            }
            finally
            {
                IsPrimaryButtonPending = false;
                deferral.Complete();
            }

            if (_request == null)
            {
                // Refused, expired, or answered elsewhere. Nothing here can say which, and the
                // sheet has nothing left to show either way.
                Hide();
                return;
            }

            MessagesPanel.ItemsSource = _request.Messages;

            UpdateDetails();
            UpdatePreview();
            UpdateWalletState(State);
        }

        protected override void OnLoaded()
        {
            base.OnLoaded();

            UpdateSession();
        }

        /// <summary>
        /// Who is asking, taken from the session rather than from the request.
        /// </summary>
        /// <remarks>
        /// The service message names the dApp with a string the dApp chose. The session carries its
        /// manifest - the domain it was fetched from and the icon declared there - which is what a
        /// user can actually judge, and the same identity the connect sheet showed when this dApp
        /// was let in.
        ///
        /// It needs no key, so it fills in before Review rather than after: deciding whether to
        /// unlock the wallet is exactly the decision that needs to know who is asking.
        /// </remarks>
        private async void UpdateSession()
        {
            var session = await _wallet.GetSessionAsync(_message.SessionId);

            if (session?.Manifest is not TonConnectManifestInfo info)
            {
                return;
            }

            _domain = WalletHelper.TonConnectHost(info.Url) ?? string.Empty;

            Domain.Text = _domain;
            ReviewLabel.Text = string.Format(Strings.WalletAppRequestsTransfer, WalletHelper.DappName(info.Name, _domain));

            if (info.Icon?.DocumentValue != null)
            {
                Photo.Source = new ProfilePictureSourcePhoto(_clientService, session.Id, info.Icon.DocumentValue, info.Icon.Minithumbnail);
            }
        }

        protected override void UpdateWalletState(WalletState state)
        {
            if (_request == null)
            {
                // Nothing loaded, so nothing to draw it with. The card is not even up yet.
                return;
            }

            // The card is drawn from both: the amount is the request's, the currency and rate are
            // the wallet's, and a rate arriving after this opened is what fills in the second line.
            Card.SetTransfer(_clientService, state, _request.Recipient, _request.Nanograms);

            UpdateFee(state);
        }

        /// <summary>
        /// The fee line, which belongs to neither page and sits under both.
        /// </summary>
        /// <remarks>
        /// Said in grams first because that is what is actually charged, with the conversion in
        /// brackets after it. Without a rate the bracket is dropped rather than filled with a
        /// number in a currency nothing was converted into.
        /// </remarks>
        private void UpdateFee(WalletState state)
        {
            var grams = Formatter.TonBalance(_request.FeeNanograms).Join();

            FeeLabel.Text = WalletHelper.TryToCurrency(_clientService, state, _request.FeeNanograms, out var converted)
                ? string.Format(Strings.WalletFeeAmountWithCurrency, grams, Formatter.FormatAmountExact(converted, WalletHelper.CurrencyDecimals, state?.Currency ?? "USD"))
                : string.Format(Strings.WalletFeeAmount, grams);
        }

        /// <summary>
        /// Moves between the two pages, which is a change of header as much as of body.
        /// </summary>
        private void UpdateDetails()
        {
            var loaded = _request != null;

            ReviewLabel.Visibility = loaded ? Visibility.Collapsed : Visibility.Visible;
            FeeLabel.Visibility = loaded ? Visibility.Visible : Visibility.Collapsed;

            SummaryPage.Visibility = loaded && !_details ? Visibility.Visible : Visibility.Collapsed;
            DetailsPage.Visibility = loaded && _details ? Visibility.Visible : Visibility.Collapsed;

            // Review until there is something to confirm. The same button, because it is the same
            // question asked twice: first whether to look, then whether to agree.
            PrimaryButtonText = loaded ? Strings.WalletConfirm : Strings.WalletReview;

            // The identity shrinks out of the way on the second page: it has said what it has to
            // say, and what matters there is the list under it.
            Photo.Size = _details ? 36 : 96;
            Photo.Margin = new Thickness(0, _details ? 0 : 4, 0, _details ? 0 : 12);

            Domain.Text = loaded ? _request.Domain : _domain ?? string.Empty;

            // Back on the second page, dismiss on the first. The same button, because they are the
            // same gesture: undo the last thing that happened.
            BackButton.Visibility = _details
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdatePreview()
        {
            // Nothing to second-guess the request with. The transfer is still exactly what it says,
            // so the section goes rather than standing empty.
            var any = _request.Actions.Count > 0;

            PreviewHeader.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
            PreviewRoot.Visibility = any ? Visibility.Visible : Visibility.Collapsed;

            PreviewPanel.ItemsSource = _request.Actions;
        }

        /// <summary>
        /// Cancel, which is a decision the dApp is told about rather than a way out of the sheet.
        /// </summary>
        /// <remarks>
        /// Only once there is something to decline. Before that the sheet is showing a name and an
        /// offer to look, and closing it leaves the request where it was - still answerable, here
        /// or on another device.
        /// </remarks>
        private void OnSecondaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            if (_request != null)
            {
                AnswerAsync(args, false);
            }
        }

        /// <summary>
        /// Answers the dApp, whichever way the user chose.
        /// </summary>
        /// <remarks>
        /// Both buttons come here, because both are answers and both are claimed the same way. The
        /// sheet closes either way: a claim that did not win means another device has answered it,
        /// and there is nothing left to decide.
        /// </remarks>
        private async void AnswerAsync(ModalPopupButtonClickEventArgs args, bool accept)
        {
            var deferral = args.GetDeferral();

            args.Cancel = true;
            IsPrimaryButtonPending = true;

            string refusal = null;
            var declined = false;

            try
            {
                await _wallet.AnswerRequestAsync(_request, accept, _lease);
            }
            catch (WalletAccessDeniedException)
            {
                // Declined before anything was claimed, so the request is still open to answer.
                declined = true;
            }
            catch (WalletRotationPendingException)
            {
                refusal = Strings.WalletSecretPhraseUpdating;
            }
            catch (WalletTransferInProgressException)
            {
                refusal = Strings.WalletTransferInProgress;
            }
            catch (Exception ex)
            {
                Logger.Error("ton connect request could not be answered: " + ex.Message);
            }
            finally
            {
                IsPrimaryButtonPending = false;
                deferral.Complete();
            }

            if (declined)
            {
                return;
            }
            else if (refusal != null)
            {
                // Unclaimed, and it passes on its own: the sheet stays so the user can try again
                // or decline.
                _ = MessagePopup.ShowNestedAsync(XamlRoot, refusal, primary: Strings.OK);
                return;
            }

            Hide();
        }

        /// <summary>
        /// The vault goes back the moment the sheet does, however it was closed.
        /// </summary>
        /// <remarks>
        /// A lease held open is the vault gate held shut, so anything else that needs the key
        /// waits behind this sheet - which is right while it is up, and a hang if it outlived it.
        /// </remarks>
        protected override void OnUnloaded()
        {
            _lease?.Dispose();
            _lease = null;

            base.OnUnloaded();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_details)
            {
                _details = false;
                UpdateDetails();
            }
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            _details = true;
            UpdateDetails();
        }

        private void Domain_Click(object sender, RoutedEventArgs e)
        {
            // The domain is the dApp's real identity, so it is worth being able to look at rather
            // than only read.
            if (!string.IsNullOrEmpty(_domain))
            {
                MessageHelper.OpenUrl(_clientService, _navigationService, "https://" + _domain);
            }
        }
    }
}
