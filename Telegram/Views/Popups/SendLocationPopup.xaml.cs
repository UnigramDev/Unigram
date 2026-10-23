//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Entities;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Devices.Geolocation;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Popups
{
    public sealed partial class SendLocationPopup : ContentPopup
    {
        public SendLocationViewModel ViewModel => DataContext as SendLocationViewModel;

        private ShapeVisual _previewShimmer;
        private Geolocator _geolocator;

        private readonly Visual _position;
        private readonly Visual _accuracy;
        private float _accuracyRadius;

        public InputMessageContent Media { get; private set; }

        // Picking a story's venue, around where it starts.
        private readonly bool _story;
        private readonly Location _start;

        private PaintEntity _place;

        /// <summary>
        /// Picks the venue a story's location shows, among those around <paramref name="start"/>,
        /// or the device when it is null. <see cref="ApplyTo"/> hands it over.
        /// </summary>
        public SendLocationPopup(ISession session, Location start)
            : this(session)
        {
            _story = true;
            _start = start;

            PrimaryButtonText = Strings.Done;
            IsPrimaryButtonEnabled = false;

            // Only the venues: neither the map nor the device's own location.
            MapPresenter.Visibility = Visibility.Collapsed;
            CurrentLocationButton.Visibility = Visibility.Collapsed;
            NearbyHeader.Visibility = Visibility.Collapsed;
        }

        public SendLocationPopup(ISession session)
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

        // Once, rather than following the device as a message's location does: the venues are
        // the ones around where it starts, which setting it searches for.
        private async void FindStoryLocation()
        {
            var location = _start ?? await ViewModel.Session.Resolve<ILocationService>().GetPositionAsync(WindowContext.GetNavigationService(this));
            if (location != null)
            {
                ViewModel.Location = location;
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

                    try
                    {
                        await _geolocator.GetGeopositionAsync();
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

            if (_previewShimmer == null && (ViewModel?.Location.Latitude != point.Coordinate.Point.Position.Latitude || ViewModel?.Location.Longitude != point.Coordinate.Point.Position.Longitude))
            {
                CompositionPathParser.CreateThumbnail(16, 9, 0, out ShapeVisual visual);
                ElementCompositionPreview.SetElementChildVisual(MapShimmer, visual);
                _previewShimmer = visual;
            }

            var latitude = point.Coordinate.Point.Position.Latitude;
            var longitude = point.Coordinate.Point.Position.Longitude;

            var rasterization = XamlRoot.RasterizationScale;

            var width = (int)(MapPresenter.ActualWidth);
            var height = (int)(MapPresenter.ActualHeight);

            var pixels = 96 * rasterization;
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

            Map.SetSource(ViewModel.ClientService, ViewModel.Location, width, height, 0);

            CurrentLocation.Address = string.Format(Strings.AccurateTo,
                Locale.Declension(Strings.R.Meters, (int)point.Coordinate.Accuracy));

            if (ScrollingHost.SelectedItem == null)
            {
                CurrentLocation.UpdateState(true, true, true);
                Media = new InputMessageLocation(ViewModel.Location);
            }
        }

        private void CurrentLocation_Click(object sender, RoutedEventArgs e)
        {
            ScrollingHost.SelectedItem = null;

            CurrentLocation.UpdateState(true, true, true);
            Media = new InputMessageLocation(ViewModel.Location);
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
            else if (args.ItemContainer.ContentTemplateRoot is VenueCell content && args.Item is Venue venue)
            {
                content.UpdateVenue(venue);
                content.UpdateState(sender.SelectionMode == ListViewSelectionMode.Multiple
                    && args.ItemContainer.IsSelected, false, true);

                args.Handled = true;
            }
        }

        #endregion

        private void Map_ImageOpened(object sender, RoutedEventArgs e)
        {
            CompositionPathParser.StopThumbnail(_previewShimmer);
            _previewShimmer = null;
            ElementCompositionPreview.SetElementChildVisual(MapShimmer, null);
        }

        #region Story

        /// <summary>
        /// Writes the venue picked for a story into <paramref name="location"/>.
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
            location.LocationQueryId = _place.LocationQueryId;
            location.LocationResultId = _place.LocationResultId;
        }

        #endregion

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is Venue venue)
            {
                CurrentLocation.UpdateState(false, true, true);
                Media = new InputMessageVenue(venue);

                if (_story)
                {
                    ViewModel.TryGetResult(venue, out long queryId, out string resultId);

                    _place = new PaintEntity
                    {
                        LocationTitle = venue.Title,
                        LocationLatitude = venue.Location.Latitude,
                        LocationLongitude = venue.Location.Longitude,
                        LocationQueryId = queryId,
                        LocationResultId = resultId
                    };

                    IsPrimaryButtonEnabled = true;
                }
            }
        }
    }
}
