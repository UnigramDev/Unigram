using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings.Password
{
    public class SettingsPasswordConfirmViewModel : TLViewModelBase
    {
        public SettingsPasswordConfirmViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
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

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
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

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public RelayCommand ResendCommand { get; }
        private async void ResendExecute()
        {
            var response = await ProtoService.SendAsync(new ResendRecoveryEmailAddressCode());
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var code = _code;
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            var response = await ProtoService.SendAsync(new CheckRecoveryEmailAddressCode(code));
            if (response is PasswordState passwordState)
            {

            }
            else if (response is Error error)
            {

            }
        }
    }
}
