using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Core.Models;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Devices.Geolocation;

namespace Unigram.Core.Services
{
    public interface ILocationService
    {
        Task<Geolocator> StartTrackingAsync();
        void StopTracking();

        Task<Geocoordinate> GetPositionAsync();

        Task<List<TLMessageMediaVenue>> GetVenuesAsync(double latitude, double longitute, string query = null);
    }

    public class LocationService : ILocationService
    {
        private Geolocator _locator;
        private ExtendedExecutionSession _session;

        public async Task<Geolocator> StartTrackingAsync()
        {
            if (_session != null)
            {
                return _locator;
            }

            if (_locator == null)
            {
                try
                {
                    var accessStatus = await Geolocator.RequestAccessAsync();
                    if (accessStatus == GeolocationAccessStatus.Allowed)
                    {
                        _locator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default, ReportInterval = uint.MaxValue, MovementThreshold = 20 };
                    }
                }
                catch { }
            }

            _session = new ExtendedExecutionSession();
            _session.Description = "Live Location";
            _session.Reason = ExtendedExecutionReason.LocationTracking;
            _session.Revoked += ExtendedExecutionSession_Revoked;

            var result = await _session.RequestExtensionAsync();
            if (result == ExtendedExecutionResult.Denied)
            {
                //TODO: handle denied
            }

            return _locator;
        }

        public void StopTracking()
        {
            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }
        }

        private void ExtendedExecutionSession_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            StopTracking();
        }

        public async Task<Geocoordinate> GetPositionAsync()
        {
            try
            {
                var accessStatus = await Geolocator.RequestAccessAsync();
                if (accessStatus == GeolocationAccessStatus.Allowed)
                {
                    var geolocator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
                    var location = await geolocator.GetGeopositionAsync();

                    return location.Coordinate;
                }
            } catch { }

            return null;
        }

        public async Task<List<TLMessageMediaVenue>> GetVenuesAsync(double latitude, double longitute, string query = null)
        {
            var builder = new StringBuilder("https://api.foursquare.com/v2/venues/search/?");
            if (string.IsNullOrEmpty(query) == false)
            {
                builder.Append(string.Format("{0}={1}&", "query", WebUtility.UrlEncode(query)));
            }
            builder.Append(string.Format("{0}={1}&", "v", "20150326"));
            builder.Append(string.Format("{0}={1}&", "locale", "en"));
            builder.Append(string.Format("{0}={1}&", "limit", "25"));
            builder.Append(string.Format("{0}={1}&", "client_id", "BN3GWQF1OLMLKKQTFL0OADWD1X1WCDNISPPOT1EMMUYZTQV1"));
            builder.Append(string.Format("{0}={1}&", "client_secret", "WEEZHCKI040UVW2KWW5ZXFAZ0FMMHKQ4HQBWXVSX4WXWBWYN"));
            builder.Append(string.Format("{0}={1},{2}&", "ll", latitude.ToString(new CultureInfo("en-US")), longitute.ToString(new CultureInfo("en-US"))));

            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, builder.ToString());

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = await Task.Run(() => JsonConvert.DeserializeObject<FoursquareRootObject>(content));

                if (json?.response?.venues != null)
                {
                    var result = new List<TLMessageMediaVenue>();
                    foreach (var item in json.response.venues)
                    {
                        var venue = new TLMessageMediaVenue();
                        venue.VenueId = item.id;
                        venue.Title = item.name;
                        venue.Address = item.location.address ?? item.location.city ?? item.location.country;
                        venue.Provider = "foursquare";
                        venue.Geo = new TLGeoPoint { Lat = item.location.lat, Long = item.location.lng };

                        if (item.categories != null && item.categories.Count > 0)
                        {
                            var icon = item.categories[0].icon;
                            if (icon != null)
                            {
                                venue.VenueType = icon.prefix.Replace("https://ss3.4sqi.net/img/categories_v2/", string.Empty).TrimEnd('_');
                                //location.Icon = string.Format("https://ss3.4sqi.net/img/categories_v2/{0}_88.png");
                            }
                        }

                        result.Add(venue);
                    }

                    return result;
                }
            }

            return new List<TLMessageMediaVenue>();
        }
    }
}
