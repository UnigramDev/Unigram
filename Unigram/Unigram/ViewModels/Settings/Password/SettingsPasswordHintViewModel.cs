//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views.Settings.Password;

namespace Unigram.ViewModels.Settings.Password
{
    public class SettingsPasswordHintViewModel : TLViewModelBase
    {
        public SettingsPasswordHintViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
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

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var password = _password;
            var hint = _hint ?? string.Empty;

            if (string.Equals(password, hint))
            {
                await MessagePopup.ShowAsync(XamlRoot, Strings.Resources.PasswordAsHintError, Strings.Resources.AppName, Strings.Resources.OK);
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
