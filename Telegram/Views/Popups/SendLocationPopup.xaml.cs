//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Data.Json;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Storage;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using JsonValue = Windows.Data.Json.JsonValue;

namespace Telegram.Views.Popups
{
    public sealed partial class SendLocationPopup : ModalPopup
    {
        // The app config object that carries the Mapbox tokens: "maps" for the map itself, "geo"
        // for the reverse geocoding that names the point under the pin, and "bmaps"/"bgeo" for
        // the same when picking a business's address.
        private const string MapsConfigKey = "tdesktop_config_map";

        // The tokens only work from pages on this origin: Mapbox checks the referrer against it.
        // Nothing resolves it, the page is served from memory by OnMapResourceRequested.
        private const string MapHost = "desktop-app-resource";

        public SendLocationViewModel ViewModel => DataContext as SendLocationViewModel;

        private ShapeVisual _previewShimmer;
        private Geolocator _geolocator;

        private readonly Visual _position;
        private readonly Visual _accuracy;
        private float _accuracyRadius;

        public InputMessageContent Media { get; private set; }

        // Picking a story's location, around where it starts.
        private readonly bool _story;
        private readonly Location _start;

        private PaintEntity _place;

        // The interactive map, created only when asked for: every load is billed.
        private WebView2 _mapView;
        private bool _mapInitialized;
        private string _mapToken;
        private string _geoToken;

        // The point under the pin, and its name once the geocoder has one.
        private Location _center;
        private string _centerAddress;
        private bool _centerSelected;

        // The map follows the device until it is moved by hand.
        private bool _centerMoved;

        /// <summary>
        /// Picks the place a story's location shows, starting at <paramref name="start"/>, or at
        /// the device when it is null. <see cref="ApplyTo"/> hands it over.
        /// </summary>
        public SendLocationPopup(ISession session, Location start)
            : this(session)
        {
            _story = true;
            _start = start;

            PrimaryButtonText = Strings.Done;
            IsPrimaryButtonEnabled = false;

            // A story names a place, which the device's own location does not have.
            CurrentLocationButton.Visibility = Visibility.Collapsed;
            SelectedLocationButton.Visibility = Visibility.Visible;
            SelectedLocation.UpdateState(true, false, true);
        }

        /// <param name="business">Picks a business's address, which has its own Mapbox tokens.</param>
        public SendLocationPopup(ISession session, bool business = false)
        {
            InitializeComponent();
            DataContext = session.Resolve<SendLocationViewModel>();

            Title = Strings.AttachLocation;

            PrimaryButtonText = Strings.Send;
            SecondaryButtonText = Strings.Cancel;

            MapPresenter.Constraint = new Size(16, 9);

            Accuracy.Width = 0;
            Accuracy.Height = 0;

            Loaded += OnLoaded;

            _accuracy = ElementComposition.GetElementVisual(Accuracy);
            _position = ElementComposition.GetElementVisual(Position);

            _position.CenterPoint = new Vector3(20, 48, 0);
            _position.Scale = Vector3.Zero;

            CompositionPathParser.CreateThumbnail(16, 9, 0, out ShapeVisual visual);
            ElementCompositionPreview.SetElementChildVisual(MapShimmer, visual);
            _previewShimmer = visual;

            if (TryGetMapTokens(session.ClientService, business, out _mapToken, out _geoToken) && ChromiumWebPresenter.IsSupported())
            {
                MapButton.Visibility = Visibility.Visible;
            }
        }

        private static bool TryGetMapTokens(IClientService clientService, bool business, out string map, out string geo)
        {
            var config = clientService.Config?.GetNamedObject(MapsConfigKey);
            if (config != null)
            {
                map = config.GetNamedString(business ? "bmaps" : "maps", null);
                geo = config.GetNamedString(business ? "bgeo" : "geo", null);
            }
            else
            {
                map = null;
                geo = null;
            }

            return !string.IsNullOrEmpty(map) && !string.IsNullOrEmpty(geo);
        }

        private void OnOpened(ModalPopup sender, ModalPopupOpenedEventArgs args)
        {

        }

        private void OnClosed(ModalPopup sender, ModalPopupClosedEventArgs args)
        {
            if (_geolocator != null)
            {
                _geolocator.PositionChanged -= OnPositionChanged;
                _geolocator = null;
            }

            CloseMap();

            CompositionPathParser.StopThumbnail(_previewShimmer);
            _previewShimmer = null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_story)
            {
                FindStoryLocation();
            }
            else
            {
                FindLocation();
            }
        }

