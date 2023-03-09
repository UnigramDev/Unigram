//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsPhoneViewModel : TLViewModelBase
    {
        public SettingsPhoneViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            ClientService.Send(new GetCountryCode(), result =>
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
            foreach (var local in Country.All)
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
            get => _selectedCountry;
            set => Set(ref _selectedCountry, value);
        }

        private string _phoneNumber;
        public string PhoneNumber
        {
            get => _phoneNumber;
            set => Set(ref _phoneNumber, value);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var phoneNumber = _phoneNumber?.Trim('+').Replace(" ", string.Empty);
            if (string.IsNullOrEmpty(phoneNumber))
            {
                RaisePropertyChanged("PHONE_NUMBER_INVALID");
                return;
            }

            IsLoading = true;

            await ClientService.SendAsync(new SetOption("x_phonenumber", new OptionValueString(phoneNumber)));

            var response = await ClientService.SendAsync(new ChangePhoneNumber(phoneNumber, new PhoneNumberAuthenticationSettings(false, false, false, false, null, new string[0])));
            if (response is AuthenticationCodeInfo info)
            {
                BootStrapper.Current.SessionState["x_codeinfo"] = info;
                NavigationService.Navigate(typeof(SettingsPhoneSentCodePage));
            }
            else if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.PHONE_NUMBER_FLOOD))
                {
                    await ShowPopupAsync(Strings.Resources.PhoneNumberFlood, Strings.Resources.AppName, Strings.Resources.OK);
                }
                else
                {
                    await ShowPopupAsync(new MessagePopup(error.Message ?? "Error message", error.Code.ToString()));
                }
            }
        }
    }
}