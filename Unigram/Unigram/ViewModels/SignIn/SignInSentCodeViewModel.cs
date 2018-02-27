using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Telegram.Api.TL.Account;
using Telegram.Api.TL.Auth;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.SignIn;
using Windows.UI.Popups;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.SignIn
{
    public class SignInSentCodeViewModel : UnigramViewModelBase
    {
        private string _phoneNumber;

        public SignInSentCodeViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ResendCommand = new RelayCommand(ResendExecute, () => !IsLoading);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var authState = ProtoService.GetAuthorizationState();
            if (authState is AuthorizationStateWaitCode waitCode)
            {
                _phoneNumber = ProtoService.GetOption<OptionValueString>("x_phonenumber").Value;
                _sentCode = waitCode;

                RaisePropertyChanged(() => SentCode);
            }

            return Task.CompletedTask;
        }

        private AuthorizationStateWaitCode _sentCode;
        public AuthorizationStateWaitCode SentCode
        {
            get
            {
                return _sentCode;
            }
            set
            {
                Set(ref _sentCode, value);
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

                if (_sentCode != null && _sentCode.CodeInfo.Type is AuthenticationCodeTypeTelegramMessage appType)
                {
                    length = appType.Length;
                }
                else if (_sentCode != null && _sentCode.CodeInfo.Type is AuthenticationCodeTypeSms smsType)
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
                return _phoneNumber;
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_sentCode == null)
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

            var response = await ProtoService.SendAsync(new CheckAuthenticationCode(_phoneCode, "Yolo", string.Empty));
            if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(TLErrorType.PHONE_NUMBER_UNOCCUPIED))
                {
                    //var signup = await ProtoService.SignUpAsync(phoneNumber, phoneCodeHash, PhoneCode, "Paolo", "Veneziani");
                    //if (signup.IsSucceeded)
                    //{
                    //    ProtoService.SetInitState();
                    //    ProtoService.CurrentUserId = signup.Value.User.Id;
                    //    SettingsHelper.IsAuthorized = true;
                    //    SettingsHelper.UserId = signup.Value.User.Id;
                    //}

                    //this._callTimer.Stop();
                    //this.StateService.ClearNavigationStack = true;
                    //this.NavigationService.UriFor<SignUpViewModel>().Navigate();
                    //var state = new SignUpPage.NavigationParameters
                    //{
                    //    PhoneNumber = _phoneNumber,
                    //    PhoneCode = _phoneCode,
                    //    Result = _sentCode,
                    //};

                    //NavigationService.Navigate(typeof(SignUpPage), new SignUpPage.NavigationParameters
                    //{
                    //    PhoneNumber = _phoneNumber,
                    //    PhoneCode = _phoneCode,
                    //    Result = _sentCode,
                    //});
                }
                else if (error.TypeEquals(TLErrorType.PHONE_CODE_INVALID))
                {
                    //await new MessageDialog(Resources.PhoneCodeInvalidString, Resources.Error).ShowAsync();
                }
                else if (error.TypeEquals(TLErrorType.PHONE_CODE_EMPTY))
                {
                    //await new MessageDialog(Resources.PhoneCodeEmpty, Resources.Error).ShowAsync();
                }
                else if (error.TypeEquals(TLErrorType.PHONE_CODE_EXPIRED))
                {
                    //await new MessageDialog(Resources.PhoneCodeExpiredString, Resources.Error).ShowAsync();
                }
                else if (error.TypeEquals(TLErrorType.SESSION_PASSWORD_NEEDED))
                {
                    //this.IsWorking = true;
                    //var password = await LegacyService.GetPasswordAsync();
                    //if (password.IsSucceeded && password.Result is TLAccountPassword)
                    //{
                    //    var state = new SignInPasswordPage.NavigationParameters
                    //    {
                    //        PhoneNumber = _phoneNumber,
                    //        PhoneCode = _phoneCode,
                    //        Result = _sentCode,
                    //        Password = password.Result as TLAccountPassword
                    //    };

                    //    NavigationService.Navigate(typeof(SignInPasswordPage), state);
                    //}
                    //else
                    //{
                    //    Execute.ShowDebugMessage("account.getPassword error " + password.Error);
                    //}
                }
                else if (error.CodeEquals(TLErrorCode.FLOOD))
                {
                    //await new MessageDialog($"{Resources.FloodWaitString}\r\n\r\n({error.Message})", Resources.Error).ShowAsync();
                }

                Execute.ShowDebugMessage("account.signIn error " + error);
            }
        }

        public RelayCommand ResendCommand { get; }
        private async void ResendExecute()
        {
            if (_sentCode == null)
            {
                //...
                return;
            }

            if (_sentCode.CodeInfo.NextType == null)
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