        // Once, rather than following the device as a message's location does: the story's
        // place is wherever the pin is left, starting here.
        private async void FindStoryLocation()
        {
            var location = _start ?? await ViewModel.Session.Resolve<ILocationService>().GetPositionAsync(WindowContext.GetNavigationService(this));
            if (location == null)
            {
                return;
            }

            ViewModel.Location = location;

            if (_mapView == null)
            {
                Map.SetSource(ViewModel.ClientService, location, (int)MapPresenter.ActualWidth, (int)MapPresenter.ActualHeight, 0);
                ShowPosition();

                UpdateCenter(location, null);
            }
            else if (!_centerMoved)
            {
                FlyTo(location);
            }
        }

        private async void FindLocation()
        {
            GeolocationAccessStatus accessStatus;

            try
            {
                accessStatus = await Geolocator.RequestAccessAsync();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
                accessStatus = GeolocationAccessStatus.Denied;
            }

            if (accessStatus == GeolocationAccessStatus.Allowed)
            {
                if (_geolocator == null)
                {
                    _geolocator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
                    _geolocator.PositionChanged += OnPositionChanged;

                    // PositionChanged is not raised for a position that was already known.
                    try
                    {
                        var position = await _geolocator.GetGeopositionAsync();
                        if (_geolocator != null)
                        {
                            UpdateLocation(position);
                        }
                    }
                    catch
                    {
                        // All the remote procedure calls must be wrapped in a try-catch block
                    }
                }
                else
                {
                    if (_previewShimmer == null)
                    {
                        CompositionPathParser.CreateThumbnail(320, 200, 0, out ShapeVisual visual);
                        ElementCompositionPreview.SetElementChildVisual(MapShimmer, visual);
                        _previewShimmer = visual;
                    }

                    try
                    {
                        UpdateLocation(await _geolocator.GetGeopositionAsync());
                    }
                    catch
                    {
                        // All the remote procedure calls must be wrapped in a try-catch block
                    }
                }
            }
        }

