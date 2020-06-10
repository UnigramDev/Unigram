using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Entities;
using Unigram.Services;
using Unigram.Views.Settings;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsPhoneSentCodeViewModel : TLViewModelBase
    {
        private string _phoneNumber;

        public SettingsPhoneSentCodeViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ResendCommand = new RelayCommand(ResendExecute, () => !IsLoading);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var authState = GetAuthorizationState();
            if (authState is AuthenticationCodeInfo codeInfo)
            {
                _phoneNumber = CacheService.Options.GetValue<string>("x_phonenumber");
                _codeInfo = codeInfo;

                RaisePropertyChanged(() => CodeInfo);
            }

            return Task.CompletedTask;
        }

        private AuthenticationCodeInfo GetAuthorizationState()
        {
            if (SessionState.TryGet("x_codeinfo", out AuthenticationCodeInfo codeInfo))
            {
                SessionState.Remove("x_codeinfo");
                return codeInfo;
            }

            return null;
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
                return _phoneNumber;
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


            //CheckChangePhoneNumberCode
            var response = await ProtoService.SendAsync(new CheckChangePhoneNumberCode(_phoneCode));
            if (response is Ok)
            {
                while (NavigationService.Frame.BackStackDepth > 1)
                {
                    NavigationService.Frame.BackStack.RemoveAt(1);
                }

                NavigationService.GoBack();
            }
            else if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.PHONE_NUMBER_OCCUPIED))
                {
                    //await new MessageDialog(Resources.PhoneCodeInvalidString, Resources.Error).ShowAsync();
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
                    ////this.IsWorking = true;
                    //var password = await LegacyService.GetPasswordAsync();
                    //if (password.IsSucceeded && password.Result is TLAccountPassword)
                    //{
                    //    var state = new SignInPasswordPage.NavigationParameters
                    //    {
                    //        PhoneNumber = _phoneNumber,
                    //        PhoneCode = _phoneCode,
                    //        //Result = _sentCode,
                    //        //Password = password.Result as TLAccountPassword
                    //    };

                    //    NavigationService.Navigate(typeof(SignInPasswordPage), state);
                    //}
                    //else
                    //{
                    //    Logs.Log.Write("account.getPassword error " + password.Error);
                    //}
                }
                else if (error.CodeEquals(ErrorCode.FLOOD))
                {
                    //await new MessageDialog($"{Resources.FloodWaitString}\r\n\r\n({error.Message})", Resources.Error).ShowAsync();
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

            var function = new ResendChangePhoneNumberCode();

            var response = await ProtoService.SendAsync(function);
            if (response is AuthenticationCodeInfo info)
            {
                App.Current.SessionState["x_codeinfo"] = info;
                NavigationService.Navigate(typeof(SettingsPhoneSentCodePage));
                NavigationService.Refresh();
            }
            else if (response is Error error)
            {

            }
        }
    }
}