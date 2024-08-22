//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Authorization
{
    public class AuthorizationPasswordViewModel : ViewModelBase
    {
        private AuthorizationStateWaitPassword _parameters;

        public AuthorizationPasswordViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.AuthorizationState is AuthorizationStateWaitPassword waitPassword)
            {
                _parameters = waitPassword;
                PasswordHint = waitPassword.PasswordHint;
            }

            return Task.CompletedTask;
        }

        private string _passwordHint;
        public string PasswordHint
        {
            get => _passwordHint;
            set => Set(ref _passwordHint, value);
        }

        private string _password;
        public string Password
        {
            get => _password;
            set => Set(ref _password, value);
        }

        private bool _isResettable;
        public bool IsResettable
        {
            get => _isResettable;
            set => Set(ref _isResettable, value);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (string.IsNullOrEmpty(_password))
            {
                RaisePropertyChanged("PASSWORD_INVALID");
                return;
            }

            var response = await ClientService.SendAsync(new CheckAuthenticationPassword(_password));
            if (response is Error error)
            {
                Logger.Error(error.Message);

                if (error.MessageEquals(ErrorType.PASSWORD_HASH_INVALID))
                {
                    Password = string.Empty;
                    RaisePropertyChanged("PASSWORD_INVALID");
                }
                else if (error.CodeEquals(ErrorCode.FLOOD))
                {
                    AlertsService.ShowFloodWaitAlert(error.Message);
                }
            }
        }

        public async void Forgot()
        {
            if (_parameters == null)
            {
                // TODO: ...
                return;
            }

            if (_parameters.HasRecoveryEmailAddress)
            {
                IsLoading = true;

                var response = await ClientService.SendAsync(new RequestAuthenticationPasswordRecovery());
                if (response is Error error)
                {
                    IsLoading = false;
                    await ShowPopupAsync(error.Message, Strings.AppName, Strings.OK);
                }
            }
            else
            {
                await ShowPopupAsync(Strings.RestorePasswordNoEmailText, Strings.RestorePasswordNoEmailTitle, Strings.OK);
                IsResettable = true;
            }
        }

        public async void Reset()
        {
            var confirm = await ShowPopupAsync(Strings.ResetMyAccountWarningText, Strings.ResetMyAccountWarning, Strings.ResetMyAccountWarningReset, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                IsLoading = true;

                var response = await ClientService.SendAsync(new DeleteAccount("Forgot password", string.Empty));
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
                        await ShowPopupAsync(Strings.ResetAccountCancelledAlert, Strings.AppName, Strings.OK);
                    }
                    else if (error.Message.StartsWith("2FA_CONFIRM_WAIT_"))
                    {
                        // TODO: show info
                    }
                    else
                    {
                        await ShowPopupAsync(error.Message, Strings.AppName, Strings.OK);
                    }
                }
            }
        }
    }
}
