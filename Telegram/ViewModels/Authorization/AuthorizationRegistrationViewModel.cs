//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Authorization
{
    public class AuthorizationRegistrationViewModel : ViewModelBase
    {
        public AuthorizationRegistrationViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !IsLoading);
        }

        private string _firstName;
        public string FirstName
        {
            get => _firstName;
            set => Set(ref _firstName, value);
        }

        private string _lastName;
        public string LastName
        {
            get => _lastName;
            set => Set(ref _lastName, value);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            //if (_sentCode == null)
            //{
            //    //...
            //    return;
            //}

            if (string.IsNullOrEmpty(_firstName))
            {
                RaisePropertyChanged("FIRSTNAME_INVALID");
                return;
            }

            if (ClientService.AuthorizationState is AuthorizationStateWaitRegistration waitRegistration && waitRegistration.TermsOfService != null && waitRegistration.TermsOfService.ShowPopup)
            {
                async void CancelSignUp()
                {
                    var decline = await ShowPopupAsync(Strings.TosUpdateDecline, Strings.TermsOfService, Strings.DeclineDeactivate, Strings.Back);
                    if (decline != ContentDialogResult.Primary)
                    {
                        SendExecute();
                        return;
                    }

                    var delete = await ShowPopupAsync(Strings.TosDeclineDeleteAccount, Strings.AppName, Strings.Deactivate, Strings.Cancel);
                    if (delete != ContentDialogResult.Primary)
                    {
                        SendExecute();
                        return;
                    }

                    ClientService.Send(new LogOut());
                }

                var confirm = await ShowPopupAsync(waitRegistration.TermsOfService.Text, Strings.TermsOfService, Strings.SignUp, Strings.Decline);
                if (confirm != ContentDialogResult.Primary)
                {
                    CancelSignUp();
                    return;
                }

                if (waitRegistration.TermsOfService.MinUserAge > 0)
                {
                    var age = await ShowPopupAsync(string.Format(Strings.TosAgeText, waitRegistration.TermsOfService.MinUserAge), Strings.TosAgeTitle, Strings.Agree, Strings.Cancel);
                    if (age != ContentDialogResult.Primary)
                    {
                        CancelSignUp();
                        return;
                    }
                }
            }

            var response = await ClientService.SendAsync(new RegisterUser(_firstName ?? string.Empty, _lastName ?? string.Empty, false));
            if (response is Error error)
            {

            }

            //var phoneNumber = _phoneNumber;
            //var phoneCodeHash = _sentCode.PhoneCodeHash;

            //IsLoading = true;

            //var response = await LegacyService.SignUpAsync(phoneNumber, phoneCodeHash, _phoneCode, _firstName, _lastName);
            //if (response.IsSucceeded)
            //{
            //    // TODO: maybe ask about notifications?

            //    NavigationService.Navigate(typeof(MainPage));
            //}
            //else if (response.Error != null)
            //{
            //    IsLoading = false;

            //    if (response.Error.TypeEquals(TLErrorType.PHONE_NUMBER_INVALID))
            //    {
            //        await ShowPopupAsync(Strings.InvalidPhoneNumber, Strings.AppName, Strings.OK);
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.PHONE_CODE_EMPTY) || response.Error.TypeEquals(TLErrorType.PHONE_CODE_INVALID))
            //    {
            //        await ShowPopupAsync(Strings.InvalidCode, Strings.AppName, Strings.OK);
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.PHONE_CODE_EXPIRED))
            //    {
            //        await ShowPopupAsync(Strings.CodeExpired, Strings.AppName, Strings.OK);
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.FIRSTNAME_INVALID))
            //    {
            //        await ShowPopupAsync(Strings.InvalidFirstName, Strings.AppName, Strings.OK);
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.LASTNAME_INVALID))
            //    {
            //        await ShowPopupAsync(Strings.InvalidLastName, Strings.AppName, Strings.OK);
            //    }
            //    else
            //    {
            //        await ShowPopupAsync(response.Error.ErrorMessage, Strings.AppName, Strings.OK);
            //    }
            //}
        }
    }
}
