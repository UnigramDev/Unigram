using System.Collections.Generic;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Views.SignIn;
using Telegram.Api.Aggregator;
using Unigram.Common;
using Unigram.Core.Models;
using System;
using Telegram.Api.Helpers;
using Windows.UI.Popups;
using Telegram.Api.TL;
using Telegram.Api;
using Windows.UI.Xaml;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;
using Unigram.Controls;
using Windows.System;
using Windows.UI.Core;
using Telegram.Api.TL.Auth.Methods;
using System.Diagnostics;
using Unigram.Views;
using Unigram.Views.Settings;
using System.Linq;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPhoneViewModel : UnigramViewModelBase
    {
        public SettingsPhoneViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            ProtoService.GotUserCountry += GotUserCountry;

            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (!string.IsNullOrEmpty(ProtoService.Country))
            {
                GotUserCountry(this, new CountryEventArgs { Country = ProtoService.Country });
            }

            IsLoading = false;
            return Task.CompletedTask;
        }

        private void GotUserCountry(object sender, CountryEventArgs e)
        {
            Country country = null;
            foreach (var local in Country.Countries)
            {
                if (string.Equals(local.Code, e.Country, StringComparison.OrdinalIgnoreCase))
                {
                    country = local;
                    break;
                }
            }

            if (country != null && SelectedCountry == null && string.IsNullOrEmpty(PhoneNumber))
            {
                BeginOnUIThread(() =>
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
                foreach (var c in Country.Countries)
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

        public IList<Country> Countries { get; } = Country.Countries.OrderBy(x => x.DisplayName).ToList();

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (string.IsNullOrEmpty(_phoneCode))
            {
                RaisePropertyChanged("PHONE_CODE_INVALID");
                return;
            }

            if (string.IsNullOrEmpty(_phoneNumber))
            {
                RaisePropertyChanged("PHONE_NUMBER_INVALID");
                return;
            }

            IsLoading = true;

            var response = await ProtoService.SendChangePhoneCodeAsync(_phoneCode + _phoneNumber, /* TODO: Verify */ null);
            if (response.IsSucceeded)
            {
                var state = new SettingsPhoneSentCodePage.NavigationParameters
                {
                    PhoneNumber = PhoneCode.TrimStart('+') + PhoneNumber,
                    Result = response.Result,
                };

                NavigationService.Navigate(typeof(SettingsPhoneSentCodePage), state);
            }
            else if (response.Error != null)
            {
                IsLoading = false;

                if (response.Error.TypeEquals(TLErrorType.PHONE_NUMBER_FLOOD))
                {
                    await TLMessageDialog.ShowAsync("Sorry, you have deleted and re-created your account too many times recently. Please wait for a few days before signing up again.", "Telegram", "OK");
                }
                else
                {
                    await new TLMessageDialog(response.Error.ErrorMessage ?? "Error message", response.Error.ErrorCode.ToString()).ShowQueuedAsync();
                }
            }
        }
    }
}