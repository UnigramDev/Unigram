using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Telegram.Api.TL.Payments;
using Template10.Services.NavigationService;
using Unigram.Views.Payments;
using Unigram.Services;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormViewModelBase : UnigramViewModelBase
    {
        public PaymentFormViewModelBase(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }

        protected TLMessage _message;
        public TLMessage Message
        {
            get
            {
                return _message;
            }
            set
            {
                Set(ref _message, value);
            }
        }

        protected TLMessageMediaInvoice _invoice = new TLMessageMediaInvoice();
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

        protected TLPaymentsPaymentForm _paymentForm;
        public TLPaymentsPaymentForm PaymentForm
        {
            get
            {
                return _paymentForm;
            }
            set
            {
                Set(ref _paymentForm, value);
            }
        }
    }
}
