//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Authorization
{
    public class AuthorizationRegistrationViewModel : TLViewModelBase
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

            var state = ClientService.GetAuthorizationState();
            if (state is AuthorizationStateWaitRegistration waitRegistration && waitRegistration.TermsOfService != null && waitRegistration.TermsOfService.ShowPopup)
            {
                async void CancelSignUp()
                {
                    var decline = await MessagePopup.ShowAsync(Strings.Resources.TosUpdateDecline, Strings.Resources.TermsOfService, Strings.Resources.DeclineDeactivate, Strings.Resources.Back);
                    if (decline != ContentDialogResult.Primary)
                    {
                        SendExecute();
                        return;
                    }

                    var delete = await MessagePopup.ShowAsync(Strings.Resources.TosDeclineDeleteAccount, Strings.Resources.AppName, Strings.Resources.Deactivate, Strings.Resources.Cancel);
                    if (delete != ContentDialogResult.Primary)
                    {
                        SendExecute();
                        return;
                    }

                    ClientService.Send(new LogOut());
                }

                var confirm = await MessagePopup.ShowAsync(waitRegistration.TermsOfService.Text, Strings.Resources.TermsOfService, Strings.Resources.SignUp, Strings.Resources.Decline);
                if (confirm != ContentDialogResult.Primary)
                {
                    CancelSignUp();
                    return;
                }

                if (waitRegistration.TermsOfService.MinUserAge > 0)
                {
                    var age = await MessagePopup.ShowAsync(string.Format(Strings.Resources.TosAgeText, waitRegistration.TermsOfService.MinUserAge), Strings.Resources.TosAgeTitle, Strings.Resources.Agree, Strings.Resources.Cancel);
                    if (age != ContentDialogResult.Primary)
                    {
                        CancelSignUp();
                        return;
                    }
                }
            }

            var response = await ClientService.SendAsync(new RegisterUser(_firstName ?? string.Empty, _lastName ?? string.Empty));
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
            //        await MessagePopup.ShowAsync(Strings.Resources.InvalidPhoneNumber, Strings.Resources.AppName, Strings.Resources.OK);
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.PHONE_CODE_EMPTY) || response.Error.TypeEquals(TLErrorType.PHONE_CODE_INVALID))
            //    {
            //        await MessagePopup.ShowAsync(Strings.Resources.InvalidCode, Strings.Resources.AppName, Strings.Resources.OK);
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.PHONE_CODE_EXPIRED))
            //    {
            //        await MessagePopup.ShowAsync(Strings.Resources.CodeExpired, Strings.Resources.AppName, Strings.Resources.OK);
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.FIRSTNAME_INVALID))
            //    {
            //        await MessagePopup.ShowAsync(Strings.Resources.InvalidFirstName, Strings.Resources.AppName, Strings.Resources.OK);
            //    }
            //    else if (response.Error.TypeEquals(TLErrorType.LASTNAME_INVALID))
            //    {
            //        await MessagePopup.ShowAsync(Strings.Resources.InvalidLastName, Strings.Resources.AppName, Strings.Resources.OK);
            //    }
            //    else
            //    {
            //        await MessagePopup.ShowAsync(response.Error.ErrorMessage, Strings.Resources.AppName, Strings.Resources.OK);
            //    }
            //}
        }
    }
}
