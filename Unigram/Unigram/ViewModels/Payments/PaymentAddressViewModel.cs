using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Entities;
using Unigram.Services;

namespace Unigram.ViewModels.Payments
{
    public class PaymentAddressViewModel : TLViewModelBase
    {
        private long _chatId;
        private long _messageId;

        public PaymentAddressViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
        }

        public void Initialize(long chatId, long messageId, Invoice invoice, OrderInfo info)
        {
            _chatId = chatId;
            _messageId = messageId;

            info ??= new OrderInfo();
            info.ShippingAddress ??= new Address();

            Invoice = invoice;
            Info = info;

            SelectedCountry = Country.Countries.FirstOrDefault(x => x.Code.Equals(info.ShippingAddress.CountryCode, StringComparison.OrdinalIgnoreCase));
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
                return _invoice != null && (_invoice.NeedEmailAddress || _invoice.NeedName || _invoice.NeedPhoneNumber);
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
                info.ShippingAddress.CountryCode = _selectedCountry?.Code?.ToUpper();
            }

            var response = await ProtoService.SendAsync(new ValidateOrderInfo(_chatId, _messageId, info, save));
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