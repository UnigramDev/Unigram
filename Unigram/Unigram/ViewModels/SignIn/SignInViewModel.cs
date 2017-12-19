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
using Unigram.Controls.Views;
using Windows.UI.Xaml.Controls;
using System.Linq;

namespace Unigram.ViewModels.SignIn
{
    public class SignInViewModel : UnigramViewModelBase
    {
        public SignInViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            ProtoService.GotUserCountry += GotUserCountry;

            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ProxyCommand = new RelayCommand(ProxyExecute);
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

            var response = await ProtoService.SendCodeAsync(_phoneCode + _phoneNumber, /* TODO: Verify */ null);
            if (response.IsSucceeded)
            {
                var state = new SignInSentCodePage.NavigationParameters
                {
                    PhoneNumber = PhoneCode.TrimStart('+') + PhoneNumber,
                    Result = response.Result,
                };

                NavigationService.Navigate(typeof(SignInSentCodePage), state);
            }
            else if (response.Error != null)
            {
                IsLoading = false;

                if (response.Error.TypeEquals(TLErrorType.PHONE_NUMBER_INVALID))
                {
                    //needShowInvalidAlert(req.phone_number, false);
                }
                else if (response.Error.TypeEquals(TLErrorType.PHONE_NUMBER_FLOOD))
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.PhoneNumberFlood, Strings.Android.AppName, Strings.Android.OK);
                }
                //else if (response.Error.TypeEquals(TLErrorType.PHONE_NUMBER_BANNED))
                //{
                //    needShowInvalidAlert(req.phone_number, true);
                //}
                else if (response.Error.TypeEquals(TLErrorType.PHONE_CODE_EMPTY) || response.Error.TypeEquals(TLErrorType.PHONE_CODE_INVALID))
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.InvalidCode, Strings.Android.AppName, Strings.Android.OK);
                }
                else if (response.Error.TypeEquals(TLErrorType.PHONE_CODE_EXPIRED))
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.CodeExpired, Strings.Android.AppName, Strings.Android.OK);
                }
                else if (response.Error.ErrorMessage.StartsWith("FLOOD_WAIT"))
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.FloodWait, Strings.Android.AppName, Strings.Android.OK);
                }
                else if (response.Error.ErrorCode != -1000)
                {
                    await TLMessageDialog.ShowAsync(response.Error.ErrorMessage, Strings.Android.AppName, Strings.Android.OK);
                }
            }
        }

        public RelayCommand ProxyCommand { get; }
        private async void ProxyExecute()
        {
            var dialog = new ProxyView();
            dialog.Server = SettingsHelper.ProxyServer;
            dialog.Port = SettingsHelper.ProxyPort.ToString();
            dialog.Username = SettingsHelper.ProxyUsername;
            dialog.Password = SettingsHelper.ProxyPassword;
            dialog.IsProxyEnabled = SettingsHelper.IsProxyEnabled;
            dialog.IsCallsProxyEnabled = SettingsHelper.IsCallsProxyEnabled;

            var enabled = SettingsHelper.IsProxyEnabled == true;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                SettingsHelper.ProxyServer = dialog.Server;
                SettingsHelper.ProxyPort = Extensions.TryParseOrDefault(dialog.Port, 1080);
                SettingsHelper.ProxyUsername = dialog.Username;
                SettingsHelper.ProxyPassword = dialog.Password;
                SettingsHelper.IsProxyEnabled = dialog.IsProxyEnabled;
                SettingsHelper.IsCallsProxyEnabled = dialog.IsCallsProxyEnabled;

                if (SettingsHelper.IsProxyEnabled || SettingsHelper.IsProxyEnabled != enabled)
                {
                    UnigramContainer.Current.ResolveType<IMTProtoService>().ToggleProxy();
                }
            }
        }
    }
}