using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Template10.Utils;
using Unigram.Core.Common;
using Unigram.Core.Models;
using Unigram.Core.Services;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Navigation;
using Unigram.Services;

namespace Unigram.ViewModels.Dialogs
{
    public class DialogShareLocationViewModel : UnigramViewModelBase
    {
        private readonly ILocationService _locationService;

        public DialogShareLocationViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, ILocationService foursquareService)
            : base(protoService, cacheService, aggregator)
        {
            _locationService = foursquareService;

            Items = new MvxObservableCollection<TdWindows.Venue>();
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

            var venues = await _locationService.GetVenuesAsync(location.Point.Position.Latitude, location.Point.Position.Longitude);
            Items.ReplaceWith(venues);
        }

        public MvxObservableCollection<TdWindows.Venue> Items { get; private set; }

        private Geocoordinate _location;
        public Geocoordinate Location
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
    }
}
