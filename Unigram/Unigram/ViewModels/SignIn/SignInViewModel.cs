using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Entities;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.SignIn;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.SignIn
{
    public class SignInViewModel : UnigramViewModelBase
    {
        public SignInViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            //LegacyService.GotUserCountry += GotUserCountry;

            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ProxyCommand = new RelayCommand(ProxyExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //if (!string.IsNullOrEmpty(LegacyService.Country))
            //{
            //    GotUserCountry(this, new CountryEventArgs { Country = LegacyService.Country });
            //}

            ProtoService.Send(new GetCountryCode(), result =>
            {
                if (result is Text text)
                {
                    BeginOnUIThread(() => GotUserCountry(text.TextValue));
                }
            });

            IsLoading = false;
            return Task.CompletedTask;
        }

        private void GotUserCountry(string code)
        {
            Country country = null;
            foreach (var local in Country.Countries)
            {
                if (string.Equals(local.Code, code, StringComparison.OrdinalIgnoreCase))
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

            var phoneNumber = (_phoneCode + _phoneNumber).Replace(" ", string.Empty);

            await ProtoService.SendAsync(new SetOption("x_phonenumber", new OptionValueString(phoneNumber)));

            var response = await ProtoService.SendAsync(new SetAuthenticationPhoneNumber(phoneNumber, false, false));
            if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.PHONE_NUMBER_INVALID))
                {
                    //needShowInvalidAlert(req.phone_number, false);
                }
                else if (error.TypeEquals(ErrorType.PHONE_NUMBER_FLOOD))
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.PhoneNumberFlood, Strings.Resources.AppName, Strings.Resources.OK);
                }
                //else if (response.Error.TypeEquals(TLErrorType.PHONE_NUMBER_BANNED))
                //{
                //    needShowInvalidAlert(req.phone_number, true);
                //}
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EMPTY) || error.TypeEquals(ErrorType.PHONE_CODE_INVALID))
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.InvalidCode, Strings.Resources.AppName, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EXPIRED))
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.CodeExpired, Strings.Resources.AppName, Strings.Resources.OK);
                }
                else if (error.Message.StartsWith("FLOOD_WAIT"))
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.FloodWait, Strings.Resources.AppName, Strings.Resources.OK);
                }
                else if (error.Code != -1000)
                {
                    await TLMessageDialog.ShowAsync(error.Message, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
        }

        public RelayCommand ProxyCommand { get; }
        private async void ProxyExecute()
        {
            var proxy = ApplicationSettings.Current.Proxy;

            var dialog = new ProxyView(false);
            dialog.Server = ApplicationSettings.Current.Proxy.Server;
            dialog.Port = proxy.Port.ToString();
            dialog.Username = proxy.Username;
            dialog.Password = proxy.Password;
            dialog.IsProxyEnabled = proxy.IsEnabled;
            dialog.IsCallsProxyEnabled = proxy.IsCallsEnabled;

            var enabled = proxy.IsEnabled == true;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                var server = proxy.Server = dialog.Server ?? string.Empty;
                var port = proxy.Port = Extensions.TryParseOrDefault(dialog.Port, 1080);
                var username = proxy.Username = dialog.Username ?? string.Empty;
                var password = proxy.Password = dialog.Password ?? string.Empty;
                var newValue = proxy.IsEnabled = dialog.IsProxyEnabled;
                proxy.IsCallsEnabled = dialog.IsCallsProxyEnabled;

                if (newValue || newValue != enabled)
                {
                    if (newValue)
                    {
                        ProtoService.Send(new SetProxy(new ProxySocks5(server, port, username, password)));
                    }
                    else
                    {
                        ProtoService.Send(new SetProxy(new ProxyEmpty()));
                    }
                }
            }
        }
    }
}