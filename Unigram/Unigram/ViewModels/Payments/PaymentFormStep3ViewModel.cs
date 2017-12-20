using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Unigram.Core.Stripe;
using Unigram.Views.Payments;
using Windows.Data.Json;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Payments
{
    public class PaymentFormStep3ViewModel : PaymentFormViewModelBase
    {
        private TLPaymentRequestedInfo _info;
        private TLPaymentsValidatedRequestedInfo _requestedInfo;
        private TLShippingOption _shipping;

        private string _publishableKey;

        public PaymentFormStep3ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
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
                var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm, TLPaymentRequestedInfo, TLPaymentsValidatedRequestedInfo, TLShippingOption>(from);

                Message = tuple.Item1;
                Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
                PaymentForm = tuple.Item2;

                _info = tuple.Item3;
                _requestedInfo = tuple.Item4;
                _shipping = tuple.Item5;

                if (_paymentForm.HasNativeProvider && _paymentForm.HasNativeParams && _paymentForm.NativeProvider.Equals("stripe"))
                {
                    IsNativeUsed = true;
                    SelectedCountry = null;

                    var json = JsonObject.Parse(_paymentForm.NativeParams.Data);

                    NeedCountry = json.GetNamedBoolean("need_country", false);
                    NeedZip = json.GetNamedBoolean("need_zip", false);
                    NeedCardholderName = json.GetNamedBoolean("need_cardholder_name", false);

                    _publishableKey = json.GetNamedString("publishable_key", string.Empty);
                }
                else
                {
                    IsNativeUsed = false;
                    RaisePropertyChanged("Navigate");
                }

                //var info = PaymentForm.HasSavedInfo ? PaymentForm.SavedInfo : new TLPaymentRequestedInfo();
                //if (info.ShippingAddress == null)
                //{
                //    info.ShippingAddress = new TLPostAddress();
                //}

                //Info = info;
                //SelectedCountry = null;
            }

            return Task.CompletedTask;
        }

        private bool _isNativeUsed;
        public bool IsNativeUsed
        {
            get
            {
                return _isNativeUsed;
            }
            set
            {
                Set(ref _isNativeUsed, value);
            }
        }

        #region Native

        public bool NeedCountry { get; private set; }

        public bool NeedZip { get; private set; }

        public bool NeedCardholderName { get; private set; }

        public bool NeedZipOrCountry
        {
            get
            {
                return NeedZip || NeedCountry;
            }
        }

        private string _card;
        public string Card
        {
            get
            {
                return _card;
            }
            set
            {
                Set(ref _card, value);
            }
        }

        private string _date;
        public string Date
        {
            get
            {
                return _date;
            }
            set
            {
                Set(ref _date, value);
            }
        }

        private string _cardName;
        public string CardName
        {
            get
            {
                return _cardName;
            }
            set
            {
                Set(ref _cardName, value);
            }
        }

        private string _cvc;
        public string CVC
        {
            get
            {
                return _cvc;
            }
            set
            {
                Set(ref _cvc, value);
            }
        }

        private string _postcode;
        public string Postcode
        {
            get
            {
                return _postcode;
            }
            set
            {
                Set(ref _postcode, value);
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

        #endregion

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
            var save = _isSave ?? false;
            if (_paymentForm.HasSavedCredentials && !save && _paymentForm.IsCanSaveCredentials)
            {
                _paymentForm.HasSavedCredentials = false;
                _paymentForm.SavedCredentials = null;

                ApplicationSettings.Current.TmpPassword = null;
                ProtoService.ClearSavedInfoAsync(false, true, null, null);
            }

            var month = 0;
            var year = 0;

            if (_date != null)
            {
                var args = _date.Split('/');
                if (args.Length == 2)
                {
                    month = int.Parse(args[0]);
                    year = int.Parse(args[1]);
                }
            }

            var card = new Card(
                _card,
                month,
                year,
                _cvc,
                _cardName,
                null, null, null, null,
                _postcode,
                _selectedCountry?.Code?.ToUpper(),
                null);

            if (!card.ValidateNumber())
            {
                RaisePropertyChanged("CARD_NUMBER_INVALID");
                return;
            }
            if (!card.ValidateExpireDate())
            {
                RaisePropertyChanged("CARD_EXPIRE_DATE_INVALID");
                return;
            }
            if (NeedCardholderName && string.IsNullOrWhiteSpace(_cardName))
            {
                RaisePropertyChanged("CARD_HOLDER_NAME_INVALID");
                return;
            }
            if (!card.ValidateCVC())
            {
                RaisePropertyChanged("CARD_CVC_INVALID");
                return;
            }
            if (NeedCountry && _selectedCountry == null)
            {
                RaisePropertyChanged("CARD_COUNTRY_INVALID");
                return;
            }
            if (NeedZip && string.IsNullOrWhiteSpace(_postcode))
            {
                RaisePropertyChanged("CARD_ZIP_INVALID");
                return;
            }

            IsLoading = true;

            using (var stripe = new StripeClient(_publishableKey))
            {
                var token = await stripe.CreateTokenAsync(card);
                if (token != null)
                {
                    var title = card.GetBrand() + " *" + card.GetLast4();
                    var credentials = string.Format("{{\"type\":\"{0}\", \"id\":\"{1}\"}}", token.Type, token.Id);

                    NavigateToNextStep(title, credentials, _isSave ?? false);
                }
                else
                {
                    IsLoading = false;
                }
            }

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

        public override void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            base.RaisePropertyChanged(propertyName);

            if (propertyName.Equals("IsLoading"))
            {
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        public void NavigateToNextStep(string title, string credentials, bool save)
        {
            NavigationService.NavigateToPaymentFormStep5(_message, _paymentForm, _info, _requestedInfo, _shipping, title, credentials, save);
        }
    }
}
