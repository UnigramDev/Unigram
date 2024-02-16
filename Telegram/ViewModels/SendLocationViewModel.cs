//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels
{
    public class SendLocationViewModel : ViewModelBase
    {
        private readonly ILocationService _locationService;

        public SendLocationViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService foursquareService)
            : base(clientService, settingsService, aggregator)
        {
            _locationService = foursquareService;

            Items = new SearchCollection<Venue, VenueCollection>(CreateSearch, new VenueDiffHandler());
        }

        public SearchCollection<Venue, VenueCollection> Items { get; private set; }

        private VenueCollection CreateSearch(object sender, string query)
        {
            if (_location != null)
            {
                return new VenueCollection(_locationService, _location.Latitude, _location.Longitude, query);
            }

            return null;
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
                    Items.UpdateQuery(Items.Query);
                }
            }
        }
    }

    public class VenueCollection : ObservableCollection<Venue>, ISupportIncrementalLoading
    {
        private readonly ILocationService _locationService;
        private readonly double _latitude;
        private readonly double _longitude;
        private readonly string _query;

        private string _nextOffset;
        private bool _hasMoreItems = true;

        public VenueCollection(ILocationService locationService, double latitude, double longitude, string query)
        {
            _locationService = locationService;
            _latitude = latitude;
            _longitude = longitude;
            _query = query;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                var response = await _locationService.GetVenuesAsync(0, _latitude, _longitude, _query, _nextOffset);
                var count = 0u;

                foreach (var item in response.Venues)
                {
                    Add(item);
                    count++;
                }

                _hasMoreItems = !string.IsNullOrEmpty(response.NextOffset);
                _nextOffset = response.NextOffset;

                return new LoadMoreItemsResult { Count = count };
            });
        }

        public bool HasMoreItems => _hasMoreItems;
    }

    public class VenueDiffHandler : IDiffHandler<Venue>
    {
        public bool CompareItems(Venue oldItem, Venue newItem)
        {
            return oldItem.Id == newItem.Id;
        }

        public void UpdateItem(Venue oldItem, Venue newItem)
        {

        }
    }
}
