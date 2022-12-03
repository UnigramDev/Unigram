using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Authorization
{
    public class AuthorizationCodeViewModel : TLViewModelBase
    {
        public AuthorizationCodeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ResendCommand = new RelayCommand(ResendExecute, () => !IsLoading);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var authState = ClientService.GetAuthorizationState();
            if (authState is AuthorizationStateWaitCode waitCode)
            {
                _codeInfo = waitCode.CodeInfo;

                RaisePropertyChanged(nameof(CodeInfo));
            }

            return Task.CompletedTask;
        }

        private AuthenticationCodeInfo _codeInfo;
        public AuthenticationCodeInfo CodeInfo
        {
            get => _codeInfo;
            set => Set(ref _codeInfo, value);
        }

        private string _code;
        public string Code
        {
            get => _code;
            set
            {
                Set(ref _code, value);

                var length = _codeInfo?.Type switch
                {
                    AuthenticationCodeTypeTelegramMessage telegramType => telegramType.Length,
                    AuthenticationCodeTypeFragment fragmentType => fragmentType.Length,
                    AuthenticationCodeTypeSms smsType => smsType.Length,
                    _ => 5,
                };

                if (_code.Length == length)
                {
                    SendExecute();
                }
            }
        }

        public string PhoneNumber => _codeInfo?.PhoneNumber;

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_codeInfo == null)
            {
                //...
                return;
            }

            if (string.IsNullOrEmpty(_code))
            {
                if (_codeInfo.Type is AuthenticationCodeTypeFragment fragment)
                {
                    MessageHelper.OpenUrl(ClientService, NavigationService, fragment.Url, false);
                }
                else
                {
                    RaisePropertyChanged("SENT_CODE_INVALID");
                }

                return;
            }

            IsLoading = true;

            var response = await ClientService.SendAsync(new CheckAuthenticationCode(_code));
            if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.PHONE_NUMBER_INVALID))
                {
                    await MessagePopup.ShowAsync(error.Message, Strings.Resources.InvalidPhoneNumber, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EMPTY) || error.TypeEquals(ErrorType.PHONE_CODE_INVALID))
                {
                    await MessagePopup.ShowAsync(error.Message, Strings.Resources.InvalidCode, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EXPIRED))
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();

                    await MessagePopup.ShowAsync(error.Message, Strings.Resources.CodeExpired, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.FIRSTNAME_INVALID))
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();

                    await MessagePopup.ShowAsync(error.Message, Strings.Resources.InvalidFirstName, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.LASTNAME_INVALID))
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();

                    await MessagePopup.ShowAsync(error.Message, Strings.Resources.InvalidLastName, Strings.Resources.OK);
                }
                else if (error.Message.StartsWith("FLOOD_WAIT"))
                {
                    await MessagePopup.ShowAsync(Strings.Resources.FloodWait, Strings.Resources.AppName, Strings.Resources.OK);
                }
                else if (error.Code != -1000)
                {
                    await MessagePopup.ShowAsync(error.Message, Strings.Resources.AppName, Strings.Resources.OK);
                }

                Logs.Logger.Error(Logs.LogTarget.API, "account.signIn error " + error);
            }
        }

        public RelayCommand ResendCommand { get; }
        private async void ResendExecute()
        {
            if (_codeInfo == null)
            {
                //...
                return;
            }

            if (_codeInfo.NextType == null)
            {
                return;
            }

            IsLoading = true;

            var function = new ResendAuthenticationCode();

            var response = await ClientService.SendAsync(function);
            if (response is Error error)
            {

            }
        }
    }
}