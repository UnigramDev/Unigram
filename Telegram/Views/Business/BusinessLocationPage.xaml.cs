using System;
using System.Globalization;
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessLocationPage : HostedPage
    {
        public BusinessLocationViewModel ViewModel => DataContext as BusinessLocationViewModel;

        public BusinessLocationPage()
        {
            InitializeComponent();
            Title = Strings.BusinessLocation;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ADDRESS_INVALID")
            {
                VisualUtilities.ShakeView(Address);
            }
        }

        #region Binding

        private ImageSource ConvertLocation(bool valid, Location location)
        {
            if (valid)
            {
                var width = 1000 * XamlRoot.RasterizationScale;
                var height = 200 * XamlRoot.RasterizationScale;

                var latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
                var longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);

                return new BitmapImage(new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3:F0},{4:F0}&key={5}",
                    latitude, longitude, 15, width, height, Constants.BingMapsApiKey)));
            }

            return null;
        }

        private Visibility ConvertClear(string address, bool valid)
        {
            return string.IsNullOrEmpty(address) && !valid
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Address.Focus(FocusState.Pointer);
            Address.SelectionStart = int.MaxValue;
        }
    }
}
