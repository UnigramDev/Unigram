//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Grams.Popups
{
    public sealed partial class TonReceiptPopup : ContentPopup
    {
        private readonly string _transactionId;

        // Set only for a settled withdrawal, where the primary button opens the transaction
        // on the blockchain instead of dismissing.
        private string _explorerUrl;

        public TonReceiptPopup(IClientService clientService, TonTransaction transaction)
        {
            InitializeComponent();

            _transactionId = transaction.Id;

            var info = TransactionInfo.FromTonTransaction(clientService, transaction);

            Title.Text = info.Heading;

            Photo.Visibility = Visibility.Collapsed;
            AnimatedPhoto.Visibility = Visibility.Collapsed;
            Subtitle.Visibility = Visibility.Collapsed;

            From.Header = info.Header;
            FromPhoto.Source = info.Photo;
            FromPhoto.Visibility = info.HasSender
                ? Visibility.Visible
                : Visibility.Collapsed;
            FromTitle.Text = info.HasSender
                ? info.Title
                : GetSourceName(transaction.Type, info.Title);

            if (info.HasSender)
            {
                Photo.Source = info.Photo;
                Photo.Visibility = Visibility.Visible;
            }

            UpdateType(clientService, transaction);

            if (string.IsNullOrEmpty(transaction.Id))
            {
                Transaction.Visibility = Visibility.Collapsed;
            }

            Identifier.Text = transaction.Id;
            Date.Content = Formatter.DateAt(transaction.Date);

            var (integer, fraction) = Formatter.TonBalance(transaction.GramAmount);

            Symbol.Text = transaction.GramAmount < 0 ? string.Empty : "+";
            Amount.Text = integer;
            Decimal.Text = fraction;

            Value.Foreground = BootStrapper.Current.Resources[transaction.GramAmount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;

            Refund.Visibility = transaction.IsRefund
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // Fragment is the only named source a Gram transaction can have; everything else
        // without a counterparty falls back to its own heading.
        private static string GetSourceName(TonTransactionType type, string fallback)
        {
            return type switch
            {
                TonTransactionTypeFragmentDeposit or TonTransactionTypeFragmentWithdrawal => Strings.Fragment,
                _ => fallback
            };
        }

        private void UpdateType(IClientService clientService, TonTransaction transaction)
        {
            switch (transaction.Type)
            {
                case TonTransactionTypeFragmentDeposit fragmentDeposit:
                    SetAnimated(clientService, fragmentDeposit.Sticker);
                    break;

                case TonTransactionTypeFragmentWithdrawal fragmentWithdrawal:
                    if (fragmentWithdrawal.WithdrawalState is RevenueWithdrawalStateSucceeded succeeded)
                    {
                        SetRow(TonDate, Formatter.DateAt(succeeded.Date));

                        _explorerUrl = succeeded.Url;
                        PurchaseCommand.Content = Strings.StarsTransactionViewInBlockchainExplorer;
                    }
                    break;

                case TonTransactionTypeUpgradedGiftPurchase upgradedGiftPurchase:
                    SetUpgradedGift(clientService, upgradedGiftPurchase.Gift, transaction.IsRefund
                        ? Strings.StarGiftReasonSale
                        : Strings.StarGiftReasonPurchase);
                    break;

                case TonTransactionTypeUpgradedGiftSale upgradedGiftSale:
                    SetUpgradedGift(clientService, upgradedGiftSale.Gift, transaction.IsRefund
                        ? upgradedGiftSale.ViaOffer
                            ? Strings.StarGiftReasonOfferRefund
                            : Strings.StarGiftReasonPurchase
                        : Strings.StarGiftReasonSale);
                    SetFullPrice(transaction.GramAmount, upgradedGiftSale.CommissionGramAmount);
                    break;

                case TonTransactionTypeGiftPurchaseOffer giftPurchaseOffer:
                    SetUpgradedGift(clientService, giftPurchaseOffer.Gift, transaction.IsRefund
                        ? Strings.StarGiftReasonSale
                        : Strings.StarGiftReasonOffer);
                    break;
            }
        }

        private static void SetRow(TableViewItem row, string content)
        {
            row.Content = content;
            row.Visibility = Visibility.Visible;
        }

        private void SetAnimated(IClientService clientService, Sticker sticker)
        {
            var source = DelayedFileSource.FromSticker(clientService, sticker);
            if (source == null)
            {
                return;
            }

            AnimatedPhoto.Source = source;
            AnimatedPhoto.Visibility = Visibility.Visible;

            Photo.Visibility = Visibility.Collapsed;
        }

        private void SetUpgradedGift(IClientService clientService, UpgradedGift gift, string reason)
        {
            SetAnimated(clientService, gift.Model.Sticker);
            SetRow(GiftName, gift.ToName());

            Reason.Header = Strings.StarGiftReason;
            SetRow(Reason, reason);
        }

        // What the sale would have paid without Telegram's cut, which is the amount that
        // reached us plus the commission that didn't.
        private void SetFullPrice(long amount, long commission)
        {
            if (commission == 0)
            {
                return;
            }

            SetRow(FullPrice, Formatter.TonBalance(Math.Abs(amount) + Math.Abs(commission)).Join());
        }

        private void Purchase_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_explorerUrl))
            {
                Hide(ContentDialogResult.Primary);
                return;
            }

            Hide();
            MessageHelper.OpenUrl(null, null, _explorerUrl);
        }

        private void CopyLink_Click(object sender, RoutedEventArgs e)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(_transactionId);
            ClipboardEx.TrySetContent(dataPackage);

            ToastPopup.Show(XamlRoot, Strings.StarsTransactionIDCopied, ToastPopupIcon.Copied);
        }
    }
}
