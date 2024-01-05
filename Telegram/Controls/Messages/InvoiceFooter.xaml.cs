//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Converters;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages
{
    public sealed partial class InvoiceFooter : ContentPresenter
    {
        public InvoiceFooter()
        {
            InitializeComponent();
        }

        public void UpdateMessage(MessageViewModel message)
        {
            var invoice = message.Content as MessageInvoice;
            if (invoice == null)
            {
                return;
            }

            Amount.Text = Formatter.FormatAmount(invoice.TotalAmount, invoice.Currency);
            Label.Text = ConvertLabel(invoice.ReceiptMessageId != 0, invoice.IsTest);
        }

        private string ConvertLabel(bool receipt, bool test)
        {
            if (receipt)
            {
                return "  " + Strings.PaymentReceipt.ToUpper();
            }

            return "  " + (test ? Strings.PaymentTestInvoice : Strings.PaymentInvoice).ToUpper();
        }
    }
}
