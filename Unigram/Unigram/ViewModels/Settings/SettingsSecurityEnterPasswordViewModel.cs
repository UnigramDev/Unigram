using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Views;
using Unigram.Views.SignIn;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Unigram.Services;
using Telegram.Td.Api;

namespace Unigram.ViewModels.Settings
{
    public class SettingsSecurityEnterPasswordViewModel : UnigramViewModelBase
    {
        //private TLAccountPassword _passwordBase;

        public SettingsSecurityEnterPasswordViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
            ForgotCommand = new RelayCommand(ForgotExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //if (parameter is TLAccountPassword password)
            //{
            //    _passwordBase = password;
            //    PasswordHint = password.Hint;
            //}

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
            //if (_passwordBase == null)
            //{
            //    // TODO: ...
            //    return;
            //}

            //if (_password == null)
            //{
            //    await TLMessageDialog.ShowAsync("Please enter your password.");
            //    return;
            //}

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

            //    Execute.ShowDebugMessage("account.checkPassword error " + response.Error);
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

            //    var response = await ProtoService.SendAsync(new RequestPasswordRecovery());
            //    if (response is PasswordRecoveryInfo info)
            //    {
            //        await TLMessageDialog.ShowAsync(string.Format(Strings.Resources.RestoreEmailSent, info.RecoveryEmailAddressPattern), Strings.Resources.AppName, Strings.Resources.OK);
            //    }
            //    else if (response is Error error)
            //    {
            //        IsLoading = false;
            //        await new TLMessageDialog(error.Message ?? "Error message", error.Code.ToString()).ShowQueuedAsync();
            //    }
            //}
            //else
            //{
            //    await TLMessageDialog.ShowAsync(Strings.Resources.RestorePasswordNoEmailText, Strings.Resources.RestorePasswordNoEmailTitle, Strings.Resources.OK);
            //    //IsResettable = true;
            //}
        }
    }
}
