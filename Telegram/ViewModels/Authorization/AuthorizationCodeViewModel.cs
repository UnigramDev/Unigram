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
        private bool _confirmedGoBack;

        public AuthorizationCodeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ResendCommand = new RelayCommand(ResendExecute, () => !IsLoading);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.AuthorizationState is AuthorizationStateWaitCode waitCode)
            {
                _codeInfo = waitCode.CodeInfo;

                RaisePropertyChanged(nameof(CodeInfo));
            }

            return Task.CompletedTask;
        }

        public override async void NavigatingFrom(NavigatingEventArgs args)
        {
            if (ClientService.AuthorizationState is AuthorizationStateWaitCode waitCode && !_confirmedGoBack)
            {
                args.Cancel = true;

                var message = string.Format(Strings.EditNumberInfo, Common.PhoneNumber.Format(waitCode.CodeInfo.PhoneNumber));
                var title = Strings.EditNumber;

                var confirm = await ShowPopupAsync(message, title, Strings.Edit, Strings.Close);
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
                Logger.Error(error.Message);

                if (error.MessageEquals(ErrorType.PHONE_NUMBER_INVALID))
                {
                    await ShowPopupAsync(error.Message, Strings.InvalidPhoneNumber, Strings.OK);
                }
                else if (error.MessageEquals(ErrorType.PHONE_CODE_EMPTY) || error.MessageEquals(ErrorType.PHONE_CODE_INVALID))
                {
                    await ShowPopupAsync(error.Message, Strings.InvalidCode, Strings.OK);
                }
                else if (error.MessageEquals(ErrorType.PHONE_CODE_EXPIRED))
                {
                    NavigationService.GoBack();
                    //NavigationService.Frame.ForwardStack.Clear();

                    await ShowPopupAsync(error.Message, Strings.CodeExpired, Strings.OK);
                }
                else if (error.MessageEquals(ErrorType.FIRSTNAME_INVALID))
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();

                    await ShowPopupAsync(error.Message, Strings.InvalidFirstName, Strings.OK);
                }
                else if (error.MessageEquals(ErrorType.LASTNAME_INVALID))
                {
                    NavigationService.GoBack();
                    //NavigationService.Frame.ForwardStack.Clear();

                    await ShowPopupAsync(error.Message, Strings.InvalidLastName, Strings.OK);
                }
                else if (error.Message.StartsWith("FLOOD_WAIT"))
                {
                    await ShowPopupAsync(Strings.FloodWait, Strings.AppName, Strings.OK);
                }
                else if (error.Code != -1000)
                {
                    await ShowPopupAsync(error.Message, Strings.AppName, Strings.OK);
                }
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