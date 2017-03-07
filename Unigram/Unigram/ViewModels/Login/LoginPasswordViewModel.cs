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
using Unigram.Common;
using Unigram.Views;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Login
{
    public class LoginPasswordViewModel : UnigramViewModelBase
    {
        private TLAccountPassword _password;

        public LoginPasswordViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var password = parameter as TLAccountPasswordBase;
            if (password != null)
            {
                _password = password as TLAccountPassword;

                if (_password != null)
                {
                    PasswordHint = _password.Hint;
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

        private string _code;
        public string Code
        {
            get
            {
                return _code;
            }
            set
            {
                Set(ref _code, value);
            }
        }

        public RelayCommand SendCommand => new RelayCommand(SendExecute);
        private async void SendExecute()
        {
            var currentSalt = _password.CurrentSalt;
            var hash = TLUtils.Combine(currentSalt, Encoding.UTF8.GetBytes(Code), currentSalt);

            var input = CryptographicBuffer.CreateFromByteArray(hash);
            var hasher = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);
            var hashed = hasher.HashData(input);
            CryptographicBuffer.CopyToByteArray(hashed, out byte[] data);

            var result = await ProtoService.CheckPasswordAsync(data);
            if (result?.IsSucceeded == true)
            {
                ProtoService.SetInitState();
                ProtoService.CurrentUserId = result.Result.User.Id;
                SettingsHelper.IsAuthorized = true;
                SettingsHelper.UserId = result.Result.User.Id;

                // TODO: maybe ask about notifications?

                NavigationService.Navigate(typeof(MainPage));
            }
            else
            {
                TLUtils.WriteLog("auth.checkPassword error " + result.Error);

                if (result.Error.TypeEquals(TLErrorType.PASSWORD_HASH_INVALID))
                {
                    //await new MessageDialog(Resources.PasswordInvalidString, Resources.Error).ShowAsync();
                }
                else if (result.Error.CodeEquals(TLErrorCode.FLOOD))
                {
                    //await new MessageDialog($"{Resources.FloodWaitString}\r\n\r\n({result.Error.Message})", Resources.Error).ShowAsync();
                }

                Execute.ShowDebugMessage("account.checkPassword error " + result.Error);
            }
        }
    }
}
