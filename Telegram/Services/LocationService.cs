//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Td.Api;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Devices.Enumeration;
using Windows.Devices.Geolocation;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services
{
    public readonly struct GetVenuesResult
    {
        public string NextOffset { get; }

        public List<Venue> Venues { get; }

        public GetVenuesResult(string offset, List<Venue> venues)
        {
            NextOffset = offset;
            Venues = venues;
        }
    }

    public interface ILocationService
    {
        Task<Geolocator> StartTrackingAsync();
        void StopTracking();

        Task<Location> GetPositionAsync(INavigationService navigation);

        /// <summary>
        /// Whether location access has been granted already, so the position can be had without
        /// asking.
        /// </summary>
        bool HasLocationAccess { get; }

        /// <summary>
        /// The weather where the device is, kept for the rest of the hour at about the same place.
        /// Without <paramref name="navigation"/> it asks for nothing, and needs location access to
        /// have been granted already.
        /// </summary>
        Task<CurrentWeather> GetCurrentWeatherAsync(INavigationService navigation);

        Task<GetVenuesResult> GetVenuesAsync(long chatId, double latitude, double longitude, string query = null, string offset = null);
    }

    public partial class LocationService : ILocationService
    {
        private readonly IClientService _clientService;

        public LocationService(IClientService clientService)
        {
            _clientService = clientService;
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
            _session?.Dispose();
            _session = null;
        }

        private void ExtendedExecutionSession_Revoked(object sender, ExtendedExecutionRevokedEventArgs args)
        {
            StopTracking();
        }

        public async Task<Location> GetPositionAsync(INavigationService navigation)
        {
            try
            {
                var accessStatus = await CheckDeviceAccessAsync(navigation);
                if (accessStatus)
                {
                    return await GetGeopositionAsync();
                }
            }
            catch { }

            return null;
        }

        private static async Task<Location> GetGeopositionAsync()
        {
            var geolocator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
            var location = await geolocator.GetGeopositionAsync();

            return new Location(
                location.Coordinate.Point.Position.Latitude,
                location.Coordinate.Point.Position.Longitude,
                location.Coordinate.Accuracy);
        }

        public bool HasLocationAccess => DeviceAccessInformation.CreateFromDeviceClass(DeviceClass.Location).CurrentStatus == DeviceAccessStatus.Allowed;

        private CurrentWeather _weather;
        private string _weatherKey;

        public async Task<CurrentWeather> GetCurrentWeatherAsync(INavigationService navigation)
        {
            Location location = null;

            if (navigation != null)
            {
                location = await GetPositionAsync(navigation);
            }
            else if (HasLocationAccess)
            {
                try
                {
                    location = await GetGeopositionAsync();
                }
                catch { }
            }

            if (location == null)
            {
                return null;
            }

            // About a hundred metres, within the same hour.
            var key = string.Format("{0}:{1}:{2}", Math.Round(location.Latitude * 1000), Math.Round(location.Longitude * 1000), DateTime.UtcNow.Ticks / TimeSpan.TicksPerHour);
            if (_weather != null && _weatherKey == key)
            {
                return _weather;
            }

            var response = await _clientService.SendAsync(new GetCurrentWeather(new Location(location.Latitude, location.Longitude, 0)));
            if (response is CurrentWeather weather)
            {
                _weather = weather;
                _weatherKey = key;

                return weather;
            }

            return null;
        }

        public async Task<bool> CheckDeviceAccessAsync(INavigationService navigation)
        {
            var access = DeviceAccessInformation.CreateFromDeviceClass(DeviceClass.Location);
            if (access.CurrentStatus == DeviceAccessStatus.Unspecified)
            {
                try
                {
                    // Throws ERROR_SERVICE_DISABLED when the system location service is disabled.
                    var accessStatus = await Geolocator.RequestAccessAsync();
                    if (accessStatus == GeolocationAccessStatus.Allowed)
                    {
                        return true;
                    }
                }
                catch { }

                return false;
            }
            else if (access.CurrentStatus != DeviceAccessStatus.Allowed)
            {
                var message = Strings.PermissionNoLocationPosition;

                var confirm = await navigation.ShowPopupAsync(message, Strings.AppName, Strings.PermissionOpenSettings, Strings.OK);
                if (confirm == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                }

                return false;
            }

            return true;
        }

        public async Task<GetVenuesResult> GetVenuesAsync(long chatId, double latitude, double longitude, string query = null, string offset = null)
        {
            var results = new List<Venue>();

            var option = _clientService.Options.VenueSearchBotUsername;
            if (string.IsNullOrEmpty(option))
            {
                // TODO: use hardcoded bot?
                return new GetVenuesResult(null, results);
            }

            var chat = await _clientService.SendAsync(new SearchPublicChat(option)) as Chat;
            if (chat == null)
            {
                return new GetVenuesResult(null, results);
            }


            var user = _clientService.GetUser(chat);
            if (user == null)
            {
                return new GetVenuesResult(null, results);
            }

            var response = await _clientService.SendAsync(new GetInlineQueryResults(user.Id, chatId, new Location(latitude, longitude, 0), query ?? string.Empty, offset ?? string.Empty));
            if (response is InlineQueryResults inlineResults)
            {
                foreach (var item in inlineResults.Results)
                {
                    if (item is InlineQueryResultVenue venue)
                    {
                        results.Add(venue.Venue);
                    }
                }

                new GetVenuesResult(inlineResults.NextOffset, results);
            }

            return new GetVenuesResult(null, results);
        }
    }
}
