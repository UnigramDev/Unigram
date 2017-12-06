using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls.Maps;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.Devices.Input;
using System.Diagnostics;
using Windows.UI.Composition;
using Windows.UI.Xaml.Hosting;
using System.Numerics;
using Windows.Foundation.Metadata;
using LinqToVisualTree;
using Unigram.Views;
using Unigram.ViewModels;
using Windows.Services.Maps;
using Unigram.Controls;
using Telegram.Api.TL;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Unigram.Common;
using Unigram.Core.Services;
using Unigram.Controls.Views;
using Unigram.ViewModels.Dialogs;

namespace Unigram.Views.Dialogs
{
    public sealed partial class DialogShareLocationPage : Page
    {
        public DialogShareLocationViewModel ViewModel => DataContext as DialogShareLocationViewModel;

        private MapIcon userPos;
        private Geoposition _lastPosition;

        public TLMessageMediaBase Media { get; private set; }
        public ContentDialogBase Dialog { get; set; }

        private bool? _liveLocation;
        public bool? LiveLocation
        {
            get
            {
                return _liveLocation;
            }
            set
            {
                _liveLocation = value;

                LiveLocationButton.Visibility = value.HasValue ? Visibility.Visible : Visibility.Collapsed;
                LiveLocationLabel.Text = value == true ? Strings.Android.SendLiveLocation : Strings.Android.StopLiveLocation;
            }
        }

        public DialogShareLocationPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<DialogShareLocationViewModel>();

            Loaded += OnLoaded;

            var observable = Observable.FromEventPattern<object>(mMap, "CenterChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(500)).ObserveOnDispatcher().Subscribe(async x =>
            {
                await UpdateLocationAsync(mMap.Center);
            });
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeMap();
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            InitializeMap();
        }

        private void BtnLayerChange_Click(object sender, RoutedEventArgs e)
        {
            if (mMap.Style == MapStyle.Road)
            {
                mMap.Style = MapStyle.AerialWithRoads;
            }
            else
            {
                mMap.Style = MapStyle.Road;
            }
        }

