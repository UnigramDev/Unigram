//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Stripe;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Payments
{
    public partial class PaymentCredentialsViewModel : ViewModelBase
    {
        private PaymentFormTypeRegular _paymentForm;

        public PaymentCredentialsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public string Initialize(PaymentFormTypeRegular paymentForm)
        {
            _paymentForm = paymentForm;
            CanSaveCredentials = _paymentForm.CanSaveCredentials;

            if (paymentForm.PaymentProvider is PaymentProviderOther other)
            {
                IsWebUsed = true;
                return other.Url;
            }
            else if (paymentForm.PaymentProvider is PaymentProviderStripe stripe)
            {
                IsNativeUsed = true;
                SelectedCountry = null;

                NeedCountry = stripe.NeedCountry;
                NeedZip = stripe.NeedPostalCode;
                NeedCardholderName = stripe.NeedCardholderName;
            }
            else if (paymentForm.PaymentProvider is PaymentProviderSmartGlocal smartGlocal)
            {
                IsNativeUsed = true;
                SelectedCountry = null;

                NeedCountry = false;
                NeedZip = false;
                NeedCardholderName = false;
            }

            return null;
        }

        public string Initialize(PaymentFormTypeRegular paymentForm, PaymentOption paymentOption)
        {
            _paymentForm = paymentForm;
            CanSaveCredentials = _paymentForm.CanSaveCredentials;

            IsWebUsed = true;
            return paymentOption.Url;
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
            get => _card;
            set => Set(ref _card, value);
        }

        private string _date;
        public string Date
        {
            get => _date;
            set => Set(ref _date, value);
        }

        private string _cardName;
        public string CardName
        {
            get => _cardName;
            set => Set(ref _cardName, value);
        }

        private string _cvc;
        public string CVC
        {
            get => _cvc;
            set => Set(ref _cvc, value);
        }

        private string _postcode;
        public string Postcode
        {
            get => _postcode;
            set => Set(ref _postcode, value);
        }

        private Country _selectedCountry = Country.All[0];
        public Country SelectedCountry
        {
            get => _selectedCountry;
            set => Set(ref _selectedCountry, value);
        }

        #endregion

        private bool? _isSave = true;
        public bool? IsSave
        {
            get => _isSave;
            set => Set(ref _isSave, value);
        }

        public async Task<SavedCredentials> ValidateAsync()
        {
            var save = _isSave ?? false;
            //if (_paymentForm.SavedCredentials != null && !save && _paymentForm.CanSaveCredentials)
            //{
            //    //_paymentForm.HasSavedCredentials = false;
            //    _paymentForm.SavedCredentials = null;

            //    ClientService.Send(new DeleteSavedCredentials());
            //}

            var month = 0;
            var year = 0;

            if (_date != null)
            {
                var args = _date.Split('/');
                if (args.Length == 2)
                {
                    int.TryParse(args[0], out month);
                    int.TryParse(args[1], out year);
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

            var title = card.GetBrand() + " *" + card.GetLast4();

            var credentials = await GetCredentialsJson(card);
            if (credentials != null)
            {
                return new SavedCredentials(credentials, title);
            }
            else
            {
                IsLoading = false;
            }

            return null;
        }

        private async Task<string> GetCredentialsJson(Card card)
        {
            if (_paymentForm.PaymentProvider is PaymentProviderStripe stripe)
            {
                using (var client = new StripeClient(stripe.PublishableKey))
                {
                    var token = await client.CreateTokenAsync(card);
                    if (token != null)
                    {
                        return string.Format("{{\"type\":\"{0}\", \"id\":\"{1}\"}}", token.Type, token.Id);
                    }
                }
            }
            else if (_paymentForm.PaymentProvider is PaymentProviderSmartGlocal smartGlocal)
            {
                using (var client = new SmartGlocalClient(smartGlocal.PublicToken))
                {
                    var token = await client.CreateTokenAsync(card, _paymentForm.Invoice.IsTest);
                    if (token != null)
                    {
                        return string.Format("{{\"type\":\"{0}\", \"token\":\"{1}\"}}", "card", token);
                    }
                }
            }

            return null;
        }
    }
}
