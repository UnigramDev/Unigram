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
using Unigram.Core.Dependency;
using Unigram.ViewModels;
using Windows.Services.Maps;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DialogSendLocationPage : Page
    {
        public DialogSendLocationViewModel ViewModel => DataContext as DialogSendLocationViewModel;

        private MapIcon userPos;

        public DialogSendLocationPage()
        {
            InitializeComponent();

            DataContext = UnigramContainer.Current.ResolveType<DialogSendLocationViewModel>();

            Loaded += OnLoaded;
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
                    Latitude = pos.Coordinate.Latitude,
                    Longitude = pos.Coordinate.Longitude
                });
                mMap.ZoomLevel = 15;
                userPos.Location = mMap.Center;
                userPos.Visible = true;
                mMap.MapElements.Remove(userPos);
                mMap.MapElements.Add(userPos);

                // Get address for current location
                var result = await MapLocationFinder.FindLocationsAtAsync(new Geopoint(new BasicGeoposition
                {
                    Latitude = pos.Coordinate.Latitude,
                    Longitude = pos.Coordinate.Longitude
                }));
                if (result.Status == MapLocationFinderStatus.Success)
                {
                    string selectedAddress = result.Locations[0].Address.FormattedAddress;
                    tblCurrentLocation.Text = selectedAddress;
                }

                // Other cases
                // TO-DO When shit gets serious
                //
                // case GeolocationAccessStatus.Denied:
                // case GeolocationAccessStatus.Allowed:
                // case GeolocationAccessStatus.Unspecified:
            }
        }

        private void BtnLocate_Click(object sender, RoutedEventArgs e)
        {
            FindLocation();
        }
    }

    public class Poi
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public Poi() { }
    }
}
