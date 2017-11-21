using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Account;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Views;
using Unigram.Views.SignIn;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.SignIn
{
    public class SignInPasswordViewModel : UnigramViewModelBase
    {
        private SignInPasswordPage.NavigationParameters _parameters;

        public SignInPasswordViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ForgotCommand = new RelayCommand(ForgotExecute);
            ResetCommand = new RelayCommand(ResetExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var parameters = parameter as SignInPasswordPage.NavigationParameters;
            if (parameters != null)
            {
                _parameters = parameters;

                if (parameters.Password is TLAccountPassword password)
                {
                    PasswordHint = password.Hint;
                }
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
            if (_parameters == null)
            {
                // TODO: ...
                return;
            }

            if (string.IsNullOrEmpty(_password))
            {
                RaisePropertyChanged("PASSWORD_INVALID");
                return;
            }

            var currentSalt = _parameters.Password.CurrentSalt;
            var hash = TLUtils.Combine(currentSalt, Encoding.UTF8.GetBytes(_password), currentSalt);

            var input = CryptographicBuffer.CreateFromByteArray(hash);
            var hasher = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);
            var hashed = hasher.HashData(input);
            CryptographicBuffer.CopyToByteArray(hashed, out byte[] data);

            var response = await ProtoService.CheckPasswordAsync(data);
            if (response.IsSucceeded)
            {
                SettingsHelper.IsAuthorized = true;
                SettingsHelper.UserId = response.Result.User.Id;
                ProtoService.CurrentUserId = response.Result.User.Id;
                ProtoService.SetInitState();

                // TODO: maybe ask about notifications?

                NavigationService.Navigate(typeof(MainPage));
            }
            else
            {
                TLUtils.WriteLog("auth.checkPassword error " + response.Error);

                if (response.Error.TypeEquals(TLErrorType.PASSWORD_HASH_INVALID))
                {
                    //await new MessageDialog(Resources.PasswordInvalidString, Resources.Error).ShowAsync();
                }
                else if (response.Error.CodeEquals(TLErrorCode.FLOOD))
                {
                    //await new MessageDialog($"{Resources.FloodWaitString}\r\n\r\n({result.Error.Message})", Resources.Error).ShowAsync();
                }

                Execute.ShowDebugMessage("account.checkPassword error " + response.Error);
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

            if (_parameters.Password.HasRecovery)
            {
                IsLoading = true;

                var response = await ProtoService.RequestPasswordRecoveryAsync();
                if (response.IsSucceeded)
                {
                    await TLMessageDialog.ShowAsync(string.Format(Strings.Android.RestoreEmailSent, response.Result.EmailPattern), Strings.Android.AppName, Strings.Android.OK);

                    // TODO: show recovery page
                }
                else if (response.Error != null)
                {
                    IsLoading = false;
                    await TLMessageDialog.ShowAsync(response.Error.ErrorMessage, Strings.Android.AppName, Strings.Android.OK);
                }
            }
            else
            {
                await TLMessageDialog.ShowAsync(Strings.Android.RestorePasswordNoEmailText, Strings.Android.RestorePasswordNoEmailTitle, Strings.Android.OK);
                IsResettable = true;
            }
        }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Android.ResetMyAccountWarningText, Strings.Android.ResetMyAccountWarning, Strings.Android.ResetMyAccountWarningReset, Strings.Android.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                IsLoading = true;

                var response = await ProtoService.DeleteAccountAsync("Forgot password");
                if (response.IsSucceeded)
                {
                    var logout = await ProtoService.LogOutAsync();

                    var state = new SignUpPage.NavigationParameters
                    {
                        PhoneNumber = _parameters.PhoneNumber,
                        PhoneCode = _parameters.PhoneCode,
                        Result = _parameters.Result,
                    };

                    NavigationService.Navigate(typeof(SignUpPage), state);
                }
                else if (response.Error != null)
                {
                    IsLoading = false;

                    if (response.Error.ErrorMessage.Contains("2FA_RECENT_CONFIRM"))
                    {
                        await TLMessageDialog.ShowAsync(Strings.Android.ResetAccountCancelledAlert, Strings.Android.AppName, Strings.Android.OK);
                    }
                    else if (response.Error.ErrorMessage.StartsWith("2FA_CONFIRM_WAIT_"))
                    {
                        // TODO: show info
                    }
                    else
                    {
                        await TLMessageDialog.ShowAsync(response.Error.ErrorMessage, Strings.Android.AppName, Strings.Android.OK);
                    }
                }
            }
        }
    }
}
