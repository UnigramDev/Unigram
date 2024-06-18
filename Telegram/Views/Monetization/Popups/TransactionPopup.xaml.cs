//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Globalization;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Monetization.Popups
{
    public sealed partial class TransactionPopup : ContentPopup
    {
        private readonly ChatRevenueTransaction _info;

        public TransactionPopup(IClientService clientService, Chat chat, ChatRevenueTransaction info)
        {
            InitializeComponent();

            _info = info;

            if (info.Type is ChatRevenueTransactionTypeEarnings earnings)
            {
                Pill.SetChat(clientService, chat);
                Pill.Visibility = Visibility.Visible;

                Date.Text = Formatter.DateAt(earnings.StartDate) + " - " + Formatter.DateAt(earnings.EndDate);
                Message.Text = Strings.MonetizationTransactionDetailProceed;
                LearnCommand.Content = Strings.OK;
            }
            else if (info.Type is ChatRevenueTransactionTypeWithdrawal withdrawal)
            {
                Pill.Visibility = Visibility.Collapsed;

                if (withdrawal.State is RevenueWithdrawalStateSucceeded succeeded)
                {
                    Date.Text = Formatter.DateAt(succeeded.Date);
                    Message.Text = Strings.MonetizationTransactionDetailWithdraw;
                    LearnCommand.Content = Strings.MonetizationTransactionDetailWithdrawButton;
                }
                else if (withdrawal.State is RevenueWithdrawalStatePending)
                {
                    Date.Text = Formatter.DateAt(withdrawal.WithdrawalDate);
                    Message.Text = Strings.MonetizationTransactionPending;
                    LearnCommand.Content = Strings.OK;
                }
                else if (withdrawal.State is RevenueWithdrawalStateFailed)
                {
                    Date.Text = Formatter.DateAt(withdrawal.WithdrawalDate);
                    Message.Text = Strings.MonetizationTransactionNotCompleted;
                    LearnCommand.Content = Strings.OK;
                }
            }
            else if (info.Type is ChatRevenueTransactionTypeRefund refund)
            {
                Pill.Visibility = Visibility.Collapsed;

                Date.Text = Formatter.DateAt(refund.RefundDate);
                Message.Text = Strings.MonetizationTransactionDetailRefund;
                LearnCommand.Content = Strings.OK;
            }

            var doubleAmount = Formatter.Amount(Math.Abs(info.CryptocurrencyAmount), info.Cryptocurrency);
            var stringAmount = doubleAmount.ToString(CultureInfo.InvariantCulture).Split('.');
            var integerAmount = long.Parse(stringAmount[0]);
            var decimalAmount = stringAmount.Length > 1 ? stringAmount[1] : "0";

            Symbol.Text = info.CryptocurrencyAmount < 0 ? "-" : "+";
            Amount.Text = integerAmount.ToString("N0");
            Decimal.Text = string.Format(".{0}", decimalAmount.PadRight(2, '0'));

            Title.Foreground = BootStrapper.Current.Resources[info.CryptocurrencyAmount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Learn_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);

            if (_info?.Type is ChatRevenueTransactionTypeWithdrawal withdrawal && withdrawal.State is RevenueWithdrawalStateSucceeded succeeded)
            {
                MessageHelper.OpenUrl(null, null, succeeded.Url);
            }
        }
    }
}
