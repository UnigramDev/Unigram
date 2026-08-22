//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;
using Windows.System;
using Windows.UI.Xaml.Controls;

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
            AppSettings.Appearance.UpdateNightMode();

            try
            {
                var result = await MapLocationFinder.FindLocationsAtAsync(geopoint, MapLocationDesiredAccuracy.Low);
                if (result.Status == MapLocationFinderStatus.Success)
                {
                    Town = result.Locations[0].Address.Town;
                }
            }
            catch
            {
                //
            }
        }

        public NightMode Mode
        {
            get => AppSettings.Appearance.NightMode;
            set
            {
                if (AppSettings.Appearance.NightMode != value)
                {
                    if (value == NightMode.Disabled)
                    {
                        IsLocationBased = false;
                        From = new TimeSpan(22, 0, 0);
                        To = new TimeSpan(9, 0, 0);
                        Location = new Location();
                        Town = null;
                    }
                    else if (AppSettings.Appearance.ForceNightMode)
                    {
                        AppSettings.Appearance.ForceNightMode = false;
                        AppSettings.Appearance.RequestedTheme = TelegramTheme.Light;
                    }

                    AppSettings.Appearance.NightMode = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsLocationBased
        {
            get => AppSettings.Appearance.IsLocationBased;
            set
            {
                if (AppSettings.Appearance.IsLocationBased != value)
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

                    AppSettings.Appearance.IsLocationBased = value;
                    RaisePropertyChanged();
                }
            }
        }

        public TimeSpan From
        {
            get => AppSettings.Appearance.From;
            set
            {
                if (AppSettings.Appearance.From != value)
                {
                    AppSettings.Appearance.From = value;
                    RaisePropertyChanged();
                }
            }
        }

        public TimeSpan To
        {
            get => AppSettings.Appearance.To;
            set
            {
                if (AppSettings.Appearance.To != value)
                {
                    AppSettings.Appearance.To = value;
                    RaisePropertyChanged();
                }
            }
        }

        public Location Location
        {
            get => AppSettings.Appearance.Location;
            set
            {
                if (AppSettings.Appearance.Location.Latitude != value.Latitude || AppSettings.Appearance.Location.Longitude != value.Longitude)
                {
                    AppSettings.Appearance.Location = value;
                    RaisePropertyChanged();
                }
            }
        }

        public string Town
        {
            get => AppSettings.Appearance.Town;
            set
            {
                if (AppSettings.Appearance.Town != value)
                {
                    AppSettings.Appearance.Town = value;
                    RaisePropertyChanged();
                }
            }
        }
    }
}
