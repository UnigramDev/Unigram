using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Native.TL;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Payments;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentReceiptViewModel : UnigramViewModelBase
    {
        private TLMessage _message;

        public PaymentReceiptViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var buffer = parameter as byte[];
            if (buffer == null)
            {
                return Task.CompletedTask;
            }

            using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            {
                var tuple = new TLTuple<TLMessage, TLPaymentsPaymentReceipt>(from);

                _message = tuple.Item1;
                Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
                Receipt = tuple.Item2;
                Bot = tuple.Item2.Users.FirstOrDefault(x => x.Id == tuple.Item2.BotId) as TLUser;
            }

            return Task.CompletedTask;
        }

        private TLMessageMediaInvoice _invoice = new TLMessageMediaInvoice();
        public TLMessageMediaInvoice Invoice
        {
            get
            {
                return _invoice;
            }
            set
            {
                Set(ref _invoice, value);
            }
        }

        private TLPaymentsPaymentReceipt _receipt = new TLPaymentsPaymentReceipt { Info = new TLPaymentRequestedInfo() };
        public TLPaymentsPaymentReceipt Receipt
        {
            get
            {
                return _receipt;
            }
            set
            {
                Set(ref _receipt, value);
            }
        }

        private TLUser _bot;
        public TLUser Bot
        {
            get
            {
                return _bot;
            }
            set
            {
                Set(ref _bot, value);
            }
        }
    }
}
