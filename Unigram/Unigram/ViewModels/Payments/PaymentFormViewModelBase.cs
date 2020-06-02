using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormViewModelBase : TLViewModelBase
    {
        public PaymentFormViewModelBase(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        protected Message _message;
        public Message Message
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

        protected MessageInvoice _invoice;
        public MessageInvoice Invoice
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

        protected PaymentForm _paymentForm;
        public PaymentForm PaymentForm
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
