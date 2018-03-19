using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.SignIn;
using Windows.UI.Popups;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.SignIn
{
    public class SignInSentCodeViewModel : UnigramViewModelBase
    {
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

            var firstName = string.Empty;
            var lastName = string.Empty;

            if (ProtoService.TryGetOption("x_firstname", out OptionValueString firstValue))
            {
                firstName = firstValue.Value;
            }
            
            if (ProtoService.TryGetOption("x_lastname", out OptionValueString lastValue))
            {
                lastName = lastValue.Value;
            }

            var response = await ProtoService.SendAsync(new CheckAuthenticationCode(_phoneCode, firstName, lastName));
            if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.PHONE_NUMBER_UNOCCUPIED))
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
                else if (error.TypeEquals(ErrorType.PHONE_CODE_INVALID))
                {
                    //await new MessageDialog(Resources.PhoneCodeInvalidString, Resources.Error).ShowAsync();
                }
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EMPTY))
                {
                    //await new MessageDialog(Resources.PhoneCodeEmpty, Resources.Error).ShowAsync();
                }
                else if (error.TypeEquals(ErrorType.PHONE_CODE_EXPIRED))
                {
                    //await new MessageDialog(Resources.PhoneCodeExpiredString, Resources.Error).ShowAsync();
                }
                else if (error.TypeEquals(ErrorType.SESSION_PASSWORD_NEEDED))
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
                else if (error.CodeEquals(ErrorCode.FLOOD))
                {
                    //await new MessageDialog($"{Resources.FloodWaitString}\r\n\r\n({error.Message})", Resources.Error).ShowAsync();
                }

                Execute.ShowDebugMessage("account.signIn error " + error);
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