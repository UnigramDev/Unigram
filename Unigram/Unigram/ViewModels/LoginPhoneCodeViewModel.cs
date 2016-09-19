﻿using GalaSoft.MvvmLight.Command;
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
using Unigram.Views;
using Windows.UI.Popups;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class LoginPhoneCodeViewModel : UnigramViewModelBase
    {
        private NavigationParameter _sentCode;

        public LoginPhoneCodeViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            _sentCode = (NavigationParameter)parameter;
            return Task.CompletedTask;
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
                if (_phoneCode.Length == 5)
                {
                    SendExecute();
                }
            }
        }

        public RelayCommand SendCommand => new RelayCommand(SendExecute);
        private async void SendExecute()
        {
            var phoneNumber = _sentCode.PhoneNumber;
            var phoneCodeHash = _sentCode.PhoneCodeHash;

            var result = await ProtoService.SignInAsync(phoneNumber, phoneCodeHash, PhoneCode);
            if (result?.IsSucceeded == true)
            {
                ProtoService.SetInitState();
                ProtoService.CurrentUserId = result.Value.User.Id;
                SettingsHelper.IsAuthorized = true;
                SettingsHelper.UserId = result.Value.User.Id;

                // TODO: maybe ask about notifications?

                NavigationService.Navigate(typeof(MainPage));
            }
            else
            {
                if (result.Error.TypeEquals(TLErrorType.PHONE_NUMBER_UNOCCUPIED))
                {
                    //this._callTimer.Stop();
                    //this.StateService.ClearNavigationStack = true;
                    //this.NavigationService.UriFor<SignUpViewModel>().Navigate();
                }
                else if (result.Error.TypeEquals(TLErrorType.PHONE_CODE_INVALID))
                {
                    //await new MessageDialog(Resources.PhoneCodeInvalidString, Resources.Error).ShowAsync();
                }
                else if (result.Error.TypeEquals(TLErrorType.PHONE_CODE_EMPTY))
                {
                    //await new MessageDialog(Resources.PhoneCodeEmpty, Resources.Error).ShowAsync();
                }
                else if (result.Error.TypeEquals(TLErrorType.PHONE_CODE_EXPIRED))
                {
                    //await new MessageDialog(Resources.PhoneCodeExpiredString, Resources.Error).ShowAsync();
                }
                else if (result.Error.TypeEquals(TLErrorType.SESSION_PASSWORD_NEEDED))
                {
                    //this.IsWorking = true;
                    var password = await ProtoService.GetPasswordAsync();
                    if (password?.IsSucceeded == true)
                    {
                        NavigationService.Navigate(typeof(LoginPasswordPage), password.Value);
                    }
                    else
                    {
                        Execute.ShowDebugMessage("account.getPassword error " + password.Error);
                    }
                }
                else if (result.Error.CodeEquals(TLErrorCode.FLOOD))
                {
                    //await new MessageDialog($"{Resources.FloodWaitString}\r\n\r\n({error.Message})", Resources.Error).ShowAsync();
                }

                Execute.ShowDebugMessage("account.signIn error " + result.Error);
            }
        }

        public class NavigationParameter
        {
            public string PhoneNumber { get; set; }

            public string PhoneCodeHash { get; set; }
        }
    }
}