        // Init map
        private void InitializeMap()
        {
            var space = (ActualHeight * 40) / 100;

            MapPresenter.Height = ActualHeight;
            MapPresenter.Margin = new Thickness(0, -(space / 2), 0, -(space / 2));

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 2))
            {
                var scrollingHost = NearbyList.Descendants<ScrollViewer>().FirstOrDefault() as ScrollViewer;

                var scrollerViewerManipulation = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(scrollingHost);

                var compositor = scrollerViewerManipulation.Compositor;

                var expression = compositor.CreateExpressionAnimation("-(ScrollManipulation.Translation.Y / 2)");
                expression.SetScalarParameter("ParallaxMultiplier", (float)(space / 2));
                expression.SetReferenceParameter("ScrollManipulation", scrollerViewerManipulation);

                var heroVisual = ElementCompositionPreview.GetElementVisual(MapPresenter);
                heroVisual.CenterPoint = new Vector3((float)(MapPresenter.ActualWidth / 2), (float)MapPresenter.ActualHeight, 0);
                heroVisual.StartAnimation("Offset.Y", expression);
            }

            mMap.Style = MapStyle.Road;
            mMap.ZoomLevel = 10;
            userPos = new MapIcon();
            userPos.ZIndex = 0;
            userPos.Image = RandomAccessStreamReference.CreateFromUri(new Uri("ms-appx:///Assets/Icons/userPos32.png"));
            userPos.Visible = false;
            mMap.MapElements.Add(userPos);
            FindLocation();
        }

        private async void FindLocation()
        {
            var accessStatus = await Geolocator.RequestAccessAsync();

            if (accessStatus == GeolocationAccessStatus.Allowed)
            {
                //case GeolocationAccessStatus.Allowed:

                Geolocator geolocator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };

                // Subscribe to the StatusChanged event to get updates of location status changes.
                //_geolocator.StatusChanged += OnStatusChanged;

                // Carry out the operation.
                Geoposition pos = await geolocator.GetGeopositionAsync();

                mMap.Center = new Geopoint(new BasicGeoposition
                {
                    Latitude = pos.Coordinate.Point.Position.Latitude,
                    Longitude = pos.Coordinate.Point.Position.Longitude
                });
                mMap.ZoomLevel = 15;
                userPos.Location = mMap.Center;
                userPos.Visible = true;
                mMap.MapElements.Remove(userPos);
                mMap.MapElements.Add(userPos);
                _lastPosition = pos;

                // Get address for current location
                await UpdateLocationAsync(mMap.Center);

                // Other cases
                // TODO: When shit gets serious
                //
                // case GeolocationAccessStatus.Denied:
                // case GeolocationAccessStatus.Allowed:
                // case GeolocationAccessStatus.Unspecified:
            }
        }

        private async Task UpdateLocationAsync(Geopoint point)
        {
            tblCurrentLocation.Text = "Getting your location...";

            var result = await MapLocationFinder.FindLocationsAtAsync(point);
            if (result.Status == MapLocationFinderStatus.Success)
            {
                var location = result.Locations.FirstOrDefault();
                if (location != null)
                {
                    tblCurrentLocation.Text = location.Address.FormattedAddress;
                }
                else
                {
                    tblCurrentLocation.Text = "Unknown location";
                }
            }
            else
            {
                tblCurrentLocation.Text = "Unknown location";
            }
        }

        private void BtnLocate_Click(object sender, RoutedEventArgs e)
        {
            FindLocation();
        }

        private void CurrentLocation_Click(object sender, RoutedEventArgs e)
        {
            Media = new TLMessageMediaGeo { Geo = new TLGeoPoint { Lat = mMap.Center.Position.Latitude, Long = mMap.Center.Position.Longitude } };
            Dialog.Hide(ContentDialogBaseResult.OK);
        }

        private async void LiveLocation_Click(object sender, RoutedEventArgs e)
        {
            if (LiveLocation == true)
            {
                var dialog = new SelectLivePeriodView(false, null);
                var confirm = await dialog.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary && _lastPosition != null)
                {
                    Media = new TLMessageMediaGeoLive { Geo = new TLGeoPoint { Lat = _lastPosition.Coordinate.Point.Position.Latitude, Long = _lastPosition.Coordinate.Point.Position.Longitude }, Period = dialog.Period };
                    Dialog.Hide(ContentDialogBaseResult.OK);
                }
            }
            else if (LiveLocation == false)
            {
                Media = new TLMessageMediaGeoLive();
                Dialog.Hide(ContentDialogBaseResult.OK);
            }
        }

        private void NearbyList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLMessageMediaVenue venue)
            {
                Media = venue;
                Dialog.Hide(ContentDialogBaseResult.OK);
            }
        }
    }

    public class Poi
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public Poi() { }
    }

    public class OpacityMask : Control
    {
        private Compositor _compositor;
        private SpriteVisual _visual;

        public OpacityMask()
        {
            var mask = ElementCompositionPreview.GetElementVisual(this);

            _compositor = mask.Compositor;
            _visual = _compositor.CreateSpriteVisual();
            _visual.Size = new Vector2(32, 32);

            ElementCompositionPreview.SetElementChildVisual(this, _visual);
        }

        #region Source

        public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(Uri), typeof(OpacityMask), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((OpacityMask)d).OnSourceChanged((Uri)e.NewValue);
        }

        private async void OnSourceChanged(Uri newValue)
        {
            var surface = await ImageLoader.Instance.LoadFromUriAsync(newValue);
            if (surface != null)
            {
                var mask = _compositor.CreateMaskBrush();
                var overlay = _compositor.CreateColorBrush(Colors.Red);

                mask.Mask = overlay;
                mask.Source = surface.Brush;

                _visual.Brush = mask;
            }
        }

        #endregion
    }
}
