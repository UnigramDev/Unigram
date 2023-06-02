//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Views.Settings.Password;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings.Password
{
    public class SettingsPasswordHintViewModel : ViewModelBase
    {
        public SettingsPasswordHintViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private string _password;

        private string _hint;
        public string Hint
        {
            get => _hint;
            set => Set(ref _hint, value);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (state.TryGet("password", out string password))
            {
                _password = password;
            }

            return Task.CompletedTask;
        }

        public async void Continue()
        {
            var password = _password;
            var hint = _hint ?? string.Empty;

            if (string.Equals(password, hint))
            {
                await ShowPopupAsync(Strings.PasswordAsHintError, Strings.AppName, Strings.OK);
                return;
            }

            var state = new NavigationState
            {
                { "password", password },
                { "hint", hint }
            };

            NavigationService.Navigate(typeof(SettingsPasswordEmailPage), state: state);
        }
    }
}
