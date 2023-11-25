//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Popups
{
    public sealed partial class SendLocationPopup : ContentPopup
    {
        public SendLocationViewModel ViewModel => DataContext as SendLocationViewModel;

        private CompositionAnimation _previewShimmer;
        private Geolocator _geolocator;

        private readonly Visual _position;
        private readonly Visual _accuracy;
        private float _accuracyRadius;

        public InputMessageContent Media { get; private set; }

        public SendLocationPopup()
        {
            InitializeComponent();
            DataContext = TypeResolver.Current.Resolve<SendLocationViewModel>();

            Title = Strings.AttachLocation;

            PrimaryButtonText = Strings.Send;
            SecondaryButtonText = Strings.Cancel;

            MapPresenter.Constraint = new Size(16, 9);

            Accuracy.Width = 0;
            Accuracy.Height = 0;

            Loaded += OnLoaded;

            _accuracy = ElementCompositionPreview.GetElementVisual(Accuracy);
            _position = ElementCompositionPreview.GetElementVisual(Position);

            _position.CenterPoint = new Vector3(20, 48, 0);
            _position.Scale = Vector3.Zero;

            _previewShimmer = CompositionPathParser.CreateThumbnail(16, 9, 0, out ShapeVisual visual);
            ElementCompositionPreview.SetElementChildVisual(MapShimmer, visual);
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {

        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (_geolocator != null)
            {
                _geolocator.PositionChanged -= OnPositionChanged;
                _geolocator = null;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            FindLocation();
        }

        private async void FindLocation()
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            if (accessStatus == GeolocationAccessStatus.Allowed)
            {
                if (_geolocator == null)
                {
                    _geolocator = new Geolocator { DesiredAccuracy = PositionAccuracy.Default };
                    _geolocator.PositionChanged += OnPositionChanged;

                    await _geolocator.GetGeopositionAsync();
                }
                else
                {
                    if (_previewShimmer == null)
                    {
                        _previewShimmer = CompositionPathParser.CreateThumbnail(320, 200, 0, out ShapeVisual visual);
                        ElementCompositionPreview.SetElementChildVisual(MapShimmer, visual);
                    }

                    UpdateLocation(await _geolocator.GetGeopositionAsync());
                }
            }
        }

        private async void OnPositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            await Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
                () => UpdateLocation(args.Position));
        }

        private void UpdateLocation(Geoposition point)
        {
            ViewModel.Location = new Location(
                point.Coordinate.Point.Position.Latitude,
                point.Coordinate.Point.Position.Longitude,
                point.Coordinate.Accuracy);

            if (Media is InputMessageLocation input)
            {
                input.Location = ViewModel.Location;
            }

            if (_previewShimmer == null)
            {
                _previewShimmer = CompositionPathParser.CreateThumbnail(320, 200, 0, out ShapeVisual visual);
                ElementCompositionPreview.SetElementChildVisual(MapShimmer, visual);
            }

            var latitude = point.Coordinate.Point.Position.Latitude;
            var longitude = point.Coordinate.Point.Position.Longitude;

            var width = MapPresenter.ActualWidth * WindowContext.Current.RasterizationScale;
            var height = MapPresenter.ActualHeight * WindowContext.Current.RasterizationScale;

            var pixels = 96 * WindowContext.Current.RasterizationScale;
            var scale = pixels * 39.37 * 156543.04 * Math.Cos(latitude * Math.PI / 180) / Math.Pow(2, 15);
            var accuracy = point.Coordinate.Accuracy * 39.37;

            var radius = (float)(accuracy / scale * pixels);
            if (radius != _accuracyRadius)
            {
                if (_position.Scale == Vector3.Zero)
                {
                    var spring = _accuracy.Compositor.CreateSpringVector3Animation();
                    spring.InitialValue = Vector3.Zero;
                    spring.FinalValue = Vector3.One;
                    spring.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
                    spring.DampingRatio = 0.6f;

                    _position.StartAnimation("Scale", spring);
                }

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

            Map.Source = new BitmapImage(new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3:F0},{4:F0}&key={5}",
                latitude, longitude, 15, width, height, Constants.BingMapsApiKey)));

            CurrentLocation.Address = string.Format(Strings.AccurateTo,
                Locale.Declension(Strings.R.Meters, (int)point.Coordinate.Accuracy));

            if (ScrollingHost.SelectedItem == null)
            {
                CurrentLocation.UpdateState(true, true, true);
                Media = new InputMessageLocation(ViewModel.Location, 0, 0, 0);
            }
        }

        private void CurrentLocation_Click(object sender, RoutedEventArgs e)
        {
            ScrollingHost.SelectedItem = null;

            CurrentLocation.UpdateState(true, true, true);
            Media = new InputMessageLocation(ViewModel.Location, 0, 0, 0);
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new MultipleListViewItem(sender, false);
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as VenueCell;
            var venue = args.Item as Venue;

            content.UpdateVenue(venue);
            content.UpdateState(sender.SelectionMode == ListViewSelectionMode.Multiple
                && args.ItemContainer.IsSelected, false, true);
        }

        #endregion

        private void Map_ImageOpened(object sender, RoutedEventArgs e)
        {
            _previewShimmer = null;
            ElementCompositionPreview.SetElementChildVisual(MapShimmer, null);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is Venue venue)
            {
                CurrentLocation.UpdateState(false, true, true);
                Media = new InputMessageVenue(venue);
            }
        }
    }
}
