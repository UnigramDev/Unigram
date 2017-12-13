using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Converters;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Messages
{
    public sealed partial class InvoiceFooter : HackedContentPresenter
    {
        public TLMessage Message => DataContext as TLMessage;
        public TLMessageMediaInvoice ViewModel => Message?.Media as TLMessageMediaInvoice;

        public BindConvert Convert => BindConvert.Current;

        private TLMessageMediaInvoice _oldValue;

        public InvoiceFooter()
        {
            InitializeComponent();

            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null && ViewModel != _oldValue) Bindings.Update();
                if (ViewModel == null) Bindings.StopTracking();

                _oldValue = ViewModel;
            };
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
