//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells.Monetization;
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
        private readonly TransactionInfo _info;

        public TransactionPopup(IClientService clientService, Chat chat, TransactionInfo info)
        {
            InitializeComponent();

            _info = info;

            if (string.IsNullOrEmpty(info.DestinationAddress))
            {
                Pill.SetChat(clientService, chat);
                Pill.Visibility = Visibility.Visible;

                Address.Visibility = Visibility.Collapsed;

                Message.Text = Strings.MonetizationTransactionDetailProceed;

                LearnCommand.Content = Strings.OK;
            }
            else
            {
                Address.Visibility = Visibility.Visible;
                Address.Content = info.DestinationAddress.Substring(0, info.DestinationAddress.Length / 2) + Environment.NewLine + info.DestinationAddress.Substring(info.DestinationAddress.Length / 2);

                Pill.Visibility = Visibility.Collapsed;

                Message.Text = Strings.MonetizationTransactionDetailWithdraw;

                LearnCommand.Content = Strings.MonetizationTransactionDetailWithdrawButton;
            }

            if (info.EndDate == 0)
            {
                Date.Text = Formatter.DateAt(info.Date);
            }
            else
            {
                Date.Text = Formatter.DateAt(info.Date) + " - " + Formatter.DateAt(info.EndDate);
            }

            var doubleAmount = Math.Abs(info.CryptocurrencyAmount / 100.0);
            var integerAmount = Math.Truncate(doubleAmount);
            var decimalAmount = (int)((decimal)doubleAmount % 1 * 100);

            Amount.Text = (info.CryptocurrencyAmount < 0 ? "-" : "+") + integerAmount.ToString("N0");
            Decimal.Text = string.Format(".{0:N0} {1}", decimalAmount, info.Cryptocurrency);

            Amount.FontSize = 28;
            Decimal.FontSize = 20;

            Title.Foreground = BootStrapper.Current.Resources[info.CryptocurrencyAmount < 0 ? "SystemFillColorCriticalBrush" : "SystemFillColorSuccessBrush"] as Brush;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Address_Click(object sender, RoutedEventArgs e)
        {
            MessageHelper.CopyText(_info.DestinationAddress);
        }

        private void Learn_Click(object sender, RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }
    }
}
