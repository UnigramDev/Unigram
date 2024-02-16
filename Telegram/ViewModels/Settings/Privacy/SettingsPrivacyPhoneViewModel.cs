//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyPhoneViewModel : MultiViewModelBase
    {
        private readonly SettingsPrivacyShowPhoneViewModel _showPhone;
        private readonly SettingsPrivacyAllowFindingByPhoneNumberViewModel _allowFindingByPhoneNumber;

        public SettingsPrivacyPhoneViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, SettingsPrivacyShowPhoneViewModel showPhone, SettingsPrivacyAllowFindingByPhoneNumberViewModel allowFindingByPhoneNumber)
            : base(clientService, settingsService, aggregator)
        {
            _showPhone = showPhone;
            _allowFindingByPhoneNumber = allowFindingByPhoneNumber;

            Children.Add(showPhone);
            Children.Add(allowFindingByPhoneNumber);
        }

        public SettingsPrivacyShowPhoneViewModel ShowPhone => _showPhone;
        public SettingsPrivacyAllowFindingByPhoneNumberViewModel AllowFindingByPhoneNumber => _allowFindingByPhoneNumber;

        private string _phoneNumber;
        public string PhoneNumber
        {
            get => _phoneNumber;
            set => Set(ref _phoneNumber, value);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (ClientService.TryGetUser(ClientService.Options.MyId, out User user))
            {
                PhoneNumber = MeUrlPrefixConverter.Convert(ClientService, $"+{user.PhoneNumber}");
            }

            return Task.CompletedTask;
        }

        public async void Save()
        {
            var response1 = await ShowPhone.SendAsync();
            var response2 = await AllowFindingByPhoneNumber.SendAsync();
            if (response1 is Ok && response2 is Ok)
            {
                NavigationService.GoBack();
            }
        }
    }
}
