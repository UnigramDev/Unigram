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

namespace Unigram.ViewModels.Settings
{
    public class SettingsSecurityEnterPasswordViewModel : UnigramViewModelBase
    {
        private TLAccountPassword _passwordBase;

        public SettingsSecurityEnterPasswordViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ForgotCommand = new RelayCommand(ForgotExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is TLAccountPassword password)
            {
                _passwordBase = password;
                PasswordHint = password.Hint;
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

        private RelayCommand _sendCommand;
        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_passwordBase == null)
            {
                // TODO: ...
                return;
            }

            if (_password == null)
            {
                await TLMessageDialog.ShowAsync("Please enter your password.");
                return;
            }

            var currentSalt = _passwordBase.CurrentSalt;
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
            if (_passwordBase == null)
            {
                // TODO: ...
                return;
            }

            if (_passwordBase.HasRecovery)
            {
                IsLoading = true;

                var response = await ProtoService.RequestPasswordRecoveryAsync();
                if (response.IsSucceeded)
                {
                    await TLMessageDialog.ShowAsync(string.Format("We have sent a recovery code to the e-mail you provided:\n\n{0}", response.Result.EmailPattern), "Telegram", "OK");
                }
                else if (response.Error != null)
                {
                    IsLoading = false;
                    await new TLMessageDialog(response.Error.ErrorMessage ?? "Error message", response.Error.ErrorCode.ToString()).ShowQueuedAsync();
                }
            }
            else
            {
                await TLMessageDialog.ShowAsync("Since you haven't provided a recovery e-mail when setting up your password, your remaining options are either to remember your password or to reset your account.", "Sorry", "OK");
                //IsResettable = true;
            }
        }
    }
}
