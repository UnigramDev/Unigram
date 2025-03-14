//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Payments
{
    public partial class PaymentAddressViewModel : ViewModelBase
    {
        private InputInvoice _inputInvoice;

        public PaymentAddressViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        public void Initialize(InputInvoice inputInvoice, Invoice invoice, OrderInfo info)
        {
            _inputInvoice = inputInvoice;

            info ??= new OrderInfo();
            info.ShippingAddress ??= new Address();

            Invoice = invoice;
            Info = info;

            SelectedCountry = Country.All.FirstOrDefault(x => x.Code.Equals(info.ShippingAddress.CountryCode, StringComparison.OrdinalIgnoreCase));
        }

        private Invoice _invoice;
        public Invoice Invoice
        {
            get => _invoice;
            set => Set(ref _invoice, value);
        }

        private OrderInfo _info = new OrderInfo { ShippingAddress = new Address() };
        public OrderInfo Info
        {
            get => _info;
            set => Set(ref _info, value);
        }

        private Country _selectedCountry = Country.All[0];
        public Country SelectedCountry
        {
            get => _selectedCountry;
            set => Set(ref _selectedCountry, value);
        }

        public bool IsAnyUserInfoRequested
        {
            get
            {
                return _invoice != null && (_invoice.NeedEmailAddress || _invoice.NeedName || _invoice.NeedPhoneNumber);
            }
        }

        private bool? _isSave = true;
        public bool? IsSave
        {
            get => _isSave;
            set => Set(ref _isSave, value);
        }

        public async Task<ValidatedOrderInfo> ValidateAsync()
        {
            IsLoading = true;

            var save = _isSave ?? false;
            var info = new OrderInfo();
            if (_invoice.NeedName)
            {
                info.Name = _info.Name;
            }
            if (_invoice.NeedEmailAddress)
            {
                info.EmailAddress = _info.EmailAddress;
            }
            if (_invoice.NeedPhoneNumber)
            {
                info.PhoneNumber = _info.PhoneNumber;
            }
            if (_invoice.NeedShippingAddress)
            {
                info.ShippingAddress = _info.ShippingAddress;
                info.ShippingAddress.CountryCode = _selectedCountry?.Code ?? string.Empty;
            }

            var response = await ClientService.SendAsync(new ValidateOrderInfo(_inputInvoice, info, save));
            if (response is ValidatedOrderInfo validated)
            {
                IsLoading = false;
                return validated;
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

            return null;
        }
    }
}