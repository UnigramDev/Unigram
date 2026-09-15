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
    public sealed partial class WalletTransactionPopup : ModalPopup
    {
        private readonly IClientService _clientService;
        private readonly IWalletService _wallet;
        private readonly INavigationService _navigationService;
        private readonly TonWalletTransaction _transaction;

        public WalletTransactionPopup(IClientService clientService, IWalletService wallet, INavigationService navigationService, TonWalletTransaction transaction)
        {
            InitializeComponent();

            _clientService = clientService;
            _wallet = wallet;
            _navigationService = navigationService;
            _transaction = transaction;

            UpdateTransaction(_wallet.State, transaction);
        }

        private void UpdateTransaction(WalletState state, TonWalletTransaction transaction)
        {
            var transfer = transaction.Type as TonWalletTransactionTypeTransfer;
            var sent = transfer != null && transfer.Amount < 0;

            if (transfer != null)
            {
                var amount = Formatter.TonBalance(Math.Abs(transfer.Amount));

                Amount.Text = (sent ? "-" : "+") + amount.Integer + amount.Fraction;
                Converted.Text = Convert(state, Math.Abs(transfer.Amount));
            }
            else
            {
                // A key rotation moves nothing; what it cost is in the fee row below.
                Amount.Text = "[Key Rotation]";
                Converted.Text = string.Empty;
            }

            UpdatePeer(transaction, transfer, sent);
            UpdateAddress(transaction.PeerAddress);
            UpdateComment(transfer);

            UpdateFee(state, Fees(transaction, transfer));

            DateRow.Content = Formatter.DateAt(transaction.Date);
        }

        /// <summary>
        /// The fee, with the diamond rather than a sign: it is the wallet's own cost whichever way
        /// the transfer went.
        /// </summary>
        private void UpdateFee(WalletState state, long fees)
        {
            if (fees <= 0)
            {
                FeeRow.Visibility = Visibility.Collapsed;
                return;
            }

            // Under one TON the formatter shows every digit, so a fee of a few thousandths comes
            // out as itself rather than as a zero.
            var fee = Formatter.TonBalance(fees);

            FeeGlyph.Text = Icons.Ton;
            FeeAmount.Text = string.Format(" {0}{1}", fee.Integer, fee.Fraction);

            // The converted fee down to the digit that carries it: a fee is a fraction of a cent
            // more often than not, and rounded to the currency's own precision every one of them
            // would read the same zero.
            var converted = ToCurrency(state, fees);

            FeeConverted.Text = converted > 0
                ? string.Format(" ~ {0}", Formatter.FormatAmountExact(converted, state?.Currency ?? "USD"))
                : string.Empty;
        }

        private void UpdatePeer(TonWalletTransaction transaction, TonWalletTransactionTypeTransfer transfer, bool sent)
        {
            if (transfer == null)
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
                PeerRow.Visibility = Visibility.Collapsed;

                // The pill goes where the other side still is, which is the address.
                AddressSendCommand.Visibility = Visibility.Visible;
                return;
            }

            PeerRow.Header = sent ? "[Recipient]" : "[Sender]";
            PeerTitle.Text = user.FullName();
            PeerPhoto.Source = ProfilePictureSource.User(_clientService, user);
        }

        // What stands in for an encrypted comment until it is decrypted. The characters are never
        // seen - the spoiler covers them - so only the width matters, and one line of them is what
        // a comment usually is.
        private const string CommentPlaceholder = "encrypted comment placeholder";

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
                Comment.SetText(_clientService, new FormattedText(transfer.Comment, Array.Empty<TextEntity>()));
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
            if (!await WalletHelper.EnsureBoundAsync(_wallet, _navigationService))
            {
                return;
            }

            try
            {
                // The encrypted body is what TDLib puts in comment: is_comment_encrypted says it
                // has to be decrypted with the user's key rather than shown.
                var comment = await _wallet.DecryptCommentAsync(_transaction, transfer.Comment);
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
            return Formatter.FormatAmountExact(ToCurrency(state, nanograms), state?.Currency ?? "USD");
        }

        private double ToCurrency(WalletState state, BigInteger nanograms)
        {
            return WalletHelper.ToCurrency(_clientService, state, nanograms);
        }

        private static long Fees(TonWalletTransaction transaction, TonWalletTransactionTypeTransfer transfer)
        {
            if (transfer != null)
            {
                // Only what this wallet paid. The fee on an incoming transfer was the sender's, and
                // is reported here all the same.
                return transfer.Amount < 0 ? transfer.FeeAmount : 0;
            }
            else if (transaction.Type is TonWalletTransactionTypeKeyChange change)
            {
                return change.FeeAmount;
            }

            return 0;
        }

        private void Peer_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            if (_transaction.PeerUserId != 0)
            {
                Hide();
                _navigationService.NavigateToUser(_transaction.PeerUserId);
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
            if (_transaction.State is not TonWalletTransactionStatePending)
            {
                flyout.CreateFlyoutItem(MenuItemExplorer, "[View in Explorer]", Icons.Globe);
            }

            flyout.CreateFlyoutItem(MenuItemAbout, "[What is Gram?]", Icons.QuestionCircle);

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void MenuItemExplorer()
        {
            MessageHelper.OpenUrl(null, null, "https://tonviewer.com/transaction/" + _transaction.Id);
        }

        private void MenuItemAbout()
        {
            _navigationService.ShowPopup(new WalletAboutPopup());
        }
    }
}
