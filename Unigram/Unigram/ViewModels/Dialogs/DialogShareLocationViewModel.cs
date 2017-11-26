using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Core.Common;
using Unigram.Core.Models;
using Unigram.Core.Services;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Dialogs
{
    public class DialogShareLocationViewModel : UnigramViewModelBase
    {
        private readonly ILocationService _locationService;

        public DialogShareLocationViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, ILocationService foursquareService)
            : base(protoService, cacheService, aggregator)
        {
            _locationService = foursquareService;

            Items = new MvxObservableCollection<TLMessageMediaVenue>();
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

        public MvxObservableCollection<TLMessageMediaVenue> Items { get; private set; }

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
