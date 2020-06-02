using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep2ViewModel : PaymentFormViewModelBase
    {
        private OrderInfo _info;

        public PaymentFormStep2ViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var buffer = parameter as byte[];
            if (buffer == null)
            {
                return Task.CompletedTask;
            }

            //using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            //{
            //    var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm, TLPaymentRequestedInfo, TLPaymentsValidatedRequestedInfo>(from);

            //    Message = tuple.Item1;
            //    Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
            //    PaymentForm = tuple.Item2;
            //    RequestedInfo = tuple.Item4;

            //    _info = tuple.Item3;
            //}

            return Task.CompletedTask;
        }

        private ValidatedOrderInfo _requestedInfo;
        public ValidatedOrderInfo RequestedInfo
        {
            get
            {
                return _requestedInfo;
            }
            set
            {
                Set(ref _requestedInfo, value);
            }
        }

        private ShippingOption _shipping;
        public ShippingOption Shipping
        {
            get
            {
                return _shipping;
            }
            set
            {
                Set(ref _shipping, value);
            }
        }

        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            if (_shipping != null)
            {
                if (_paymentForm.SavedCredentials != null)
                {
                    //if (ApplicationSettings.Current.TmpPassword != null)
                    //{
                    //    if (ApplicationSettings.Current.TmpPassword.ValidUntil < TLUtils.Now + 60)
                    //    {
                    //        ApplicationSettings.Current.TmpPassword = null;
                    //    }
                    //}

                    //if (ApplicationSettings.Current.TmpPassword != null)
                    //{
                    //    NavigationService.NavigateToPaymentFormStep5(_message, _paymentForm, _info, _requestedInfo, _shipping, null, null, true);
                    //}
                    //else
                    //{
                    //    NavigationService.NavigateToPaymentFormStep4(_message, _paymentForm, _info, _requestedInfo, _shipping);
                    //}
                }
                else
                {
                    //NavigationService.NavigateToPaymentFormStep3(_message, _paymentForm, _info, _requestedInfo, _shipping);
                }
            }
        }
    }
}
