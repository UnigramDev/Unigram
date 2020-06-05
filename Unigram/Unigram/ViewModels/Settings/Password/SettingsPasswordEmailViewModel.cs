using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Settings.Password;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings.Password
{
    public class SettingsPasswordEmailViewModel : TLViewModelBase
    {
        public SettingsPasswordEmailViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
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

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (state.TryGet("password", out string password))
            {
                _password = password;
            }

            if (state.TryGet("hint", out string hint))
            {
                _hint = hint;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
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

                var confirm = await MessagePopup.ShowAsync(Strings.Resources.YourEmailSkipWarningText, Strings.Resources.YourEmailSkipWarning, Strings.Resources.YourEmailSkip, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
            }

            var response = await ProtoService.SendAsync(new SetPassword(string.Empty, password, hint, addressValid, address));
            if (response is PasswordState passwordState)
            {
                if (passwordState.RecoveryEmailAddressCodeInfo != null)
                {
                    var state = new Dictionary<string, object>
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
                    await MessagePopup.ShowAsync(Strings.Resources.PasswordEmailInvalid, Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                }
            }
        }
    }
}