//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public partial class SendLocationViewModel : ViewModelBase
    {
        private readonly ILocationService _locationService;

        public SendLocationViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService foursquareService)
            : base(clientService, settingsService, aggregator)
        {
            _locationService = foursquareService;
        }

        private Location _location;
        public Location Location
        {
            get => _location;
            set
            {
                if (value?.Latitude != _location?.Latitude || value?.Longitude != _location?.Longitude)
                {
                    Set(ref _location, value);
                }
            }
        }
    }
}
