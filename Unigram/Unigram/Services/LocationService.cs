//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
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

        Task<Location> GetPositionAsync(XamlRoot xamlRoot);

        Task<GetVenuesResult> GetVenuesAsync(long chatId, double latitude, double longitude, string query = null, string offset = null);
    }

    public class LocationService : ILocationService
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

        public async Task<Location> GetPositionAsync(XamlRoot xamlRoot)
        {
            try
            {
                var accessStatus = await CheckDeviceAccessAsync(xamlRoot);
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

        public async Task<bool> CheckDeviceAccessAsync(XamlRoot xamlRoot)
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

                var confirm = await MessagePopup.ShowAsync(xamlRoot, message, Strings.Resources.AppName, Strings.Resources.PermissionOpenSettings, Strings.Resources.OK);
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
