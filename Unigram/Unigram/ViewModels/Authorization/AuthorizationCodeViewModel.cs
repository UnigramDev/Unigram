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
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Authorization
{
    public class AuthorizationCodeViewModel : TLViewModelBase
    {
        private readonly ISessionService _sessionService;
        private bool _confirmedGoBack;

        public AuthorizationCodeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ISessionService sessionService)
            : base(clientService, settingsService, aggregator)
        {
            _sessionService = sessionService;

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

        public override async void NavigatingFrom(NavigatingEventArgs args)
        {
            var authState = ClientService.GetAuthorizationState();
            if (authState is AuthorizationStateWaitCode waitCode && !_confirmedGoBack)
            {
                args.Cancel = true;

                var message = string.Format(Strings.Resources.EditNumberInfo, Common.PhoneNumber.Format(waitCode.CodeInfo.PhoneNumber));
                var title = Strings.Resources.EditNumber;

                var confirm = await ShowPopupAsync(message, title, Strings.Resources.Edit, Strings.Resources.Close);
                if (confirm == ContentDialogResult.Primary)
                {
                    _confirmedGoBack = true;
                    NavigationService.GoBack();
                }
            }
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
                    await ShowPopupAsync(error.Message, Strings.Resources.InvalidPhoneNumber, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EMPTY) || error.TypeEquals(ErrorType.PHONE_CODE_INVALID))
                {
                    await ShowPopupAsync(error.Message, Strings.Resources.InvalidCode, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EXPIRED))
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();

                    await ShowPopupAsync(error.Message, Strings.Resources.CodeExpired, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.FIRSTNAME_INVALID))
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();

                    await ShowPopupAsync(error.Message, Strings.Resources.InvalidFirstName, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.LASTNAME_INVALID))
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();

                    await ShowPopupAsync(error.Message, Strings.Resources.InvalidLastName, Strings.Resources.OK);
                }
                else if (error.Message.StartsWith("FLOOD_WAIT"))
                {
                    await ShowPopupAsync(Strings.Resources.FloodWait, Strings.Resources.AppName, Strings.Resources.OK);
                }
                else if (error.Code != -1000)
                {
                    await ShowPopupAsync(error.Message, Strings.Resources.AppName, Strings.Resources.OK);
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