using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Authorization
{
    public class AuthorizationEmailCodeViewModel : TLViewModelBase
    {
        public AuthorizationEmailCodeViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var authState = ProtoService.GetAuthorizationState();
            if (authState is AuthorizationStateWaitEmailCode waitEmailCode)
            {
                CodeInfo = waitEmailCode.CodeInfo;

                RaisePropertyChanged(nameof(CodeInfo));
            }

            return Task.CompletedTask;
        }

        /*NextPhoneNumberAuthorizationDate
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            set;
        }

        //
        // Summary:
        //     Information about the sent authentication code.
        public extern EmailAddressAuthenticationCodeInfo*/

        private int _nextPhoneNumberAuthorizationDate;
        public int NextPhoneNumberAuthorizationDate
        {
            get => _nextPhoneNumberAuthorizationDate;
            set => Set(ref _nextPhoneNumberAuthorizationDate, value);
        }

        private EmailAddressAuthenticationCodeInfo _codeInfo;
        public EmailAddressAuthenticationCodeInfo CodeInfo
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

                var length = 5;

                if (_codeInfo != null)
                {
                    length = _codeInfo.Length;
                }

                if (_code.Length == length)
                {
                    SendExecute();
                }
            }
        }

        public string EmailAddressPattern => _codeInfo?.EmailAddressPattern;

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
                RaisePropertyChanged("CODE_INVALID");
                return;
            }

            IsLoading = true;

            var response = await ProtoService.SendAsync(new CheckAuthenticationEmailCode(new EmailAddressAuthenticationCode(_code)));
            if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.EMAIL_VERIFY_EXPIRED))
                {
                    await MessagePopup.ShowAsync(Strings.Resources.CodeExpired, Strings.Resources.RestorePasswordNoEmailTitle, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.CODE_INVALID))
                {
                    await MessagePopup.ShowAsync(Strings.Resources.InvalidCode, Strings.Resources.RestorePasswordNoEmailTitle, Strings.Resources.OK);
                }

                Logs.Logger.Error(Logs.LogTarget.API, "account.signIn error " + error);
            }
        }
    }
}