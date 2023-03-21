//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings.Password;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings.Password
{
    public class SettingsPasswordEmailViewModel : TLViewModelBase
    {
        public SettingsPasswordEmailViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        private string _password;
        private string _hint;

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (state.TryGet("password", out string password))
            {
                _password = password;
            }

            if (state.TryGet("hint", out string hint))
            {
                _hint = hint;
            }

            return Task.CompletedTask;
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var password = _password;
            var hint = _hint;
            var address = _address;
            var addressValid = true;

            if (string.IsNullOrEmpty(address) || !new EmailAddressAttribute().IsValid(address))
            {
                address = string.Empty;
                addressValid = false;

                var confirm = await ShowPopupAsync(Strings.YourEmailSkipWarningText, Strings.YourEmailSkipWarning, Strings.YourEmailSkip, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            var response = await ClientService.SendAsync(new SetPassword(string.Empty, password, hint, addressValid, address));
            if (response is PasswordState passwordState)
            {
                if (passwordState.RecoveryEmailAddressCodeInfo != null)
                {
                    var state = new NavigationState
                    {
                        { "email", address },
                        { "pattern", passwordState.RecoveryEmailAddressCodeInfo.EmailAddressPattern },
                        { "length", passwordState.RecoveryEmailAddressCodeInfo.Length }
                    };

                    NavigationService.Navigate(typeof(SettingsPasswordConfirmPage), state: state);
                }
                else
                {
                    NavigationService.Navigate(typeof(SettingsPasswordDonePage));
                }
            }
            else if (response is Error error)
            {
                if (error.TypeEquals(ErrorType.EMAIL_INVALID))
                {
                    await ShowPopupAsync(Strings.PasswordEmailInvalid, Strings.AppName, Strings.OK, Strings.Cancel);
                }
            }
        }
    }
}