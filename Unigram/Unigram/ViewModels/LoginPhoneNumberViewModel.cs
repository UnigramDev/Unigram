using System.Collections.Generic;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Views;
using GalaSoft.MvvmLight.Command;
using Telegram.Api.Aggregator;
using Unigram.Common;
using Unigram.Core.Models;
using System;
using Telegram.Api.Helpers;

namespace Unigram.ViewModels
{
    public class LoginPhoneNumberViewModel : UnigramViewModelBase
    {
        public LoginPhoneNumberViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            var alphabet = "abcdefghijklmnopqrstuvwxyz";
            var list = new List<KeyedList<string, Country>>(alphabet.Length);
            var dictionary = new Dictionary<string, KeyedList<string, Country>>();
            for (int i = 0; i < alphabet.Length; i++)
            {
                var key = alphabet[i].ToString();
                var group = new KeyedList<string, Country>(key);

                list.Add(group);
                dictionary[key] = group;
            }

            foreach (var country in Country.CountriesSource)
            {
                dictionary[country.GetKey()].Add(country);
            }

            Countries = list;

            ProtoService.GotUserCountry += GotUserCountry;

            if (!string.IsNullOrEmpty(ProtoService.Country))
            {
                GotUserCountry(this, new CountryEventArgs { Country = ProtoService.Country });
            }
        }

        private void GotUserCountry(object sender, CountryEventArgs e)
        {
            Country country = null;
            foreach (var local in Country.CountriesSource)
            {
                if (string.Equals(local.Code, e.Country, StringComparison.OrdinalIgnoreCase))
                {
                    country = local;
                    break;
                }
            }

            if (country != null && SelectedCountry == null && string.IsNullOrEmpty(PhoneNumber))
            {

                // Temporary fix: delay the execution by 500 millisec.
                // Reason: this operation was executed BEFORE the UI update
                // and population of the list, so when the list was afterwards
                // populated the first item (Afghanistan) was again assinged
                // as SelectedCountry, thus overriding the correct values.
                Execute.BeginOnUIThread(new TimeSpan(0,0,0,0,500),() =>
                {
                    _phoneCode = country.PhoneCode;
                    SelectedCountry = country;
                    RaisePropertyChanged(() => PhoneCode);
                });
            }
        }

        private Country _selectedCountry;
        public Country SelectedCountry
        {
            get
            {
                return _selectedCountry;
            }
            set
            {
                if (value != null)
                {
                    _phoneCode = value.PhoneCode;
                    RaisePropertyChanged(() => PhoneCode);
                }

                Set(ref _selectedCountry, value);
            }
        }

        private string _phoneCode;
        public string PhoneCode
        {
            get
            {
                return _phoneCode;
            }
            set
            {
                Set(ref _phoneCode, value);

                Country country = null;
                foreach (var c in Country.CountriesSource)
                {
                    if (c.PhoneCode == PhoneCode)
                    {
                        if (c.PhoneCode == "1" && c.Code != "us")
                        {
                            continue;
                        }

                        if (c.PhoneCode == "7" && c.Code != "ru")
                        {
                            continue;
                        }

                        country = c;
                        break;
                    }
                }

                SelectedCountry = country;
            }
        }

        private string _phoneNumber;
        public string PhoneNumber
        {
            get
            {
                return _phoneNumber;
            }
            set
            {
                Set(ref _phoneNumber, value);
            }
        }

        public List<KeyedList<string, Country>> Countries { get; private set; }

        public RelayCommand SendCommand => new RelayCommand(SendExecute);
        private async void SendExecute()
        {
            var result = await ProtoService.SendCodeAsync(PhoneCode.TrimStart('+') + PhoneNumber);
            if (result?.IsSucceeded == true)
            {
                var state = new
                {
                    PhoneNumber = PhoneCode.TrimStart('+') + PhoneNumber,
                    PhoneCodeHash = result.Value.PhoneCodeHash,
                    //PhoneRegistered = result.Value.PhoneRegistered.Value,
                    //PhoneCallTimeout = result.Value.SendCallTimeout.Value
                };

                NavigationService.Navigate(typeof(LoginPhoneCodePage), state);
            }
        }
    }
}
