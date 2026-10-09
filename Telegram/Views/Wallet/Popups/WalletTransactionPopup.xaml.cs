//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Threading.Tasks;
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
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;

namespace Telegram.Views.Wallet.Popups
{
    /// <summary>
    /// One transaction, in full.
    /// </summary>
    /// <remarks>
    /// Shaped after the Stars receipt, without the picture above the amount: a transfer has
    /// nothing to show but its own numbers.
    /// </remarks>
    public sealed partial class WalletTransactionPopup : WalletPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        // Not readonly: the row this was opened with is replaced when it settles - see Handle.
        private TonWalletTransaction _transaction;

        private Task<BigInteger?> _fee;
        private bool _feeInfoPending;

        /// <param name="standalone">
        /// Whether the wallet is not already on screen behind this - opened from a chat rather than
        /// from the wallet window, which is the only case with somewhere to go from here.
        /// </param>
        public WalletTransactionPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, TonWalletTransaction transaction, bool standalone = false)
            : base(wallet, navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _transaction = transaction;

            PrimaryButtonText = standalone ? Strings.WalletOpenMyWallet : Strings.OK;

            UpdateTransaction(State, transaction);
        }

        private void OnPrimaryButtonClick(ModalPopup sender, ModalPopupButtonClickEventArgs args)
        {
            // The receipt is done with either way: what it has to say about the transfer is on it,
            // and the wallet is where the rest of them are.
            Hide();

            _navigationService.NavigateToWallet();
        }

        /// <summary>
        /// Follows the transaction rather than the copy the popup was opened with, and re-prices it
        /// when the rate it is converted at moves.
        /// </summary>
        /// <remarks>
        /// A pending transfer is a row this device wrote, and it is replaced without anything here
        /// asking - by the transaction it became, or by a failure. Matched by id, because what
        /// replaces it is a different object.
        ///
        /// A row that disappears from the history entirely leaves the popup showing what it last
        /// had, which is the right answer: it is still a true account of what was there, and the
        /// alternative is a popup that empties itself while being read.
        /// </remarks>
        protected override void UpdateWalletState(WalletState state)
        {
            foreach (var item in state.Activity)
            {
                if (string.Equals(item.Id, _transaction.Id, StringComparison.Ordinal))
                {
                    _transaction = item;
                    break;
                }
            }

            UpdateTransaction(state, _transaction);
        }

        private void UpdateTransaction(WalletState state, TonWalletTransaction transaction)
        {
            var transfer = transaction.Type as TonWalletTransactionTypeTransfer;
            var onRampDeposit = transaction.Type as TonWalletTransactionTypeOnRampDeposit;
            var nftTransfer = transaction.Type as TonWalletTransactionTypeNftTransfer;

            var outgoing = (transfer != null && transfer.Amount < 0) || (nftTransfer != null && nftTransfer.IsOutgoing);
            var amountValue = transfer?.Amount ?? onRampDeposit?.Amount ?? 0;

            if (amountValue != 0)
            {
                var amount = Formatter.GramExact(Math.Abs(amountValue));

                Amount.Text = (outgoing ? "-" : "+") + amount.Integer + amount.Fraction;
                Converted.Text = Convert(state, Math.Abs(amountValue));
            }
            else if (nftTransfer != null)
            {
                Amount.Text = nftTransfer.Nft.Name;
                Converted.Text = string.Empty;
            }
            else
            {
                // A key rotation moves nothing; what it cost is in the fee row below.
                Amount.Text = Strings.WalletKeyUpdate;
                Converted.Text = string.Empty;
            }

            if (nftTransfer?.Nft.Image != null)
            {
                HeaderPhoto.Source = new ProfilePictureSourcePhoto(_clientService, nftTransfer.Nft.Image.Photo.Id, nftTransfer.Nft.Image.Photo, null, Shape: ProfilePictureShape.Superellipse);
                HeaderPhoto.Visibility = Visibility.Visible;

                HeaderAnimated.Visibility = Visibility.Collapsed;
            }
            else if (transaction.Type is TonWalletTransactionTypeKeyChange)
            {
                HeaderPhoto.Source = ProfilePictureSourceText.GetGlyph(Icons.KeyFilled, long.MinValue);
                HeaderPhoto.Visibility = Visibility.Visible;

                HeaderAnimated.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeaderPhoto.Source = null;
                HeaderPhoto.Visibility = Visibility.Collapsed;

                HeaderAnimated.Visibility = Visibility.Visible;
            }

            UpdatePeer(transaction, transfer, nftTransfer, outgoing);
            UpdateAddress(transaction.PeerAddress);
            UpdateComment(transfer);

            UpdateFee(state, transaction.FeeAmount, transfer is { IsGasless: true });

            DateRow.Content = Formatter.DateAt(transaction.Date);
        }

        /// <summary>
        /// The fee, with the diamond rather than a sign: it is the wallet's own cost whichever way
        /// the transfer went.
        /// </summary>
        private void UpdateFee(WalletState state, long fees, bool gasless)
        {
            if (gasless)
            {
                // The row stays, and what it says is the point: this transfer cost the user
                // nothing, which is not the same as it having had no fee.
                FeeRow.Visibility = Visibility.Visible;
                FeeInfoCommand.Visibility = Visibility.Visible;

                FeeGlyph.Text = Icons.Ton;
                FeeAmount.Text = Strings.WalletFeePaidByTelegram;
                FeeConverted.Text = string.Empty;
                return;
            }

            FeeInfoCommand.Visibility = Visibility.Collapsed;

            if (fees <= 0)
            {
                FeeRow.Visibility = Visibility.Collapsed;
                return;
            }

            // Under one TON the formatter shows every digit, so a fee of a few thousandths comes
            // out as itself rather than as a zero.
            var fee = Formatter.GramExact(fees);

            FeeGlyph.Text = Icons.Ton;
            FeeAmount.Text = string.Format(" {0}{1}", fee.Integer, fee.Fraction);

            // The converted fee down to the digit that carries it: a fee is a fraction of a cent
            // more often than not, and rounded to the currency's own precision every one of them
            // would read the same zero. Nothing at all while the rates are still coming.
            FeeConverted.Text = WalletHelper.TryToCurrency(_clientService, state, fees, out var converted) && converted > BigInteger.Zero
                ? string.Format(" ~ {0}", Formatter.FormatAmountExact(converted, WalletHelper.CurrencyDecimals, state?.Currency ?? "USD"))
                : string.Empty;
        }

        private void UpdatePeer(TonWalletTransaction transaction, TonWalletTransactionTypeTransfer transfer, TonWalletTransactionTypeNftTransfer nftTransfer, bool sent)
        {
            if (transfer == null && nftTransfer == null)
            {
                // A key rotation has no other side, and nothing to send to.
                PeerRow.Visibility = Visibility.Collapsed;
                AddressRow.Visibility = Visibility.Collapsed;
                return;
            }

            // Only a Telegram user earns the row. Without one it would be an address beside a
            // placeholder, which is what the address row below already is.
            if (transaction.PeerUserId == 0 || !_clientService.TryGetUser(transaction.PeerUserId, out User user))
            {
                if (string.IsNullOrEmpty(transaction.PeerDomain))
                {
                    PeerRow.Visibility = Visibility.Collapsed;

                    // The pill goes where the other side still is, which is the address.
                    AddressSendCommand.Visibility = Visibility.Visible;
                }
                else
                {
                    PeerRow.Header = sent ? Strings.WalletRecipient : Strings.WalletSender;
                    PeerTitle.Text = transaction.PeerDomain;
                    PeerPhoto.Visibility = Visibility.Collapsed;
                }

                return;
            }

            PeerRow.Header = sent ? Strings.WalletRecipient : Strings.WalletSender;
            PeerTitle.Text = user.FullName();
            PeerPhoto.Source = ProfilePictureSource.User(_clientService, user);
        }

        // What stands in for an encrypted comment until it is decrypted. The characters are never
        // seen - the spoiler covers them - so only the width matters, and one line of them is what
        // a comment usually is.
        private const string CommentPlaceholder = "encrypted comment";

        // The link the placeholder carries. Never opened - the handler answers it - so it only has
        // to be something no real comment would contain.
        private const string DecryptCommand = "tg://wallet?decrypt";

        private void UpdateComment(TonWalletTransactionTypeTransfer transfer)
        {
            if (transfer == null || (transfer.Comment.Length == 0 && !transfer.IsCommentEncrypted))
            {
                CommentRoot.Visibility = Visibility.Collapsed;
                return;
            }

            CommentRoot.Visibility = Visibility.Visible;

            if (transfer.IsCommentEncrypted)
            {
                ShowPlaceholder();
            }
            else
            {
                Comment.SetText(_clientService, transfer.Comment.AsFormattedText());
            }
        }

        private bool _revealed;

        private void ShowPlaceholder()
        {
            // Two entities over the same run: the spoiler is what it looks like, and the link is
            // what makes it operable - a hyperlink takes focus, answers Enter, and is what raises
            // TextEntityClick.
            var entities = new TextEntity[]
            {
                new TextEntity(0, CommentPlaceholder.Length, new TextEntityTypeSpoiler()),
                new TextEntity(0, CommentPlaceholder.Length, new TextEntityTypeTextUrl(DecryptCommand))
            };

            Comment.SetText(_clientService, new FormattedText(CommentPlaceholder, entities));
        }

        /// <summary>
        /// Reveals an encrypted comment, which means decrypting it: the spoiler hides a
        /// placeholder, so uncovering it would show nothing worth seeing.
        /// </summary>
        private async void Comment_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            // Nothing else is going to open this: it is a command, not an address.
            e.Handled = true;

            if (_transaction.Type is not TonWalletTransactionTypeTransfer transfer || !transfer.IsCommentEncrypted || _revealed)
            {
                return;
            }

            // Decrypting needs the signing key, and this may be the first thing on this device to
            // ask for one.
            using var lease = _wallet.CreateLease(_navigationService);

            if (!await WalletHelper.EnsureBoundAsync(_clientService, _wallet, _navigationService, lease))
            {
                return;
            }

            try
            {
                // The encrypted body is what TDLib puts in comment: is_comment_encrypted says it
                // has to be decrypted with the user's key rather than shown.
                var comment = await _wallet.DecryptCommentAsync(_transaction, transfer.Comment, lease);
                if (comment != null)
                {
                    _revealed = true;
                    Comment.SetText(_clientService, new FormattedText(comment, Array.Empty<TextEntity>()));

                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("wallet comment could not be decrypted: " + ex.Message);
            }

            // The placeholder goes back: a comment that cannot be read is still there.
            ShowPlaceholder();
        }

        /// <summary>
        /// Fills the address into the groups the markup declares.
        /// </summary>
        /// <remarks>
        /// Text only: which groups are dimmed is decided in the markup, where the brush is the
        /// theme's own and follows it. A shorter address than the twelve groups leaves the rest
        /// empty rather than showing what was there before.
        /// </remarks>
        private void UpdateAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                AddressRow.Visibility = Visibility.Collapsed;
                return;
            }

            var groups = new[]
            {
                AddressGroup0, AddressGroup1, AddressGroup2, AddressGroup3,
                AddressGroup4, AddressGroup5, AddressGroup6, AddressGroup7,
                AddressGroup8, AddressGroup9, AddressGroup10, AddressGroup11
            };

            for (int i = 0; i < groups.Length; i++)
            {
                var offset = i * 4;

                groups[i].Text = offset < address.Length
                    ? address.Substring(offset, Math.Min(4, address.Length - offset))
                    : string.Empty;
            }
        }

        private string Convert(WalletState state, BigInteger nanograms)
        {
            return WalletHelper.TryToCurrency(_clientService, state, nanograms, out var amount)
                ? Formatter.FormatAmountExact(amount, WalletHelper.CurrencyDecimals, state?.Currency ?? "USD")
                : string.Empty;
        }

        private async void FeeInfo_Click(object sender, RoutedEventArgs e)
        {
            if (_transaction.Type is not TonWalletTransactionTypeTransfer transfer || _feeInfoPending)
            {
                return;
            }

            // A click while the estimate is out would put up a second explanation beside the first.
            _feeInfoPending = true;

            // What this transfer would have cost had Telegram not paid for it. Emulated, so nothing
            // is signed and nothing asks for the key.
            _fee ??= _wallet.EstimateFeeAsync(_transaction.PeerAddress, BigInteger.Abs(transfer.Amount), transfer.Comment, !transfer.IsCommentEncrypted);

            var fee = await _fee;
            if (fee == null)
            {
                // Not kept: the next click should be another try.
                _fee = null;
            }

            _feeInfoPending = false;

            var text = fee != null
                ? Formatter.Grams(fee.Value)
                : Strings.WalletUnknown;

            _ = MessagePopup.ShowNestedAsync(XamlRoot, string.Format(Strings.WalletNetworkFeeInfo, text), Strings.WalletNetworkFee, Strings.OK);
        }

        private void Peer_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (_transaction.PeerUserId != 0)
            {
                Hide();
                _navigationService.NavigateToUser(_transaction.PeerUserId);
            }
            else
            {
                Send_Click(null, null);
            }
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            // This one is done: what it had to say about the transfer has been read, and the other
            // side of it is the only thing carried over.
            Hide();

            var popup = new WalletSendPopup(_clientService, _wallet, _navigationService, _transaction.PeerUserId, _transaction.PeerAddress, _transaction.PeerDomain);

            await _navigationService.ShowPopupAsync(popup);
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            // Nothing to look up until it has landed: what a pending row is keyed by is the
            // message that was sent, and the explorer knows about transactions.
            if (_transaction.State is TonWalletTransactionStateSucceeded)
            {
                flyout.CreateFlyoutItem(MenuItemExplorer, Strings.WalletViewInExplorer, Icons.Globe);
            }

            flyout.CreateFlyoutItem(MenuItemAbout, Strings.WalletWhatIsGram, Icons.QuestionCircle);

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void MenuItemExplorer()
        {
            if (_transaction.State is TonWalletTransactionStateSucceeded succeeded)
            {
                MessageHelper.OpenUrl(null, null, _clientService.Options.TonBlockchainExplorerUrl + "transaction/" + succeeded.TxHash);
            }
        }

        private void MenuItemAbout()
        {
            _navigationService.ShowPopup(new WalletAboutPopup());
        }
    }
}