        private void OnPositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateLocation(args.Position));
        }

        private void UpdateLocation(Geoposition point)
        {
            ViewModel.Location = new Location(
                point.Coordinate.Point.Position.Latitude,
                point.Coordinate.Point.Position.Longitude,
                point.Coordinate.Accuracy);

            CurrentLocation.Address = string.Format(Strings.AccurateTo,
                Locale.Declension(Strings.R.Meters, (int)point.Coordinate.Accuracy));

            if (!_centerSelected)
            {
                CurrentLocation.UpdateState(true, true, true);
                Media = new InputMessageLocation(ViewModel.Location);
            }

            // The static map stays under the interactive one, which only needs to be told once.
            if (_mapView != null)
            {
                if (!_centerMoved)
                {
                    FlyTo(ViewModel.Location);
                }

                return;
            }

            if (_previewShimmer == null && (ViewModel?.Location.Latitude != point.Coordinate.Point.Position.Latitude || ViewModel?.Location.Longitude != point.Coordinate.Point.Position.Longitude))
            {
                CompositionPathParser.CreateThumbnail(16, 9, 0, out ShapeVisual visual);
                ElementCompositionPreview.SetElementChildVisual(MapShimmer, visual);
                _previewShimmer = visual;
            }

            var latitude = point.Coordinate.Point.Position.Latitude;

            var rasterization = XamlRoot.RasterizationScale;

            var width = (int)(MapPresenter.ActualWidth);
            var height = (int)(MapPresenter.ActualHeight);

            var pixels = 96 * rasterization;
            var scale = pixels * 39.37 * 156543.04 * Math.Cos(latitude * Math.PI / 180) / Math.Pow(2, 15);
            var accuracy = point.Coordinate.Accuracy * 39.37;

            var radius = (float)(accuracy / scale * pixels);
            if (radius != _accuracyRadius)
            {
                ShowPosition();

                Accuracy.Width = radius * 2;
                Accuracy.Height = radius * 2;

                var prev = new Vector2(_accuracyRadius, _accuracyRadius);
                var next = new Vector2(radius, radius);

                var anim = _accuracy.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0, new Vector3(prev / next, 1));
                anim.InsertKeyFrame(1, Vector3.One);

                _accuracy.CenterPoint = new Vector3(next, 0);
                _accuracy.StartAnimation("Scale", anim);
                _accuracyRadius = radius;
            }

            Map.SetSource(ViewModel.ClientService, ViewModel.Location, width, height, 0);
        }

        private void ShowPosition()
        {
            if (_position.Scale == Vector3.Zero)
            {
                var spring = _position.Compositor.CreateSpringVector3Animation();
                spring.InitialValue = Vector3.Zero;
                spring.FinalValue = Vector3.One;
                spring.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                spring.DampingRatio = 0.6f;

                _position.StartAnimation("Scale", spring);
            }
        }

        private void CurrentLocation_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Location == null)
            {
                return;
            }

            _centerSelected = false;
            SelectedLocation.UpdateState(false, true, true);

            CurrentLocation.UpdateState(true, true, true);
            Media = new InputMessageLocation(ViewModel.Location);
        }

        private void SelectedLocation_Click(object sender, RoutedEventArgs e)
        {
            SelectCenter();
        }

        private void SelectCenter()
        {
            if (_center == null)
            {
                return;
            }

            _centerSelected = true;
            CurrentLocation.UpdateState(false, true, true);
            SelectedLocation.UpdateState(true, true, true);

            Media = new InputMessageLocation(_center);
        }

        private void Map_ImageOpened(object sender, RoutedEventArgs e)
        {
            CompositionPathParser.StopThumbnail(_previewShimmer);
            _previewShimmer = null;
            ElementCompositionPreview.SetElementChildVisual(MapShimmer, null);
        }

        #region Map

        private async void MapButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mapView != null)
            {
                return;
            }

            MapButton.Visibility = Visibility.Collapsed;
            Accuracy.Visibility = Visibility.Collapsed;

            _mapView = new WebView2();
            _mapView.WebMessageReceived += OnMapMessageReceived;

            // Over the static map, which shows through until the tiles arrive, and under the pin.
            MapPresenter.Children.Insert(1, _mapView);
            MapPresenter.Constraint = new Size(4, 3);

            ShowPosition();
            ElementCompositionPreview.SetIsTranslationEnabled(Position, true);

            var view = _mapView;

            try
            {
                await view.EnsureCoreWebView2Async();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            // Closed while it was being created.
            if (_mapView != view)
            {
                return;
            }

            var core = view.CoreWebView2;
            if (core == null)
            {
                CloseMap();
                return;
            }

            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = AppSettings.Diagnostics.EnableWebViewDevTools;
            core.Settings.AreDevToolsEnabled = AppSettings.Diagnostics.EnableWebViewDevTools;

            core.NavigationStarting += OnMapNavigationStarting;
            core.NewWindowRequested += OnMapNewWindowRequested;
            core.WebResourceRequested += OnMapResourceRequested;
            core.AddWebResourceRequestedFilter($"http://{MapHost}/*", CoreWebView2WebResourceContext.All);

            core.Navigate($"http://{MapHost}/picker.html");
        }

        private void CloseMap()
        {
            var view = _mapView;
            if (view == null)
            {
                return;
            }

            _mapView = null;
            _mapInitialized = false;

            view.WebMessageReceived -= OnMapMessageReceived;

            var core = view.CoreWebView2;
            if (core != null)
            {
                core.NavigationStarting -= OnMapNavigationStarting;
                core.NewWindowRequested -= OnMapNewWindowRequested;
                core.WebResourceRequested -= OnMapResourceRequested;
            }

            MapPresenter.Children.Remove(view);
            view.Close();
        }

        private async void OnMapResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            if (!Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out Uri uri) || uri.AbsolutePath != "/picker.html")
            {
                args.Response = sender.Environment.CreateWebResourceResponse(null, 404, "Not Found", string.Empty);
                return;
            }

            var deferral = args.GetDeferral();

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(Path.Combine(AppPaths.InstalledPath, "Assets", "Map", "picker.html"));
                var stream = await file.OpenReadAsync();

                args.Response = sender.Environment.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: text/html; charset=utf-8");
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                args.Response = sender.Environment.CreateWebResourceResponse(null, 500, "Internal Server Error", string.Empty);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void OnMapNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            args.Cancel = !Uri.TryCreate(args.Uri, UriKind.Absolute, out Uri uri) || uri.Host != MapHost;
        }

        // The attribution links, which the map's terms require to work.
        private async void OnMapNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            args.Handled = true;

            if (Uri.TryCreate(args.Uri, UriKind.Absolute, out Uri uri) && uri.Scheme == Uri.UriSchemeHttps)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private void OnMapMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            if (sender != _mapView || !Uri.TryCreate(args.Source, UriKind.Absolute, out Uri source) || source.Host != MapHost)
            {
                return;
            }

            string message;
            try
            {
                message = args.TryGetWebMessageAsString();
            }
            catch
            {
                return;
            }

            if (!JsonObject.TryParse(message, out JsonObject data))
            {
                return;
            }

            switch (data.GetNamedString("event", string.Empty))
            {
                case "ready":
                    InitializeMap();
                    break;
                case "error":
                    CloseMap();
                    break;
                case "escape":
                    Hide();
                    break;
                case "move_start":
                    if (data.GetNamedBoolean("user", false))
                    {
                        _centerMoved = true;
                    }

                    LiftPin(true);
                    _centerAddress = null;
                    SelectedLocation.Address = Strings.Loading;

                    if (_story)
                    {
                        IsPrimaryButtonEnabled = false;
                    }
                    break;
                case "move_end":
                    LiftPin(false);
                    UpdateCenter(ReadLocation(data), null);

                    if (_centerMoved)
                    {
                        SelectCenter();
                    }
                    break;
                case "address":
                    UpdateCenter(ReadLocation(data), data.GetNamedString("address", string.Empty));
                    break;
            }
        }

        private void InitializeMap()
        {
            var location = _center ?? ViewModel.Location;

            // Without a fix the page frames this country instead.
            string region;
            try
            {
                region = new GeographicRegion().CodeTwoLetter;
            }
            catch
            {
                region = string.Empty;
            }

            var json = new JsonObject
            {
                ["token"] = JsonValue.CreateStringValue(_mapToken),
                ["geo"] = JsonValue.CreateStringValue(_geoToken),
                ["language"] = JsonValue.CreateStringValue(LocaleService.Current.Id),
                ["dark"] = JsonValue.CreateBooleanValue(ActualTheme == ElementTheme.Dark),
                ["region"] = JsonValue.CreateStringValue(region),
                ["latitude"] = location != null ? JsonValue.CreateNumberValue(location.Latitude) : JsonValue.CreateNullValue(),
                ["longitude"] = location != null ? JsonValue.CreateNumberValue(location.Longitude) : JsonValue.CreateNullValue()
            };

            _mapInitialized = true;
            _ = _mapView?.CoreWebView2?.ExecuteScriptAsync($"Picker.init({json.Stringify()});");
        }

        // Before the page is initialized there is no map to move: InitializeMap reads the latest
        // location when it runs.
        private void FlyTo(Location location)
        {
            if (!_mapInitialized)
            {
                return;
            }

            var latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
            var longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);

            _ = _mapView?.CoreWebView2?.ExecuteScriptAsync($"Picker.flyTo({latitude}, {longitude});");
        }

        private static Location ReadLocation(JsonObject data)
        {
            return new Location(data.GetNamedNumber("latitude", 0), data.GetNamedNumber("longitude", 0), 0);
        }

        private void LiftPin(bool lift)
        {
            var animation = _position.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(1, new Vector3(0, lift ? -12 : 0, 0));
            animation.Duration = TimeSpan.FromMilliseconds(200);

            _position.StartAnimation("Translation", animation);
        }

        // Without a name from the geocoder, the point is named by its coordinates.
        private void UpdateCenter(Location location, string address)
        {
            _center = location;
            _centerAddress = string.IsNullOrEmpty(address)
                ? string.Format(CultureInfo.InvariantCulture, "({0:F6}, {1:F6})", location.Latitude, location.Longitude)
                : address;

            // The geocoder runs a while after the map stops, so until then the cell waits for it.
            if (address != null || _mapView == null)
            {
                SelectedLocation.Address = _centerAddress;
            }

            if (_centerSelected && Media is InputMessageLocation input)
            {
                input.Location = location;
            }

            SelectedLocationButton.Visibility = Visibility.Visible;

            if (_story)
            {
                _place = new PaintEntity
                {
                    LocationTitle = _centerAddress,
                    LocationLatitude = location.Latitude,
                    LocationLongitude = location.Longitude
                };

                IsPrimaryButtonEnabled = address != null || _mapView == null;
            }
        }

        #endregion

        #region Story

        /// <summary>
        /// Writes the place picked for a story into <paramref name="location"/>.
        /// </summary>
        public void ApplyTo(PaintEntity location)
        {
            if (_place == null)
            {
                return;
            }

            location.LocationTitle = _place.LocationTitle;
            location.LocationLatitude = _place.LocationLatitude;
            location.LocationLongitude = _place.LocationLongitude;
            location.LocationQueryId = 0;
            location.LocationResultId = null;
        }

        #endregion
    }
}
