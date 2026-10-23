//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
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

            Items = new SearchCollection<Venue, VenueCollection>(CreateSearch, new VenueDiffHandler());
        }

        public SearchCollection<Venue, VenueCollection> Items { get; private set; }

        private VenueCollection CreateSearch(object sender, string query)
        {
            if (_location != null)
            {
                return new VenueCollection(_locationService, _location.Latitude, _location.Longitude, query, _results);
            }

            return null;
        }

        // Each venue's inline query and result in it, which a story area names a found venue by.
        private readonly Dictionary<Venue, VenueResult> _results = new();

        public bool TryGetResult(Venue venue, out long queryId, out string resultId)
        {
            if (_results.TryGetValue(venue, out VenueResult result))
            {
                queryId = result.QueryId;
                resultId = result.ResultId;
                return true;
            }

            queryId = 0;
            resultId = null;
            return false;
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

    public sealed partial class VenueResult
    {
        public VenueResult(long queryId, string resultId)
        {
            QueryId = queryId;
            ResultId = resultId;
        }

        public long QueryId { get; }

        public string ResultId { get; }
    }

    public partial class VenueCollection : IncrementalCollection<Venue>
    {
        private readonly ILocationService _locationService;
        private readonly double _latitude;
        private readonly double _longitude;
        private readonly string _query;
        private readonly Dictionary<Venue, VenueResult> _results;

        private string _nextOffset;
        public VenueCollection(ILocationService locationService, double latitude, double longitude, string query, Dictionary<Venue, VenueResult> results)
        {
            _locationService = locationService;
            _latitude = latitude;
            _longitude = longitude;
            _query = query;
            _results = results;
        }

        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await _locationService.GetVenuesAsync(0, _latitude, _longitude, _query, _nextOffset);

            for (int i = 0; i < response.Venues.Count; i++)
            {
                var item = response.Venues[i];

                if (response.ResultIds != null && i < response.ResultIds.Count)
                {
                    _results[item] = new VenueResult(response.QueryId, response.ResultIds[i]);
                }

                Add(item);
                totalCount++;
            }

            _nextOffset = response.NextOffset;

            return new IncrementalLoadResult(totalCount, !string.IsNullOrEmpty(response.NextOffset));
        }
    }

    public partial class VenueDiffHandler : IDiffHandler<Venue>
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
