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
using Telegram.Api.TL.Auth;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Views;
using Unigram.Views.SignIn;
using Windows.UI.Popups;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.SignIn
{
    public class SignUpViewModel : UnigramViewModelBase
    {
        private string _phoneNumber;
        private string _phoneCode;
        private TLAuthSentCode _sentCode;

        public SignUpViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var param = parameter as SignUpPage.NavigationParameters;
            if (param != null)
            {
                _phoneNumber = param.PhoneNumber;
                _phoneCode = param.PhoneCode;
                _sentCode = param.Result;
            }

            return Task.CompletedTask;
        }

        private string _firstName;
        public string FirstName
        {
            get
            {
                return _firstName;
            }
            set
            {
                Set(ref _firstName, value);
            }
        }

        private string _lastName;
        public string LastName
        {
            get
            {
                return _lastName;
            }
            set
            {
                Set(ref _lastName, value);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (_sentCode == null)
            {
                //...
                return;
            }

            if (_firstName == null)
            {
                RaisePropertyChanged("FIRSTNAME_INVALID");
                return;
            }

            var phoneNumber = _phoneNumber;
            var phoneCodeHash = _sentCode.PhoneCodeHash;

            IsLoading = true;

            var response = await ProtoService.SignUpAsync(phoneNumber, phoneCodeHash, _phoneCode, _firstName, _lastName);
            if (response.IsSucceeded)
            {
                SettingsHelper.IsAuthorized = true;
                SettingsHelper.UserId = response.Result.User.Id;
                ProtoService.CurrentUserId = response.Result.User.Id;
                ProtoService.SetInitState();

                // TODO: maybe ask about notifications?

                NavigationService.Navigate(typeof(MainPage));
            }
            else if (response.Error != null)
            {
                IsLoading = false;

                if (response.Error.TypeEquals(TLErrorType.PHONE_NUMBER_INVALID))
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.InvalidPhoneNumber, Strings.Android.AppName, Strings.Android.OK);
                }
                else if (response.Error.TypeEquals(TLErrorType.PHONE_CODE_EMPTY) || response.Error.TypeEquals(TLErrorType.PHONE_CODE_INVALID))
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.InvalidCode, Strings.Android.AppName, Strings.Android.OK);
                }
                else if (response.Error.TypeEquals(TLErrorType.PHONE_CODE_EXPIRED))
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.CodeExpired, Strings.Android.AppName, Strings.Android.OK);
                }
                else if (response.Error.TypeEquals(TLErrorType.FIRSTNAME_INVALID))
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.InvalidFirstName, Strings.Android.AppName, Strings.Android.OK);
                }
                else if (response.Error.TypeEquals(TLErrorType.LASTNAME_INVALID))
                {
                    await TLMessageDialog.ShowAsync(Strings.Android.InvalidLastName, Strings.Android.AppName, Strings.Android.OK);
                }
                else
                {
                    await TLMessageDialog.ShowAsync(response.Error.ErrorMessage, Strings.Android.AppName, Strings.Android.OK);
                }
            }
        }
    }
}
