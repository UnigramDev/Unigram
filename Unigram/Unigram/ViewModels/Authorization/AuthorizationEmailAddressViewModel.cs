using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Authorization
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
                    await MessagePopup.ShowAsync(Strings.Resources.EmailAddressInvalid, Strings.Resources.RestorePasswordNoEmailTitle, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.EMAIL_NOT_ALLOWED))
                {
                    await MessagePopup.ShowAsync(Strings.Resources.EmailNotAllowed, Strings.Resources.RestorePasswordNoEmailTitle, Strings.Resources.OK);
                }

                Logs.Logger.Error(Logs.LogTarget.API, "account.signIn error " + error);
            }
        }
    }
}