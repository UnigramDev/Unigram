//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public enum SettingsPasswordState
    {
        Confirm,
        Create,
        Manage,
        Input,
        Recovery
    }

    public class SettingsPasswordViewModel : TLViewModelBase
    {
        private PasswordState _passwordState;

        //private TLAccountPassword _passwordBase;

        public SettingsPasswordViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Input = new InputViewModel(this, clientService, settingsService, aggregator);

            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ForgotCommand = new RelayCommand(ForgotExecute);
            EnableCommand = new RelayCommand(EnableExecute);
            DisableCommand = new RelayCommand(DisableExecute);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetPasswordState());
            if (response is PasswordState passwordState)
            {
                Update(passwordState, false);
            }

            //if (parameter is TLAccountPassword password)
            //{
            //    _passwordBase = password;
            //    PasswordHint = password.Hint;
            //}

        }

        private void Update(PasswordState passwordState, bool justSet)
        {
            _passwordState = passwordState;

            if (passwordState.HasPassword && !justSet)
            {
                State = SettingsPasswordState.Confirm;
                PasswordHint = passwordState.PasswordHint;
            }
            else if (justSet)
            {
                State = SettingsPasswordState.Manage;
            }
            else
            {
                State = SettingsPasswordState.Create;
            }
        }

        public InputViewModel Input { get; private set; }

        private SettingsPasswordState _state;
        public SettingsPasswordState State
        {
            get => _state;
            set => Set(ref _state, value);
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

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            //if (_passwordBase == null)
            //{
            //    // TODO: ...
            //    return;
            //}

            if (string.IsNullOrEmpty(_password))
            {
                await ShowPopupAsync("Please enter your password.");
                return;
            }

            var response = await ClientService.SendAsync(new GetRecoveryEmailAddress(_password));
            if (response is RecoveryEmailAddress)
            {
                State = SettingsPasswordState.Manage;
            }

            //var response = await LegacyService.CheckPasswordAsync(data);
            //if (response.IsSucceeded)
            //{
            //    // TODO: maybe ask about notifications?

            //    NavigationService.Navigate(typeof(MainPage));
            //}
            //else
            //{
            //    if (response.Error.TypeEquals(TLErrorType.PASSWORD_HASH_INVALID))
            //    {
            //        //await new MessageDialog(Resources.PasswordInvalidString, Resources.Error).ShowAsync();
            //    }
            //    else if (response.Error.CodeEquals(TLErrorCode.FLOOD))
            //    {
            //        //await new MessageDialog($"{Resources.FloodWaitString}\r\n\r\n({result.Error.Message})", Resources.Error).ShowAsync();
            //    }

            //    Logs.Log.Write("account.checkPassword error " + response.Error);
            //}
        }

        public RelayCommand ForgotCommand { get; }
        private async void ForgotExecute()
        {
            //if (_passwordBase == null)
            //{
            //    // TODO: ...
            //    return;
            //}

            //if (_passwordBase.HasRecovery)
            //{
            //    IsLoading = true;

            //    var response = await ClientService.SendAsync(new RequestPasswordRecovery());
            //    if (response is PasswordRecoveryInfo info)
            //    {
            //        await ShowPopupAsync(string.Format(Strings.RestoreEmailSent, info.RecoveryEmailAddressPattern), Strings.AppName, Strings.OK);
            //    }
            //    else if (response is Error error)
            //    {
            //        IsLoading = false;
            //        await ShowPopupAsync(new MessagePopup(error.Message ?? "Error message", error.Code.ToString()));
            //    }
            //}
            //else
            //{
            //    await ShowPopupAsync(Strings.RestorePasswordNoEmailText, Strings.RestorePasswordNoEmailTitle, Strings.OK);
            //    //IsResettable = true;
            //}
        }

        #region Manage

        public RelayCommand EnableCommand { get; }
        private void EnableExecute()
        {
            State = SettingsPasswordState.Input;
        }

        public RelayCommand DisableCommand { get; }
        private async void DisableExecute()
        {
            var state = _passwordState;
            if (state == null)
            {
                return;
            }

            var message = Strings.TurnPasswordOffQuestion;
            if (state.HasPassportData)
            {
                message += Environment.NewLine + Environment.NewLine + Strings.TurnPasswordOffPassport;
            }

            var confirm = await ShowPopupAsync(message, Strings.AppName, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new SetPassword(_password, string.Empty, string.Empty, false, string.Empty));
            if (response is PasswordState passwordState)
            {
                Update(passwordState, false);
            }
            else
            {

            }
        }

        #endregion

        public class InputViewModel : TLViewModelBase
        {
            private readonly SettingsPasswordViewModel _viewModel;

            public InputViewModel(SettingsPasswordViewModel viewModel, IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
                : base(clientService, settingsService, aggregator)
            {
                _viewModel = viewModel;

                SendCommand = new RelayCommand(SendExecute);
            }

            private string _password;
            public string Password
            {
                get => _password;
                set => Set(ref _password, value);
            }

            private string _passwordRetype;
            public string PasswordRetype
            {
                get => _passwordRetype;
                set => Set(ref _passwordRetype, value);
            }

            private string _passwordHint;
            public string PasswordHint
            {
                get => _passwordHint;
                set => Set(ref _passwordHint, value);
            }

            private string _emailAddress;
            public string EmailAddress
            {
                get => _emailAddress;
                set => Set(ref _emailAddress, value);
            }

            public RelayCommand SendCommand { get; }
            private async void SendExecute()
            {
                var oldPassword = _viewModel.Password ?? string.Empty;
                var password = _password ?? string.Empty;
                var passwordRetype = _passwordRetype ?? string.Empty;
                var passwordHint = _passwordHint ?? string.Empty;
                var emailAddress = _emailAddress ?? string.Empty;
                var emailValid = true;

                if (string.IsNullOrWhiteSpace(password))
                {
                    // Error
                    return;
                }

                if (!string.Equals(password, passwordRetype))
                {
                    // Error
                    await ShowPopupAsync(Strings.PasswordDoNotMatch, Strings.AppName, Strings.OK);
                    return;
                }

                if (string.IsNullOrEmpty(emailAddress) || !new EmailAddressAttribute().IsValid(emailAddress))
                {
                    emailValid = false;
                    emailAddress = string.Empty;

                    var confirm = await ShowPopupAsync(Strings.YourEmailSkipWarningText, Strings.YourEmailSkipWarning, Strings.YourEmailSkip, Strings.Cancel);
                    if (confirm != ContentDialogResult.Primary)
                    {
                        return;
                    }
                }

                var response = await ClientService.SendAsync(new SetPassword(oldPassword, password, passwordHint, emailValid, emailAddress));
                if (response is PasswordState passwordState)
                {
                    _viewModel.Update(passwordState, true);
                }
                else if (response is Error error)
                {
                    if (error.TypeEquals(ErrorType.EMAIL_UNCONFIRMED))
                    {

                    }
                }
            }
        }
    }
}
