//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.Services;

namespace Unigram.ViewModels.Settings.Privacy
{
    public class SettingsPrivacyPhoneViewModel : TLMultipleViewModelBase
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

            SendCommand = new RelayCommand(SendExecute);
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

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
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
