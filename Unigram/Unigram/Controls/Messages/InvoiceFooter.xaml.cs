using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages
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

            Amount.Text = BindConvert.Current.FormatAmount(invoice.TotalAmount, invoice.Currency);
            Label.Text = ConvertLabel(invoice.ReceiptMessageId != 0, invoice.IsTest);
        }

        private string ConvertLabel(bool receipt, bool test)
        {
            if (receipt)
            {
                return "  " + Strings.Resources.PaymentReceipt.ToUpper();
            }

            return "  " + (test ? Strings.Resources.PaymentTestInvoice : Strings.Resources.PaymentInvoice).ToUpper();
        }
    }
}
