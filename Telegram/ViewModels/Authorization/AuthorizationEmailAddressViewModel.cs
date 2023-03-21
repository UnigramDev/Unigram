//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Authorization
{
    public class AuthorizationEmailAddressViewModel : TLViewModelBase
    {
        public AuthorizationEmailAddressViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var authState = ClientService.GetAuthorizationState();
            if (authState is AuthorizationStateWaitEmailAddress waitEmailAddress)
            {

            }

            return Task.CompletedTask;
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (string.IsNullOrEmpty(_address) || !_address.IsValidEmailAddress())
            {
                RaisePropertyChanged("EMAIL_INVALID");
                return;
            }

            IsLoading = true;

            var response = await ClientService.SendAsync(new SetAuthenticationEmailAddress(_address));
            if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.EMAIL_INVALID))
                {
                    await ShowPopupAsync(Strings.EmailAddressInvalid, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                }
                else if (error.TypeEquals(ErrorType.EMAIL_NOT_ALLOWED))
                {
                    await ShowPopupAsync(Strings.EmailNotAllowed, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                }

                Logs.Logger.Error(Logs.LogTarget.API, "account.signIn error " + error);
            }
        }
    }
}