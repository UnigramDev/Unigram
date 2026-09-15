//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    /// <summary>
    /// One row of wallet history: who, what, when, and how much. Filled from
    /// <c>ContainerContentChanging</c> like the other cells, so a recycled row costs a few property
    /// writes and no bindings.
    /// </summary>
    public sealed partial class WalletTransactionCell : Grid
    {
        // TON blue, for the rows that have no Telegram user to show a photo of.
        private static readonly Color TonColor = Color.FromArgb(0xFF, 0x00, 0x79, 0xFF);

        private readonly Brush _received;

        public WalletTransactionCell()
        {
            InitializeComponent();

            // Read once: this runs while the list scrolls, and a resource lookup walks the tree.
            // Safe to hold, being made of a colour that follows the theme - unlike the brush the
            // amount is drawn in otherwise, which the theme replaces rather than repaints.
            _received = Resources["AmountReceivedBrush"] as Brush;
        }

        public void UpdateInfo(IClientService clientService, TonWalletTransaction transaction)
        {
            var transfer = transaction.Type as TonWalletTransactionTypeTransfer;

            UpdatePeer(clientService, transaction, transfer);
            UpdateAmount(transfer);

            if (transaction.State is TonWalletTransactionStatePending)
            {
                // Also ahead of the direction: nothing has been sent until the chain says so.
                Subtitle.Text = "[Pending]";
                Subtitle.Visibility = Visibility.Visible;
            }
            else if (transaction.State is TonWalletTransactionStateFailed)
            {
                // It supersedes the direction: a transfer that never landed was neither sent nor
                // received.
                Subtitle.Text = "[Failed]";
                Subtitle.Visibility = Visibility.Visible;
            }
            else if (transfer != null)
            {
                Subtitle.Text = transfer.Amount < 0 ? "[Sent]" : "[Received]";
                Subtitle.Visibility = Visibility.Visible;
            }
            else
            {
                // A key rotation has nothing to say here that the title has not said already.
                Subtitle.Visibility = Visibility.Collapsed;
            }

            Date.Text = Formatter.DateAt(transaction.Date);
        }

        private void UpdatePeer(IClientService clientService, TonWalletTransaction transaction, TonWalletTransactionTypeTransfer transfer)
        {
            if (transfer == null)
            {
                Title.Text = "[Key Rotation]";
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.KeyFilled, long.MinValue);
            }
            else if (transaction.PeerUserId != 0 && clientService.TryGetUser(transaction.PeerUserId, out User user))
            {
                Title.Text = user.FullName();
                Photo.Source = ProfilePictureSource.User(clientService, user);
            }
            else
            {
                // Nobody the account knows: a domain if the address has one, the address otherwise,
                // and the chain's own mark instead of a photo.
                Title.Text = transaction.PeerDomain.Length > 0
                    ? transaction.PeerDomain
                    : Shorten(transaction.PeerAddress);

                Photo.Source = ProfilePictureSourceText.GetGlyph(transfer.Amount < 0 ? Icons.ArrowCircleUpFilled : Icons.ArrowCircleDownFilled, 3);
            }
        }

        private void UpdateAmount(TonWalletTransactionTypeTransfer transfer)
        {
            if (transfer == null)
            {
                // A key rotation moves nothing, and an amount of zero would read as a transfer
                // that failed.
                AmountInteger.Text = string.Empty;
                AmountFraction.Text = string.Empty;
                return;
            }

            // TDLib signs the amount rather than naming a direction: negative is outgoing.
            var sent = transfer.Amount < 0;
            var amount = Formatter.TonBalance(Math.Abs(transfer.Amount));

            AmountInteger.Text = (sent ? "-" : "+") + amount.Integer;
            AmountFraction.Text = amount.Fraction;

            if (sent || _received == null)
            {
                // Back to the colour the style gives it. Only a local value is cleared, so the
                // amount must never be given a Foreground in the markup - that is a local value
                // too, and this would take it away for good.
                Amount.ClearValue(TextBlock.ForegroundProperty);
            }
            else
            {
                Amount.Foreground = _received;
            }
        }

        /// <summary>
        /// An address is 48 characters and no row is that wide; the ends are what a reader compares.
        /// </summary>
        private static string Shorten(string address)
        {
            if (address.Length <= 8)
            {
                return address;
            }

            return address.Substring(0, 4) + "…" + address.Substring(address.Length - 4);
        }
    }
}
