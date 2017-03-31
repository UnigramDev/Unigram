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
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep1ViewModel : UnigramViewModelBase
    {
        private TLMessage _message;

        public PaymentFormStep1ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
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
                    var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm>(from);

                    _message = tuple.Item1;
                    Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
                    PaymentForm = tuple.Item2;
                    Info = PaymentForm.HasSavedInfo ? PaymentForm.SavedInfo : new TLPaymentRequestedInfo { ShippingAddress = new TLPostAddress() };
                }
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

        private TLPaymentsPaymentForm _paymentForm;
        public TLPaymentsPaymentForm PaymentForm
        {
            get
            {
                return _paymentForm;
            }
            set
            {
                Set(ref _paymentForm, value);
                RaisePropertyChanged(() => IsAnyUserInfoRequested);
            }
        }

        private TLPaymentRequestedInfo _info = new TLPaymentRequestedInfo { ShippingAddress = new TLPostAddress() };
        public TLPaymentRequestedInfo Info
        {
            get
            {
                return _info;
            }
            set
            {
                Set(ref _info, value);
            }
        }

        public bool IsAnyUserInfoRequested
        {
            get
            {
                return _paymentForm != null && (_paymentForm.Invoice.IsEmailRequested || _paymentForm.Invoice.IsNameRequested || _paymentForm.Invoice.IsPhoneRequested);
            }
        }

        private bool? _isSave = true;
        public bool? IsSave
        {
            get
            {
                return _isSave;
            }
            set
            {
                Set(ref _isSave, value);
            }
        }

        private RelayCommand _sendCommand;
        public RelayCommand SendCommand => _sendCommand = _sendCommand ?? new RelayCommand(SendExecute, () => !IsLoading);
        private async void SendExecute()
        {
            IsLoading = true;

            var save = _isSave ?? false;
            var info = new TLPaymentRequestedInfo();
            if (_paymentForm.Invoice.IsNameRequested)
            {
                info.Name = _info.Name;
            }
            if (_paymentForm.Invoice.IsEmailRequested)
            {
                info.Email = _info.Email;
            }
            if (_paymentForm.Invoice.IsPhoneRequested)
            {
                info.Phone = _info.Phone;
            }
            if (_paymentForm.Invoice.IsShippingAddressRequested)
            {
                info.ShippingAddress = _info.ShippingAddress;
            }

            var response = await ProtoService.ValidateRequestedInfoAsync(save, _message.Id, info);
            if (response.IsSucceeded)
            {
                if (_paymentForm.HasSavedInfo && !save)
                {
                    ProtoService.ClearSavedInfoAsync(true, false, null, null);
                }


            }
        }
    }
}