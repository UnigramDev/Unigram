using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
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
                await MessagePopup.ShowAsync(Strings.Resources.PasswordAsHintError, Strings.Resources.AppName, Strings.Resources.OK);
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
