//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Business
{
    public partial class BusinessLocationViewModel : BusinessFeatureViewModelBase
    {
        public BusinessLocationViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override Task OnNavigatedToAsync(UserFullInfo cached, NavigationMode mode, NavigationState state)
        {
            _cached = cached?.BusinessInfo?.Location;
            Address = cached?.BusinessInfo?.Location?.Address;
            Location = cached?.BusinessInfo?.Location?.Location;

            return Task.CompletedTask;
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Invalidate(ref _address, value);
        }

        private Location _location;
        public Location Location
        {
            get => _location;
            set
            {
                if (Invalidate(ref _location, value))
                {
                    RaisePropertyChanged(nameof(IsLocationValid));
                }
            }
        }

        public bool IsLocationValid => Location != null && Location.Latitude != 0 && Location.Longitude != 0;

        public void ToggleMap()
        {
            if (IsLocationValid)
            {
                Location = new Location();
            }
            else
            {
                ChangeMap();
            }
        }

        public async void ChangeMap()
        {
            var popup = new SendLocationPopup(Session);

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                if (popup.Media is InputMessageLocation location)
                {
                    Location = location.Location;
                }
                else if (popup.Media is InputMessageVenue venue)
                {
                    Location = venue.Venue.Location;
                }
            }
        }

        public async void Clear()
        {
            var confirm = await ShowPopupAsync(Strings.BusinessLocationClearMessage, Strings.BusinessLocationClearTitle, Strings.Remove, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                Address = string.Empty;
                Location = new Location();
            }
        }

        public override bool HasChanged => !_cached.AreTheSame(GetSettings());

        protected override async void ContinueImpl(NavigatingEventArgs args)
        {
            var settings = GetSettings();
            if (settings != null)
            {
                if (string.IsNullOrEmpty(Address))
                {
                    RaisePropertyChanged("ADDRESS_INVALID");
                    return;
                }
            }

            if (settings.AreTheSame(_cached))
            {
                _completed = true;
                NavigationService.GoBack(args);
                return;
            }

            var response = await ClientService.SendAsync(new SetBusinessLocation(settings));
            if (response is Ok)
            {
                _completed = true;
                NavigationService.GoBack(args);
            }
            else if (response is Error error)
            {
                ShowToast(error);
            }
        }

        private BusinessLocation _cached;
        private BusinessLocation GetSettings()
        {
            if (string.IsNullOrEmpty(Address))
            {
                return null;
            }

            return new BusinessLocation
            {
                Location = Location,
                Address = Address
            };
        }
    }
}
