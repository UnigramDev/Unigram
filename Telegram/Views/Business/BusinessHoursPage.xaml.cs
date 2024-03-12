using System;
using System.Globalization;
using Telegram.Td.Api;
using Telegram.ViewModels.Business;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Business
{
    public sealed partial class BusinessHoursPage : HostedPage
    {
        public BusinessHoursViewModel ViewModel => DataContext as BusinessHoursViewModel;

        public BusinessHoursPage()
        {
            InitializeComponent();
            Title = Strings.BusinessHours;
        }

        #region Binding

        private ImageSource ConvertLocation(bool valid, Location location)
        {
            if (valid)
            {
                var latitude = location.Latitude.ToString(CultureInfo.InvariantCulture);
                var longitude = location.Longitude.ToString(CultureInfo.InvariantCulture);

                return new BitmapImage(new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, 15, "320,200")));
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
    }
}
