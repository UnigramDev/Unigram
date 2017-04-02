using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Unigram.Core.Models;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep3ViewModel : PaymentFormViewModelBase
    {
        public PaymentFormStep3ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            SelectedCountry = null;

            return Task.CompletedTask;
        }

        public List<KeyedList<string, Country>> Countries { get; } = Country.GroupedCountries;

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

            //var save = _isSave ?? false;
            //var info = new TLPaymentRequestedInfo();
            //if (_paymentForm.Invoice.IsNameRequested)
            //{
            //    info.Name = _info.Name;
            //}
            //if (_paymentForm.Invoice.IsEmailRequested)
            //{
            //    info.Email = _info.Email;
            //}
            //if (_paymentForm.Invoice.IsPhoneRequested)
            //{
            //    info.Phone = _info.Phone;
            //}
            //if (_paymentForm.Invoice.IsShippingAddressRequested)
            //{
            //    info.ShippingAddress = _info.ShippingAddress;
            //    info.ShippingAddress.CountryIso2 = _selectedCountry?.Code;
            //}

            //var response = await ProtoService.ValidateRequestedInfoAsync(save, _message.Id, info);
            //if (response.IsSucceeded)
            //{
            //    IsLoading = false;

            //    if (_paymentForm.HasSavedInfo && !save)
            //    {
            //        ProtoService.ClearSavedInfoAsync(true, false, null, null);
            //    }

            //    if (_paymentForm.Invoice.IsFlexible)
            //    {
            //        NavigationService.Navigate(typeof(PaymentFormStep2Page), TLTuple.Create(_message, _paymentForm, response.Result));
            //    }
            //    else if (_paymentForm.HasSavedCredentials)
            //    {
            //        // TODO: Is password expired?
            //        var expired = true;
            //        if (expired)
            //        {
            //            NavigationService.Navigate(typeof(PaymentFormStep4Page));
            //        }
            //        else
            //        {
            //            NavigationService.Navigate(typeof(PaymentFormStep5Page));
            //        }
            //    }
            //    else
            //    {
            //        NavigationService.Navigate(typeof(PaymentFormStep3Page));
            //    }
            //}
        }
    }
}
