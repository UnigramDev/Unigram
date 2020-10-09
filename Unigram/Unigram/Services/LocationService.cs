using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Devices.Enumeration;
using Windows.Devices.Geolocation;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace Unigram.Services
{
    public interface ILocationService
    {
        Task<Geolocator> StartTrackingAsync();
        void StopTracking();

        Task<Location> GetPositionAsync();

        Task<List<Venue>> GetVenuesAsync(long chatId, double latitude, double longitude, string query = null);
    }

    public class LocationService : ILocationService
    {
        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;

        public LocationService(IProtoService protoService, ICacheService cacheService)
        {
            _protoService = protoService;
            _cacheService = cacheService;
        }

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

        public async Task<Location> GetPositionAsync()
        {
            try
            {
                var accessStatus = await CheckDeviceAccessAsync();
                if (accessStatus)
                {
                    var geolocator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
                    var location = await geolocator.GetGeopositionAsync();

                    return new Location(location.Coordinate.Point.Position.Latitude, location.Coordinate.Point.Position.Longitude);
                }
            }
            catch { }

            return null;
        }

        public async Task<bool> CheckDeviceAccessAsync()
        {
            var access = DeviceAccessInformation.CreateFromDeviceClass(DeviceClass.Location);
            if (access.CurrentStatus == DeviceAccessStatus.Unspecified)
            {
                var accessStatus = await Geolocator.RequestAccessAsync();
                if (accessStatus == GeolocationAccessStatus.Allowed)
                {
                    return true;
                }

                return false;
            }
            else if (access.CurrentStatus != DeviceAccessStatus.Allowed)
            {
                var message = Strings.Resources.PermissionNoLocationPosition;

                var confirm = await MessagePopup.ShowAsync(message, Strings.Resources.AppName, Strings.Resources.PermissionOpenSettings, Strings.Resources.OK);
                if (confirm == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                }

                return false;
            }

            return true;
        }

        public async Task<List<Telegram.Td.Api.Venue>> GetVenuesAsync(long chatId, double latitude, double longitude, string query = null)
        {
#if USE_FOURSQUARE

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

            try
            {
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, builder.ToString());

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = await Task.Run(() => JsonConvert.DeserializeObject<FoursquareRootObject>(content));

                    if (json?.response?.venues != null)
                    {
                        var result = new List<Telegram.Td.Api.Venue>();
                        foreach (var item in json.response.venues)
                        {
                            var venue = new Telegram.Td.Api.Venue();
                            venue.Id = item.id;
                            venue.Title = item.name;
                            venue.Address = item.location.address ?? item.location.city ?? item.location.country;
                            venue.Provider = "foursquare";
                            venue.Location = new Telegram.Td.Api.Location(item.location.lat, item.location.lng);

                            //if (item.categories != null && item.categories.Count > 0)
                            //{
                            //    var icon = item.categories[0].icon;
                            //    if (icon != null)
                            //    {
                            //        venue.VenueType = icon.prefix.Replace("https://ss3.4sqi.net/img/categories_v2/", string.Empty).TrimEnd('_');
                            //        //location.Icon = string.Format("https://ss3.4sqi.net/img/categories_v2/{0}_88.png");
                            //    }
                            //}

                            result.Add(venue);
                        }

                        return result;
                    }
                }
            }
            catch { }

            return new List<Telegram.Td.Api.Venue>();

#else

            var results = new List<Venue>();

            var option = _cacheService.Options.VenueSearchBotUsername;
            if (string.IsNullOrEmpty(option))
            {
                // TODO: use hardcoded bot?
                return results;
            }

            var chat = await _protoService.SendAsync(new SearchPublicChat(option)) as Chat;
            if (chat == null)
            {
                return results;
            }


            var user = _cacheService.GetUser(chat);
            if (user == null)
            {
                return results;
            }

            var response = await _protoService.SendAsync(new GetInlineQueryResults(user.Id, chatId, new Location(latitude, longitude), query ?? string.Empty, string.Empty));
            if (response is InlineQueryResults inlineResults)
            {
                foreach (var item in inlineResults.Results)
                {
                    if (item is InlineQueryResultVenue venue)
                    {
                        results.Add(venue.Venue);
                    }
                }
            }

            return results;
#endif
        }
    }
}
