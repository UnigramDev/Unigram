//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;
using Windows.System;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsNightModeViewModel : SettingsThemesViewModel
    {
        private readonly ILocationService _locationService;

        public SettingsNightModeViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IThemeService themeService, ILocationService locationService)
            : base(clientService, settingsService, aggregator, themeService, true)
        {
            _locationService = locationService;
        }

        public async void UpdateLocation()
        {
            var location = await _locationService.GetPositionAsync(NavigationService);
            if (location == null)
            {
                var confirm = await ShowPopupAsync(Strings.GpsDisabledAlert, Strings.AppName, Strings.ConnectingToProxyEnable, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location"));
                }

                return;
            }

            var geopoint = new Geopoint(new BasicGeoposition { Latitude = location.Latitude, Longitude = location.Longitude });

            Location = location;
            Settings.Appearance.UpdateNightMode();

            var result = await MapLocationFinder.FindLocationsAtAsync(geopoint, MapLocationDesiredAccuracy.Low);
            if (result.Status == MapLocationFinderStatus.Success)
            {
                Town = result.Locations[0].Address.Town;
            }
        }

        public NightMode Mode
        {
            get => Settings.Appearance.NightMode;
            set
            {
                if (Settings.Appearance.NightMode != value)
                {
                    if (value == NightMode.Disabled)
                    {
                        IsLocationBased = false;
                        From = new TimeSpan(22, 0, 0);
                        To = new TimeSpan(9, 0, 0);
                        Location = new Location();
                        Town = null;
                    }
                    else if (Settings.Appearance.ForceNightMode)
                    {
                        Settings.Appearance.ForceNightMode = false;
                        Settings.Appearance.RequestedTheme = TelegramTheme.Light;
                    }

                    Settings.Appearance.NightMode = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsLocationBased
        {
            get => Settings.Appearance.IsLocationBased;
            set
            {
                if (Settings.Appearance.IsLocationBased != value)
                {
                    if (value && Location.Latitude == 0 && Location.Longitude == 0)
                    {
                        UpdateLocation();
                    }
                    else
                    {
                        From = new TimeSpan(22, 0, 0);
                        To = new TimeSpan(9, 0, 0);
                        Location = new Location();
                        Town = null;
                    }

                    Settings.Appearance.IsLocationBased = value;
                    RaisePropertyChanged();
                }
            }
        }

        public TimeSpan From
        {
            get => Settings.Appearance.From;
            set
            {
                if (Settings.Appearance.From != value)
                {
                    Settings.Appearance.From = value;
                    RaisePropertyChanged();
                }
            }
        }

        public TimeSpan To
        {
            get => Settings.Appearance.To;
            set
            {
                if (Settings.Appearance.To != value)
                {
                    Settings.Appearance.To = value;
                    RaisePropertyChanged();
                }
            }
        }

        public Location Location
        {
            get => Settings.Appearance.Location;
            set
            {
                if (Settings.Appearance.Location.Latitude != value.Latitude || Settings.Appearance.Location.Longitude != value.Longitude)
                {
                    Settings.Appearance.Location = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string Town
        {
            get => Settings.Appearance.Town;
            set
            {
                if (Settings.Appearance.Town != value)
                {
                    Settings.Appearance.Town = value;
                    RaisePropertyChanged();
                }
            }
        }
    }
}
