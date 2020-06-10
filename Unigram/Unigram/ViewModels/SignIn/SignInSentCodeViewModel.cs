using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.SignIn
{
    public class SignInSentCodeViewModel : TLViewModelBase
    {
        public SignInSentCodeViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ResendCommand = new RelayCommand(ResendExecute, () => !IsLoading);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var authState = ProtoService.GetAuthorizationState();
            if (authState is AuthorizationStateWaitCode waitCode)
            {
                _codeInfo = waitCode.CodeInfo;

                RaisePropertyChanged(() => CodeInfo);
            }

            return Task.CompletedTask;
        }

        private AuthenticationCodeInfo _codeInfo;
        public AuthenticationCodeInfo CodeInfo
        {
            get
            {
                return _codeInfo;
            }
            set
            {
                Set(ref _codeInfo, value);
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

                var length = 5;

                if (_codeInfo != null && _codeInfo.Type is AuthenticationCodeTypeTelegramMessage appType)
                {
                    length = appType.Length;
                }
                else if (_codeInfo != null && _codeInfo.Type is AuthenticationCodeTypeSms smsType)
                {
                    length = smsType.Length;
                }

                if (_phoneCode.Length == length)
                {
                    SendExecute();
                }
            }
        }

        public string PhoneNumber
        {
            get
            {
                return _codeInfo?.PhoneNumber;
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_codeInfo == null)
            {
                //...
                return;
            }

            if (string.IsNullOrEmpty(_phoneCode))
            {
                RaisePropertyChanged("SENT_CODE_INVALID");
                return;
            }

            IsLoading = true;

            var response = await ProtoService.SendAsync(new CheckAuthenticationCode(_phoneCode));
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

                Logs.Logger.Error(Logs.Target.API, "account.signIn error " + error);
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

            var response = await ProtoService.SendAsync(function);
            if (response is Error error)
            {

            }
        }
    }
}