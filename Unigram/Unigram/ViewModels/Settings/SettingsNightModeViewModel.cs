using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.Services.Settings;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNightModeViewModel : TLViewModelBase
    {
        private readonly ILocationService _locationService;

        public SettingsNightModeViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _locationService = locationService;

            UpdateLocationCommand = new RelayCommand(UpdateLocationExecute);
        }

        public RelayCommand UpdateLocationCommand { get; }
        private async void UpdateLocationExecute()
        {
            var location = await _locationService.GetPositionAsync();
            if (location == null)
            {
                var confirm = await MessagePopup.ShowAsync(Strings.Resources.GpsDisabledAlert, Strings.Resources.AppName, Strings.Resources.ConnectingToProxyEnable, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location"));
                }

                return;
            }

            var geopoint = new Geopoint(new BasicGeoposition { Latitude = location.Latitude, Longitude = location.Longitude });

            Location = location;
            UpdateTheme();

            var result = await MapLocationFinder.FindLocationsAtAsync(geopoint, MapLocationDesiredAccuracy.Low);
            if (result.Status == MapLocationFinderStatus.Success)
            {
                Town = result.Locations[0].Address.Town;
            }
        }

        public async void UpdateTheme()
        {
            Settings.Appearance.UpdateTimer();

            var conditions = Settings.Appearance.CheckNightModeConditions();
            var theme = conditions == null
                ? Settings.Appearance.GetActualTheme()
                : conditions == true
                ? ElementTheme.Dark
                : ElementTheme.Light;

            foreach (TLWindowContext window in WindowContext.ActiveWrappers)
            {
                await window.Dispatcher.DispatchAsync(() =>
                {
                    window.UpdateTitleBar();

                    if (window.Content is FrameworkElement element)
                    {
                        element.RequestedTheme = theme;
                    }
                });
            }

            Aggregator.Publish(new UpdateSelectedBackground(true, ProtoService.GetSelectedBackground(true)));
            Aggregator.Publish(new UpdateSelectedBackground(false, ProtoService.GetSelectedBackground(false)));
        }

        public NightMode Mode
        {
            get
            {
                return Settings.Appearance.NightMode;
            }
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

                    Settings.Appearance.NightMode = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool IsLocationBased
        {
            get
            {
                return Settings.Appearance.IsLocationBased;
            }
            set
            {
                if (Settings.Appearance.IsLocationBased != value)
                {
                    if (value && Location.Latitude == 0 && Location.Longitude == 0)
                    {
                        UpdateLocationExecute();
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
            get
            {
                return Settings.Appearance.From;
            }
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
            get
            {
                return Settings.Appearance.To;
            }
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
            get
            {
                return Settings.Appearance.Location;
            }
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
            get
            {
                return Settings.Appearance.Town;
            }
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
