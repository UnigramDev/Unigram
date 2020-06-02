using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Services;
using Unigram.Views.Payments;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentReceiptViewModel : TLViewModelBase
    {
        public PaymentReceiptViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var navigation = parameter as ReceiptNavigation;
            if (navigation == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetPaymentReceipt(navigation.ChatId, navigation.ReceiptMessageId));
            if (response is PaymentReceipt receipt)
            {
                Receipt = receipt;
                Bot = ProtoService.GetUser(receipt.PaymentsProviderUserId);

                var second = await ProtoService.SendAsync(new GetMessage(navigation.ChatId, navigation.ReceiptMessageId));
                if (second is Message message1 && message1.Content is MessagePaymentSuccessful payment)
                {
                    Payment = payment;

                    var third = await ProtoService.SendAsync(new GetMessage(navigation.ChatId, payment.InvoiceMessageId));
                    if (third is Message message2 && message2.Content is MessageInvoice invoice)
                    {
                        Invoice = invoice;
                    }
                }
            }

            //using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            //{
            //    var tuple = new TLTuple<TLMessage, TLPaymentsPaymentReceipt>(from);

            //    _message = tuple.Item1;
            //    Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
            //    Receipt = tuple.Item2;
            //    Bot = tuple.Item2.Users.FirstOrDefault(x => x.Id == tuple.Item2.BotId) as TLUser;
            //}
        }

        private MessageInvoice _invoce;
        public MessageInvoice Invoice
        {
            get
            {
                return _invoce;
            }
            set
            {
                Set(ref _invoce, value);
            }
        }

        private MessagePaymentSuccessful _payment;
        public MessagePaymentSuccessful Payment
        {
            get
            {
                return _payment;
            }
            set
            {
                Set(ref _payment, value);
            }
        }

        private PaymentReceipt _receipt;
        public PaymentReceipt Receipt
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

        private User _bot;
        public User Bot
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
