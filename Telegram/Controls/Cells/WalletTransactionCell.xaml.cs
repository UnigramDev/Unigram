//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Wallet;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
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
            var onRampDeposit = transaction.Type as TonWalletTransactionTypeOnRampDeposit;
            var nftTransfer = transaction.Type as TonWalletTransactionTypeNftTransfer;

            UpdatePeer(clientService, transaction);
            UpdateNft(clientService, nftTransfer);

            if (transfer != null)
            {
                UpdateAmount(transfer.Amount);
            }
            else if (onRampDeposit != null)
            {
                UpdateAmount(onRampDeposit.Amount);
            }

            if (transaction.State is TonWalletTransactionStatePending)
            {
                // Also ahead of the direction: nothing has been sent until the chain says so.
                Subtitle.Text = Strings.WalletSending;
            }
            else if (transaction.State is TonWalletTransactionStateFailed)
            {
                // It supersedes the direction: a transfer that never landed was neither sent nor
                // received.
                Subtitle.Text = Strings.WalletFailedTransfer;
            }
            else if (transfer != null)
            {
                Subtitle.Text = transfer.Amount < 0 ? Strings.WalletOutgoingTransfer : Strings.WalletIncomingTransfer;
            }
            else if (onRampDeposit != null)
            {
                Subtitle.Text = onRampDeposit.ProviderName;
            }
            else if (nftTransfer != null)
            {
                Subtitle.Text = nftTransfer.IsOutgoing ? Strings.WalletOutgoingTransferNft : Strings.WalletIncomingTransferNft;
            }
            else
            {
                Subtitle.Text = Shorten(transaction.PeerAddress);
            }

            Date.Text = Formatter.DateAt(transaction.Date);
        }

        private void UpdatePeer(IClientService clientService, TonWalletTransaction transaction)
        {
            if (transaction.Type is TonWalletTransactionTypeKeyChange)
            {
                Title.Text = Strings.WalletKeyUpdate;
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.KeyFilled, long.MinValue);
            }
            else if (transaction.Type is TonWalletTransactionTypeOnRampDeposit)
            {
                Title.Text = Strings.WalletTransactionTopUp;
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.PaymentFilled);
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

                if (transaction.Type is TonWalletTransactionTypeTransfer transfer)
                {
                    Photo.Source = ProfilePictureSourceText.GetGlyph(transfer.Amount < 0 ? Icons.ArrowCircleUpFilled : Icons.ArrowCircleDownFilled, transfer.Amount < 0 ? 5 : 3);
                }
                else if (transaction.Type is TonWalletTransactionTypeNftTransfer nftTransfer)
                {
                    Photo.Source = ProfilePictureSourceText.GetGlyph(nftTransfer.IsOutgoing ? Icons.ArrowCircleUpFilled : Icons.ArrowCircleDownFilled, nftTransfer.IsOutgoing ? 5 : 3);
                }
            }
        }

        // Where the send screen's stone lands. GlyphBounds only means something once a layout pass
        // has finished after the row was bound.
        internal FrameworkElement Anchor => Amount;

        internal TextBlock AmountText => Amount;

        internal ProfilePicture PhotoElement => Photo;

        // How the row looks while its transfer is under way. Made the first time it shows one.
        internal WalletPendingRow PendingRow { get; set; }

        // The glyph's own brush while the send screen's stone covers it. A Run has no opacity, so
        // hiding it means swapping its brush, and the markup's is put back rather than cleared -
        // clearing would leave it inheriting the amount's.
        private Brush _glyphForeground;
        private Brush _glyphHidden;

        internal void HideGlyph(bool hidden)
        {
            if (hidden && _glyphForeground == null)
            {
                _glyphForeground = AmountGlyph.Foreground;
                AmountGlyph.Foreground = _glyphHidden ??= new SolidColorBrush(Windows.UI.Colors.Transparent);
            }
            else if (!hidden && _glyphForeground != null)
            {
                AmountGlyph.Foreground = _glyphForeground;
                _glyphForeground = null;
            }
        }

        /// <summary>
        /// The TON glyph beside the amount, in window coordinates and as it is drawn - a raised
        /// row draws its amount larger than layout puts it.
        /// </summary>
        internal Rect GlyphBounds(out double fontSize)
        {
            var start = AmountGlyph.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            var end = AmountGlyph.ContentEnd.GetCharacterRect(LogicalDirection.Backward);

            // Through the transform rather than offset from the origin: a raised row's scales are
            // RenderTransforms, and they apply to the glyph's own position as much as to the
            // amount's.
            var local = new Rect(start.X, start.Y, Math.Max(0, end.X - start.X), start.Height);
            var bounds = Amount.TransformToVisual(null).TransformBounds(local);
            fontSize = Amount.FontSize;

            return PendingRow?.Project(bounds, ref fontSize) ?? bounds;
        }

        private void UpdateAmount(long value)
        {
            if (value == 0)
            {
                // A key rotation moves nothing, and an amount of zero would read as a transfer
                // that failed.
                AmountInteger.Text = string.Empty;
                AmountFraction.Text = string.Empty;
                return;
            }

            // TDLib signs the amount rather than naming a direction: negative is outgoing.
            var outgoing = value < 0;
            var amount = Formatter.TonBalance(Math.Abs(value));

            AmountInteger.Text = (outgoing ? "-" : "+") + amount.Integer;
            AmountFraction.Text = amount.Fraction;
            AmountGlyph.Text = Icons.Ton;

            if (outgoing || _received == null)
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

        private void UpdateNft(IClientService clientService, TonWalletTransactionTypeNftTransfer transfer)
        {
            if (transfer == null)
            {
                CollectibleRoot.Visibility = Visibility.Collapsed;
                return;
            }

            if (transfer.Nft.Image != null)
            {
                CollectiblePhoto.Source = new ProfilePictureSourcePhoto(clientService, transfer.Nft.Image.Photo.Id, transfer.Nft.Image.Photo, null, Shape: ProfilePictureShape.Superellipse);
                CollectiblePhoto.Visibility = Visibility.Visible;
            }
            else
            {
                CollectiblePhoto.Source = null;
                CollectiblePhoto.Visibility = Visibility.Collapsed;
            }

            CollectibleName.Text = transfer.Nft.Name;
            CollectibleInfo.Text = Strings.WalletCollectibleGift;
            CollectibleRoot.Visibility = Visibility.Visible;

            var outgoing = transfer.IsOutgoing;

            AmountInteger.Text = string.Format("{0}{1} {2}", outgoing ? "-" : "+", Locale.Declension(Strings.R.items, 1), Icons.Gift14);
            AmountFraction.Text = string.Empty;
            AmountGlyph.Text = string.Empty;

            if (outgoing || _received == null)
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
