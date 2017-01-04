using System.Collections.Generic;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Views.Login;
using Telegram.Api.Aggregator;
using Unigram.Common;
using Unigram.Core.Models;
using System;
using Telegram.Api.Helpers;
using Windows.UI.Popups;
using Telegram.Api.TL;
using Telegram.Api;

namespace Unigram.ViewModels.Login
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

            // IDEA FELA

            //if(SelectedCountry == null)
            //{
            //    SelectedCountry = Countries[0][0];
            //}

            // Oldimplementation, keeping it till further investigation.
            // This portion is moved in a RelayCommand in MATEI'S IDEA.

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

            // IDEA MATEI
            //if (country != null && string.IsNullOrEmpty(PhoneNumber))

            // Old implementation, keeping it til further investigation
            if (country != null && SelectedCountry == null && string.IsNullOrEmpty(PhoneNumber))
            {
                Execute.BeginOnUIThread(() =>
                {
                    SelectedCountry = country;
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
            var result = await ProtoService.SendCodeAsync(PhoneCode.TrimStart('+') + PhoneNumber, /* TODO: Verify */ null);
            if (result?.IsSucceeded == true)
            {
                var state = new LoginPhoneCodePage.NavigationParameters
                {
                    PhoneNumber = PhoneCode.TrimStart('+') + PhoneNumber,
                    PhoneCodeHash = result.Value.PhoneCodeHash,
                    //PhoneRegistered = result.Value.PhoneRegistered.Value,
                    //PhoneCallTimeout = result.Value.SendCallTimeout.Value
                };

                NavigationService.Navigate(typeof(LoginPhoneCodePage), state);
            }
            else if (result.Error != null)
            {
                await new MessageDialog(result.Error.ErrorMessage, result.Error.ErrorCode.ToString()).ShowAsync();
            }
        }

        // IDEA MATEI

        //public RelayCommand LocalizeCommand => new RelayCommand(LocalizeExecute);
        //private void LocalizeExecute()
        //{
        //    ProtoService.GotUserCountry += GotUserCountry;

        //    if (!string.IsNullOrEmpty(ProtoService.Country))
        //    {
        //        GotUserCountry(this, new CountryEventArgs { Country = ProtoService.Country });
        //    }
        //}
    }
}