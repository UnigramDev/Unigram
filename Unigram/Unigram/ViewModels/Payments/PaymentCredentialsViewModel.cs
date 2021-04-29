using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Entities;
using Unigram.Services;
using Unigram.Services.Stripe;

namespace Unigram.ViewModels.Payments
{
    public class PaymentCredentialsViewModel : TLViewModelBase
    {
        private PaymentForm _paymentForm;
        private string _publishableKey;

        public PaymentCredentialsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        public bool Initialize(PaymentForm paymentForm)
        {
            //using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            //{
            //    var tuple = new TLTuple<TLMessage, TLPaymentsPaymentForm, TLPaymentRequestedInfo, TLPaymentsValidatedRequestedInfo, TLShippingOption>(from);

            //    Message = tuple.Item1;
            //    Invoice = tuple.Item1.Media as TLMessageMediaInvoice;
            //    PaymentForm = tuple.Item2;

            //    _info = tuple.Item3;
            //    _requestedInfo = tuple.Item4;
            //    _shipping = tuple.Item5;

            _paymentForm = paymentForm;

            CanSaveCredentials = _paymentForm.CanSaveCredentials;

            if (paymentForm.PaymentsProvider != null)
            {
                IsNativeUsed = true;
                SelectedCountry = null;

                NeedCountry = paymentForm.PaymentsProvider.NeedCountry;
                NeedZip = paymentForm.PaymentsProvider.NeedPostalCode;
                NeedCardholderName = paymentForm.PaymentsProvider.NeedCardholderName;

                _publishableKey = paymentForm.PaymentsProvider.PublishableKey;

                return false;
            }
            else
            {
                IsWebUsed = true;
                RaisePropertyChanged("Navigate");

                return true;
            }
        }

        public bool IsNativeUsed { get; private set; }
        public bool IsWebUsed { get; private set; }

        #region Native

        public bool CanSaveCredentials { get; private set; }

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

        private Country _selectedCountry = Country.All[0];
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

        public async Task<SavedCredentials> ValidateAsync()
        {
            var save = _isSave ?? false;
            if (_paymentForm.SavedCredentials != null && !save && _paymentForm.CanSaveCredentials)
            {
                //_paymentForm.HasSavedCredentials = false;
                _paymentForm.SavedCredentials = null;

                ProtoService.Send(new DeleteSavedCredentials());
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
                return null;
            }
            if (!card.ValidateExpireDate())
            {
                RaisePropertyChanged("CARD_EXPIRE_DATE_INVALID");
                return null;
            }
            if (NeedCardholderName && string.IsNullOrWhiteSpace(_cardName))
            {
                RaisePropertyChanged("CARD_HOLDER_NAME_INVALID");
                return null;
            }
            if (!card.ValidateCVC())
            {
                RaisePropertyChanged("CARD_CVC_INVALID");
                return null;
            }
            if (NeedCountry && _selectedCountry == null)
            {
                RaisePropertyChanged("CARD_COUNTRY_INVALID");
                return null;
            }
            if (NeedZip && string.IsNullOrWhiteSpace(_postcode))
            {
                RaisePropertyChanged("CARD_ZIP_INVALID");
                return null;
            }

            IsLoading = true;

            using (var stripe = new StripeClient(_publishableKey))
            {
                var token = await stripe.CreateTokenAsync(card);
                if (token != null)
                {
                    var title = card.GetBrand() + " *" + card.GetLast4();
                    var credentials = string.Format("{{\"type\":\"{0}\", \"id\":\"{1}\"}}", token.Type, token.Id);

                    return new SavedCredentials(credentials, title);
                }
                else
                {
                    IsLoading = false;
                }
            }

            return null;
        }
    }
}
