//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Create
{
    public partial class NewContactViewModel : ViewModelBase
    {
        public NewContactViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            ClientService.Send(new GetCountryCode(), result =>
            {
                if (result is Text text)
                {
                    GotUserCountry(text.TextValue);
                }
            });

            return Task.CompletedTask;
        }

        private void GotUserCountry(string code)
        {
            Country country = null;
            foreach (var local in Country.All)
            {
                if (string.Equals(local.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    country = local;
                    break;
                }
            }

            BeginOnUIThread(() =>
            {
                if (country != null && SelectedCountry == null && string.IsNullOrEmpty(PhoneNumber))
                {
                    _phoneNumber = $"+{country.PhoneCode}";
                    SelectedCountry = country;
                }
            });
        }

        private Country _selectedCountry;
        public Country SelectedCountry
        {
            get => _selectedCountry;
            set => Set(ref _selectedCountry, value);
        }

        private string _firstName = string.Empty;
        public string FirstName
        {
            get => _firstName;
            set
            {
                if (Set(ref _firstName, value))
                {
                    RaisePropertyChanged(nameof(CanCreate));
                }
            }
        }

        private string _lastName = string.Empty;
        public string LastName
        {
            get => _lastName;
            set => Set(ref _lastName, value);
        }

        private string _phoneNumber = string.Empty;
        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                if (Set(ref _firstName, value))
                {
                    RaisePropertyChanged(nameof(CanCreate));
                }
            }
        }

        public bool CanCreate => !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(PhoneNumber);

        public async void Create()
        {
            var phoneNumber = _phoneNumber?.Trim('+').Replace(" ", string.Empty);

            var response = await ClientService.SendAsync(new ImportContacts(new[] { new Contact(phoneNumber, _firstName, _lastName, string.Empty, 0) }));
            if (response is ImportedContacts imported)
            {
                if (imported.UserIds.Count > 0)
                {
                    NavigationService.NavigateToUser(imported.UserIds[0]);
                }
                else
                {
                    await ShowPopupAsync(string.Format(Strings.ContactNotRegistered, _firstName), Strings.AppName, Strings.OK);
                }
            }
            else
            {
                await ShowPopupAsync(string.Format(Strings.ContactNotRegistered, _firstName), Strings.AppName, Strings.OK);
            }
        }
    }
}
