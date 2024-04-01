//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Converters;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Monetization.Popups
{
    public sealed partial class TransferPopup : ContentPopup
    {
        public TransferPopup(string address, CryptoAmount amount)
        {
            InitializeComponent();

            Title = string.Format(Strings.MonetizationTransferTitle, Formatter.FormatAmount(amount.CryptocurrencyAmount, amount.Cryptocurrency));

            Address.Content = address.Substring(0, address.Length / 2) + Environment.NewLine + address.Substring(address.Length / 2);

            PrimaryButtonText = Strings.Send;
            SecondaryButtonText = Strings.Cancel;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
