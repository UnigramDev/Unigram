using Rg.DiffUtils;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.ViewModels
{
    public class SendLocationViewModel : TLViewModelBase
    {
        private readonly ILocationService _locationService;

        public SendLocationViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService foursquareService)
            : base(protoService, cacheService, settingsService, aggregator)
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
