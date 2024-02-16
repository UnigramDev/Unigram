//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Authorization
{
    public class AuthorizationEmailAddressViewModel : ViewModelBase
    {
        public AuthorizationEmailAddressViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.AuthorizationState is AuthorizationStateWaitEmailAddress waitEmailAddress)
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
                Logger.Error(error.Message);

                if (error.MessageEquals(ErrorType.EMAIL_INVALID))
                {
                    await ShowPopupAsync(Strings.EmailAddressInvalid, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                }
                else if (error.MessageEquals(ErrorType.EMAIL_NOT_ALLOWED))
                {
                    await ShowPopupAsync(Strings.EmailNotAllowed, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                }
            }
        }
    }
}