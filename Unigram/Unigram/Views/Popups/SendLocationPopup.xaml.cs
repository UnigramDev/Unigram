using LinqToVisualTree;
using System;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.ViewModels;
using Windows.Devices.Geolocation;
using Windows.Foundation.Metadata;
using Windows.Services.Maps;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Popups
{
    public sealed partial class SendLocationPopup : ContentPopup
    {
        public SendLocationViewModel ViewModel => DataContext as SendLocationViewModel;

        private MapIcon userPos;
        private Geoposition _lastPosition;

        public InputMessageContent Media { get; private set; }

        //private bool? _liveLocation;
        //public bool? LiveLocation
        //{
        //    get
        //    {
        //        return _liveLocation;
        //    }
        //    set
        //    {
        //        _liveLocation = value;

        //        LiveLocationButton.Visibility = value.HasValue ? Visibility.Visible : Visibility.Collapsed;
        //        LiveLocationLabel.Text = value == true ? Strings.Resources.SendLiveLocation : Strings.Resources.StopLiveLocation;

        //        LiveLocationButton.Visibility = Visibility.Collapsed;
        //    }
        //}

        public SendLocationPopup()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<SendLocationViewModel>();

            //PrimaryButtonText = Strings.Resources.Send;
            //SecondaryButtonText = Strings.Resources.Cancel;

            Loaded += OnLoaded;

            var observable = Observable.FromEventPattern<object>(mMap, "CenterChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(500)).ObserveOnDispatcher().Subscribe(async x =>
            {
                await UpdateLocationAsync(mMap.Center);
            });

            var observable1 = Observable.FromEventPattern<TextChangedEventArgs>(SearchField, "TextChanged");
            var throttled1 = observable1.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    ViewModel.Search = null;
                }
                else
                {
                    ViewModel.Find(SearchField.Text);
                }
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
            Media = new InputMessageLocation(new Location(mMap.Center.Position.Latitude, mMap.Center.Position.Longitude, 0), 0, 0, 0);
            Hide(ContentDialogResult.Primary);
        }

        //private async void LiveLocation_Click(object sender, RoutedEventArgs e)
        //{
        //    if (LiveLocation == true)
        //    {
        //        var dialog = new SelectLivePeriodView(false, null);
        //        var confirm = await dialog.ShowQueuedAsync();
        //        if (confirm == ContentDialogResult.Primary && _lastPosition != null)
        //        {
        //            Media = new InputMessageLocation(new Location(_lastPosition.Coordinate.Point.Position.Latitude, _lastPosition.Coordinate.Point.Position.Longitude), dialog.Period);
        //            Dialog.Hide(ContentDialogResult.Primary);
        //        }
        //    }
        //    else if (LiveLocation == false)
        //    {
        //        //Media = new TLMessageMediaGeoLive();
        //        Dialog.Hide(ContentDialogResult.Primary);
        //    }
        //}

        private void NearbyList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Venue venue)
            {
                Media = new InputMessageVenue(venue);
                Hide(ContentDialogResult.Primary);
            }
        }

        #region Search

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            MainHeader.Visibility = Visibility.Collapsed;
            SearchField.Visibility = Visibility.Visible;

            SearchField.Focus(FocusState.Keyboard);
        }

        private void Search_GotFocus(object sender, RoutedEventArgs e)
        {
            Search_TextChanged(null, null);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                MainHeader.Visibility = Visibility.Visible;
                SearchField.Visibility = Visibility.Collapsed;

                NearbyList.Focus(FocusState.Programmatic);
            }

            Search_TextChanged(null, null);
        }

        private async void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Unfocused && string.IsNullOrEmpty(SearchField.Text))
            {
                NearbyList.Visibility = Visibility.Visible;

                ViewModel.Search = null;
            }
            else if (SearchField.FocusState != FocusState.Unfocused)
            {
                NearbyList.Visibility = Visibility.Collapsed;

                //var items = ViewModel.Contacts.Search = new SearchUsersCollection(ViewModel.ProtoService, SearchField.Text);
                //await items.LoadMoreItemsAsync(0);
            }
        }

        private void Search_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            //var activePanel = rpMasterTitlebar.SelectedIndex == 0 ? DialogsPanel : ContactsPanel;
            //var activeList = rpMasterTitlebar.SelectedIndex == 0 ? DialogsSearchListView : ContactsSearchListView;
            //var activeResults = rpMasterTitlebar.SelectedIndex == 0 ? ChatsResults : ContactsResults;

            //if (activePanel.Visibility == Visibility.Visible)
            //{
            //    return;
            //}

            //if (e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down)
            //{
            //    var index = e.Key == Windows.System.VirtualKey.Up ? -1 : 1;
            //    var next = activeList.SelectedIndex + index;
            //    if (next >= 0 && next < activeResults.View.Count)
            //    {
            //        activeList.SelectedIndex = next;
            //        activeList.ScrollIntoView(activeList.SelectedItem);
            //    }

            //    e.Handled = true;
            //}
            //else if (e.Key == Windows.System.VirtualKey.Enter)
            //{
            //    var index = Math.Max(activeList.SelectedIndex, 0);
            //    var container = activeList.ContainerFromIndex(index) as ListViewItem;
            //    if (container != null)
            //    {
            //        var peer = new ListViewItemAutomationPeer(container);
            //        var invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
            //        invokeProv.Invoke();
            //    }

            //    e.Handled = true;
            //}
        }

        #endregion

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var venue = args.Item as Venue;

            var border = content.Children[0] as Border;
            var bitmap = border.Child as BitmapIcon;

            border.Background = PlaceholderHelper.GetBrush(venue.Id.GetHashCode());
            bitmap.UriSource = new Uri(string.Format("https://ss3.4sqi.net/img/categories_v2/{0}_88.png", venue.Type));
        }

        #endregion
    }

    public class Poi
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public Poi() { }
    }
}
