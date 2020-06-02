using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Entities;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep1ViewModel : PaymentFormViewModelBase
    {
        public PaymentFormStep1ViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
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
            //    var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm>(from);

            //    Message = tuple.Item1;
            //    Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
            //    PaymentForm = tuple.Item2;

            //    var info = PaymentForm.HasSavedInfo ? PaymentForm.SavedInfo : new TLPaymentRequestedInfo();
            //    if (info.ShippingAddress == null)
            //    {
            //        info.ShippingAddress = new TLPostAddress();
            //    }

            //    Info = info;
            //    SelectedCountry = Country.Countries.FirstOrDefault(x => x.Code.Equals(info.ShippingAddress.CountryIso2, StringComparison.OrdinalIgnoreCase));
            //}

            return Task.CompletedTask;
        }

        private OrderInfo _info = new OrderInfo { ShippingAddress = new Address() };
        public OrderInfo Info
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

        public IList<Country> Countries { get; } = Country.Countries.OrderBy(x => x.DisplayName).ToList();

        private Country _selectedCountry = Country.Countries[0];
        public Country SelectedCountry
        {
            get
            {
                return _selectedCountry;
            }
            set
            {
                Set(ref _selectedCountry, value);
            }
        }

        public bool IsAnyUserInfoRequested
        {
            get
            {
                return _paymentForm != null && (_paymentForm.Invoice.NeedEmailAddress || _paymentForm.Invoice.NeedName || _paymentForm.Invoice.NeedPhoneNumber);
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

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            IsLoading = true;

            var save = _isSave ?? false;
            var info = new OrderInfo();
            if (_paymentForm.Invoice.NeedName)
            {
                info.Name = _info.Name;
            }
            if (_paymentForm.Invoice.NeedEmailAddress)
            {
                info.EmailAddress = _info.EmailAddress;
            }
            if (_paymentForm.Invoice.NeedPhoneNumber)
            {
                info.PhoneNumber = _info.PhoneNumber;
            }
            if (_paymentForm.Invoice.NeedShippingAddress)
            {
                info.ShippingAddress = _info.ShippingAddress;
                info.ShippingAddress.CountryCode = _selectedCountry?.Code?.ToUpper();
            }

            var response = await ProtoService.SendAsync(new ValidateOrderInfo(0, 0, info, save));
            if (response is ValidatedOrderInfo validated)
            {
                IsLoading = false;

                if (_paymentForm.SavedOrderInfo != null && !save)
                {
                    ProtoService.Send(new DeleteSavedOrderInfo());
                }

                if (_paymentForm.Invoice.IsFlexible)
                {
                    //NavigationService.NavigateToPaymentFormStep2(_message, _paymentForm, info, response.Result);
                }
                else if (_paymentForm.SavedCredentials != null)
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
                    //    NavigationService.NavigateToPaymentFormStep5(_message, _paymentForm, info, response.Result, null, null, null, true);
                    //}
                    //else
                    //{
                    //    NavigationService.NavigateToPaymentFormStep4(_message, _paymentForm, info, response.Result, null);
                    //}
                }
                else
                {
                    //NavigationService.NavigateToPaymentFormStep3(_message, _paymentForm, info, response.Result, null);
                }
            }
            else if (response is Error error)
            {
                IsLoading = false;

                switch (error.Message)
                {
                    case "REQ_INFO_NAME_INVALID":
                    case "REQ_INFO_PHONE_INVALID":
                    case "REQ_INFO_EMAIL_INVALID":
                    case "ADDRESS_COUNTRY_INVALID":
                    case "ADDRESS_CITY_INVALID":
                    case "ADDRESS_POSTCODE_INVALID":
                    case "ADDRESS_STATE_INVALID":
                    case "ADDRESS_STREET_LINE1_INVALID":
                    case "ADDRESS_STREET_LINE2_INVALID":
                        RaisePropertyChanged(error.Message);
                        break;
                    default:
                        //AlertsCreator.processError(error, PaymentFormActivity.this, req);
                        break;
                }
            }
        }

        public override void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.RaisePropertyChanged(propertyName);

            if (propertyName.Equals("PaymentForm"))
            {
                RaisePropertyChanged(() => IsAnyUserInfoRequested);
            }
            else if (propertyName.Equals("IsLoading"))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }
    }
}