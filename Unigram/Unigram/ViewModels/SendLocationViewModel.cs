using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class SendLocationViewModel : TLViewModelBase
    {
        private readonly ILocationService _locationService;

        public SendLocationViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService foursquareService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _locationService = foursquareService;

            Items = new MvxObservableCollection<Venue>();
            OnNavigatedToAsync(null, NavigationMode.New, null);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var location = await _locationService.GetPositionAsync();
            if (location == null)
            {
                Location = null;
                return;
            }

            Location = location;

            var venues = await _locationService.GetVenuesAsync(0, location.Latitude, location.Longitude);
            Items.ReplaceWith(venues);
        }

        public MvxObservableCollection<Venue> Items { get; private set; }

        private Location _location;
        public Location Location
        {
            get
            {
                return _location;
            }
            set
            {
                Set(ref _location, value);
            }
        }

        #region Search

        public async void Find(string query)
        {
            var location = _location;
            if (location == null)
            {
                return;
            }

            var venues = await _locationService.GetVenuesAsync(0, location.Latitude, location.Longitude, query);
            Search = new MvxObservableCollection<Venue>(venues);
        }

        private MvxObservableCollection<Venue> _search;
        public MvxObservableCollection<Venue> Search
        {
            get
            {
                return _search;
            }
            set
            {
                Set(ref _search, value);
            }
        }

        #endregion
    }
}
