using System.Collections.Generic;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Settings.Password;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings.Password
{
    public class SettingsPasswordHintViewModel : TLViewModelBase
    {
        public SettingsPasswordHintViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
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

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (state.TryGet("password", out string password))
            {
                _password = password;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var password = _password;
            var hint = _hint ?? string.Empty;

            if (string.Equals(password, hint))
            {
                await MessagePopup.ShowAsync(Strings.Resources.PasswordAsHintError, Strings.Resources.AppName, Strings.Resources.OK);
                return;
            }

            var state = new Dictionary<string, object>
            {
                { "password", password },
                { "hint", hint }
            };

            NavigationService.Navigate(typeof(SettingsPasswordEmailPage), state: state);
        }
    }
}
