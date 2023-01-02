//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings.Password
{
    public class SettingsPasswordConfirmViewModel : TLViewModelBase
    {
        public SettingsPasswordConfirmViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            ResendCommand = new RelayCommand(ResendExecute);
            SendCommand = new RelayCommand(SendExecute);
        }

        private int _length;

        private string _pattern;
        public string Pattern
        {
            get => _pattern;
            set => Set(ref _pattern, value);
        }

        private string _code;
        public string Code
        {
            get => _code;
            set
            {
                Set(ref _code, value);

                if (_code.Length == _length && _length > 0)
                {
                    SendExecute();
                }
            }
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (state.TryGet("email", out string email))
            {
                Pattern = email;
            }
            else if (state.TryGet("pattern", out string pattern))
            {
                Pattern = pattern;
            }

            if (state.TryGet("length", out int length))
            {
                _length = length;
            }

            return Task.CompletedTask;
        }

        public RelayCommand ResendCommand { get; }
        private async void ResendExecute()
        {
            var response = await ClientService.SendAsync(new ResendRecoveryEmailAddressCode());
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var code = _code;
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            var response = await ClientService.SendAsync(new CheckRecoveryEmailAddressCode(code));
            if (response is PasswordState passwordState)
            {

            }
            else if (response is Error error)
            {

            }
        }
    }
}
