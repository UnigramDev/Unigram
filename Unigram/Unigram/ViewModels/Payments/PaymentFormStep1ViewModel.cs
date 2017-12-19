using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Native.TL;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Payments;
using Unigram.Common;
using Unigram.Core.Models;
using Unigram.Views.Payments;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep1ViewModel : PaymentFormViewModelBase
    {
        public PaymentFormStep1ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
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

            using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            {
                var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm>(from);

                Message = tuple.Item1;
                Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
                PaymentForm = tuple.Item2;

                var info = PaymentForm.HasSavedInfo ? PaymentForm.SavedInfo : new TLPaymentRequestedInfo();
                if (info.ShippingAddress == null)
                {
                    info.ShippingAddress = new TLPostAddress();
                }

                Info = info;
                SelectedCountry = Country.Countries.FirstOrDefault(x => x.Code.Equals(info.ShippingAddress.CountryIso2, StringComparison.OrdinalIgnoreCase));
            }

            return Task.CompletedTask;
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

        public RelayCommand SendCommand { get; }
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
                info.ShippingAddress.CountryIso2 = _selectedCountry?.Code?.ToUpper();
            }

            var response = await ProtoService.ValidateRequestedInfoAsync(_message.Id, info, save);
            if (response.IsSucceeded)
            {
                IsLoading = false;

                if (_paymentForm.HasSavedInfo && !save)
                {
                    ProtoService.ClearSavedInfoAsync(true, false, null, null);
                }

                if (_paymentForm.Invoice.IsFlexible)
                {
                    NavigationService.NavigateToPaymentFormStep2(_message, _paymentForm, info, response.Result);
                }
                else if (_paymentForm.HasSavedCredentials)
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
                        NavigationService.NavigateToPaymentFormStep5(_message, _paymentForm, info, response.Result, null, null, null, true);
                    }
                    else
                    {
                        NavigationService.NavigateToPaymentFormStep4(_message, _paymentForm, info, response.Result, null);
                    }
                }
                else
                {
                    NavigationService.NavigateToPaymentFormStep3(_message, _paymentForm, info, response.Result, null);
                }
            }
            else if (response.Error != null)
            {
                IsLoading = false;

                switch (response.Error.ErrorMessage)
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
                        RaisePropertyChanged(response.Error.ErrorMessage);
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