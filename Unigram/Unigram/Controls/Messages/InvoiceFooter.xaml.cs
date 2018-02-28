using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TdWindows;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
                return "  " + Strings.Android.PaymentReceipt.ToUpper();
            }

            return "  " + (test ? Strings.Android.PaymentTestInvoice : Strings.Android.PaymentInvoice).ToUpper();
        }
    }
}
