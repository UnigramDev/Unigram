//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsPhoneSentCodeViewModel : TLViewModelBase
    {
        private string _phoneNumber;

        public SettingsPhoneSentCodeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ResendCommand = new RelayCommand(ResendExecute, () => !IsLoading);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var authState = GetAuthorizationState();
            if (authState is AuthenticationCodeInfo codeInfo)
            {
                _phoneNumber = ClientService.Options.GetValue<string>("x_phonenumber");
                _codeInfo = codeInfo;

                RaisePropertyChanged(nameof(CodeInfo));
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
            get => _codeInfo;
            set => Set(ref _codeInfo, value);
        }

        private string _phoneCode;
        public string PhoneCode
        {
            get => _phoneCode;
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
            var response = await ClientService.SendAsync(new CheckChangePhoneNumberCode(_phoneCode));
            if (response is Ok)
            {
                while (NavigationService.Frame.BackStackDepth > 1)
                {
                    NavigationService.RemoveFromBackStack(1);
                }

                NavigationService.GoBack();
            }
            else if (response is Error error)
            {
                IsLoading = false;
                Logger.Error(error.Message);

                if (error.MessageEquals(ErrorType.PHONE_NUMBER_OCCUPIED))
                {
                    //await new MessageDialog(Resources.PhoneCodeInvalidString, Resources.Error).ShowAsync();
                }
                else if (error.MessageEquals(ErrorType.PHONE_CODE_INVALID))
                {
                    //await new MessageDialog(Resources.PhoneCodeInvalidString, Resources.Error).ShowAsync();
                }
                else if (error.MessageEquals(ErrorType.PHONE_CODE_EMPTY))
                {
                    //await new MessageDialog(Resources.PhoneCodeEmpty, Resources.Error).ShowAsync();
                }
                else if (error.MessageEquals(ErrorType.PHONE_CODE_EXPIRED))
                {
                    //await new MessageDialog(Resources.PhoneCodeExpiredString, Resources.Error).ShowAsync();
                }
                else if (error.MessageEquals(ErrorType.SESSION_PASSWORD_NEEDED))
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

            var response = await ClientService.SendAsync(function);
            if (response is AuthenticationCodeInfo info)
            {
                BootStrapper.Current.SessionState["x_codeinfo"] = info;
                NavigationService.Navigate(typeof(SettingsPhoneSentCodePage));
                NavigationService.Refresh();
            }
            else if (response is Error error)
            {

            }
        }
    }
}