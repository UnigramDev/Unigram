using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls;
using Windows.ApplicationModel.ExtendedExecution;
using Windows.Devices.Enumeration;
using Windows.Devices.Geolocation;
using Windows.System;

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

                    return new Location(
                        location.Coordinate.Point.Position.Latitude,
                        location.Coordinate.Point.Position.Longitude,
                        location.Coordinate.Accuracy);
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

        public async Task<List<Venue>> GetVenuesAsync(long chatId, double latitude, double longitude, string query = null)
        {
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

            var response = await _protoService.SendAsync(new GetInlineQueryResults(user.Id, chatId, new Location(latitude, longitude, 0), query ?? string.Empty, string.Empty));
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
        }
    }
}
