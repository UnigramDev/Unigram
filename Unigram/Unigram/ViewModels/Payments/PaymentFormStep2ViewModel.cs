using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Views.Payments;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep2ViewModel : PaymentFormViewModelBase
    {
        private TLPaymentRequestedInfo _info;

        public PaymentFormStep2ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var buffer = parameter as byte[];
            if (buffer != null)
            {
                using (var from = new TLBinaryReader(buffer))
                {
                    var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm, TLPaymentRequestedInfo, TLPaymentsValidatedRequestedInfo>(from);

                    Message = tuple.Item1;
                    Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
                    PaymentForm = tuple.Item2;
                    RequestedInfo = tuple.Item4;

                    _info = tuple.Item3;
                }
            }

            return Task.CompletedTask;
        }

        private TLPaymentsValidatedRequestedInfo _requestedInfo;
        public TLPaymentsValidatedRequestedInfo RequestedInfo
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

        private TLShippingOption _shipping;
        public TLShippingOption Shipping
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

        private RelayCommand _sendCommand;
        public RelayCommand SendCommand => _sendCommand = _sendCommand ?? new RelayCommand(SendExecute, () => !IsLoading);
        private void SendExecute()
        {
            if (_shipping != null)
            {
                if (_paymentForm.HasSavedCredentials)
                {
                    if (ApplicationSettings.Current.TmpPassword != null)
                    {
                        if (ApplicationSettings.Current.TmpPassword.ValidUntil < TLUtils.Now + 60)
                        {
                            ApplicationSettings.Current.TmpPassword = null;
                        }
                    }

                    if (ApplicationSettings.Current.TmpPassword != null)
                    {
                        NavigationService.NavigateToPaymentFormStep5(_message, _paymentForm, _info, _requestedInfo, _shipping, null, null, true);
                    }
                    else
                    {
                        NavigationService.NavigateToPaymentFormStep4(_message, _paymentForm, _info, _requestedInfo, _shipping);
                    }
                }
                else
                {
                    NavigationService.NavigateToPaymentFormStep3(_message, _paymentForm, _info, _requestedInfo, _shipping);
                }
            }
        }
    }
}
