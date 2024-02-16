//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings.Password;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsPasswordViewModel : ViewModelBase
    {
        private PasswordState _passwordState;
        private string _password;

        public SettingsPasswordViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is string password)
            {
                _password = password;
            }

            _passwordState = await ClientService.SendAsync(new GetPasswordState()) as PasswordState;
        }

        public async void Change()
        {
            var password = new SettingsPasswordCreatePopup();

            if (ContentDialogResult.Primary != await ShowPopupAsync(password))
            {
                return;
            }

            var hint = new SettingsPasswordHintPopup(ClientService, _password, password.Password);

            if (ContentDialogResult.Primary != await ShowPopupAsync(hint))
            {
                return;
            }

            _password = password.Password;
        }

        public async void ChangeEmail()
        {
            var popup = new SettingsPasswordEmailAddressPopup(ClientService, new SetRecoveryEmailAddress(_password, string.Empty));

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary && popup.PasswordState?.RecoveryEmailAddressCodeInfo != null)
            {
                await ShowPopupAsync(new SettingsPasswordEmailCodePopup(ClientService, popup.PasswordState?.RecoveryEmailAddressCodeInfo, SettingsPasswordEmailCodeType.New));
            }
        }

        public async void Disable()
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

            var confirm = await ShowPopupAsync(message, Strings.TurnPasswordOffQuestionTitle, Strings.OK, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new SetPassword(_password, string.Empty, string.Empty, false, string.Empty));
            if (response is PasswordState passwordState)
            {
                if (passwordState.HasPassword is false)
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();
                }
                else
                {

                }
            }
            else
            {

            }
        }
    }
}
