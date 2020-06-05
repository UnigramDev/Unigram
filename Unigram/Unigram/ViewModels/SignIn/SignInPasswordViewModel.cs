using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Entities;
using Unigram.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.SignIn
{
    public class SignInPasswordViewModel : TLViewModelBase
    {
        private AuthorizationStateWaitPassword _parameters;

        public SignInPasswordViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ForgotCommand = new RelayCommand(ForgotExecute);
            ResetCommand = new RelayCommand(ResetExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var authState = ProtoService.GetAuthorizationState();
            if (authState is AuthorizationStateWaitPassword waitPassword)
            {
                _parameters = waitPassword;
                PasswordHint = waitPassword.PasswordHint;
            }

            return Task.CompletedTask;
        }

        private string _passwordHint;
        public string PasswordHint
        {
            get
            {
                return _passwordHint;
            }
            set
            {
                Set(ref _passwordHint, value);
            }
        }

        private string _password;
        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                Set(ref _password, value);
            }
        }

        private bool _isResettable;
        public bool IsResettable
        {
            get
            {
                return _isResettable;
            }
            set
            {
                Set(ref _isResettable, value);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (string.IsNullOrEmpty(_password))
            {
                RaisePropertyChanged("PASSWORD_INVALID");
                return;
            }

            var response = await ProtoService.SendAsync(new CheckAuthenticationPassword(_password));
            if (response is Error error)
            {
                if (error.TypeEquals(ErrorType.PASSWORD_HASH_INVALID))
                {
                    Password = string.Empty;
                    RaisePropertyChanged("PASSWORD_INVALID");
                }
                else if (error.CodeEquals(ErrorCode.FLOOD))
                {
                    AlertsService.ShowFloodWaitAlert(error.Message);
                }

                Logs.Logger.Error(Logs.Target.API, "account.checkPassword error " + error);
            }
        }

        public RelayCommand ForgotCommand { get; }
        private async void ForgotExecute()
        {
            if (_parameters == null)
            {
                // TODO: ...
                return;
            }

            if (_parameters.HasRecoveryEmailAddress)
            {
                IsLoading = true;

                var response = await ProtoService.SendAsync(new RequestAuthenticationPasswordRecovery());
                if (response is Error error)
                {
                    IsLoading = false;
                    await MessagePopup.ShowAsync(error.Message, Strings.Resources.AppName, Strings.Resources.OK);
                }
            }
            else
            {
                await MessagePopup.ShowAsync(Strings.Resources.RestorePasswordNoEmailText, Strings.Resources.RestorePasswordNoEmailTitle, Strings.Resources.OK);
                IsResettable = true;
            }
        }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ResetMyAccountWarningText, Strings.Resources.ResetMyAccountWarning, Strings.Resources.ResetMyAccountWarningReset, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                IsLoading = true;

                var response = await ProtoService.SendAsync(new DeleteAccount("Forgot password"));
                if (response is Ok)
                {
                    //var logout = await LegacyService.LogOutAsync();

                    //var state = new SignUpPage.NavigationParameters
                    //{
                    //    PhoneNumber = _parameters.PhoneNumber,
                    //    PhoneCode = _parameters.PhoneCode,
                    //    Result = _parameters.Result,
                    //};

                    //NavigationService.Navigate(typeof(SignUpPage), state);
                }
                else if (response is Error error)
                {
                    IsLoading = false;

                    if (error.Message.Contains("2FA_RECENT_CONFIRM"))
                    {
                        await MessagePopup.ShowAsync(Strings.Resources.ResetAccountCancelledAlert, Strings.Resources.AppName, Strings.Resources.OK);
                    }
                    else if (error.Message.StartsWith("2FA_CONFIRM_WAIT_"))
                    {
                        // TODO: show info
                    }
                    else
                    {
                        await MessagePopup.ShowAsync(error.Message, Strings.Resources.AppName, Strings.Resources.OK);
                    }
                }
            }
        }
    }
}
