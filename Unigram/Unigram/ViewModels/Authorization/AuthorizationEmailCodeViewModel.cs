//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;

namespace Unigram.ViewModels.Authorization
{
    public class AuthorizationEmailCodeViewModel : TLViewModelBase
    {
        public AuthorizationEmailCodeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var authState = ClientService.GetAuthorizationState();
            if (authState is AuthorizationStateWaitEmailCode waitEmailCode)
            {
                CodeInfo = waitEmailCode.CodeInfo;

                RaisePropertyChanged(nameof(CodeInfo));
            }

            return Task.CompletedTask;
        }

        /*NextPhoneNumberAuthorizationDate
        {
            [MethodImpl(MethodImplOptions.InternalCall)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall)]
            [param: In]
            set;
        }

        //
        // Summary:
        //     Information about the sent authentication code.
        public extern EmailAddressAuthenticationCodeInfo*/

        private int _nextPhoneNumberAuthorizationDate;
        public int NextPhoneNumberAuthorizationDate
        {
            get => _nextPhoneNumberAuthorizationDate;
            set => Set(ref _nextPhoneNumberAuthorizationDate, value);
        }

        private EmailAddressAuthenticationCodeInfo _codeInfo;
        public EmailAddressAuthenticationCodeInfo CodeInfo
        {
            get => _codeInfo;
            set => Set(ref _codeInfo, value);
        }

        private string _code;
        public string Code
        {
            get => _code;
            set
            {
                Set(ref _code, value);

                var length = 5;

                if (_codeInfo != null)
                {
                    length = _codeInfo.Length;
                }

                if (_code.Length == length)
                {
                    SendExecute();
                }
            }
        }

        public string EmailAddressPattern => _codeInfo?.EmailAddressPattern;

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_codeInfo == null)
            {
                //...
                return;
            }

            if (string.IsNullOrEmpty(_code))
            {
                RaisePropertyChanged("CODE_INVALID");
                return;
            }

            IsLoading = true;

            var response = await ClientService.SendAsync(new CheckAuthenticationEmailCode(new EmailAddressAuthenticationCode(_code)));
            if (response is Error error)
            {
                IsLoading = false;

                if (error.TypeEquals(ErrorType.EMAIL_VERIFY_EXPIRED))
                {
                    await MessagePopup.ShowAsync(XamlRoot, Strings.Resources.CodeExpired, Strings.Resources.RestorePasswordNoEmailTitle, Strings.Resources.OK);
                }
                else if (error.TypeEquals(ErrorType.CODE_INVALID))
                {
                    await MessagePopup.ShowAsync(XamlRoot, Strings.Resources.InvalidCode, Strings.Resources.RestorePasswordNoEmailTitle, Strings.Resources.OK);
                }

                Logs.Logger.Error(Logs.LogTarget.API, "account.signIn error " + error);
            }
        }
    }
}