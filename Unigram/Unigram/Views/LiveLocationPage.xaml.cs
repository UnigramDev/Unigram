using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views
{
    public sealed partial class LiveLocationPage : Page, ILiveLocationDelegate
    {
        public LiveLocationViewModel ViewModel => DataContext as LiveLocationViewModel;

        public LiveLocationPage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<LiveLocationViewModel, ILiveLocationDelegate>(this);
        }

        public void UpdateNewMessage(Message message)
        {
            var location = message.Content as MessageLocation;
            if (location == null)
            {
                return;
            }

            var pin = new Button();
            pin.Tag = message.Id;
            pin.Content = message.Id;
            MapControl.SetLocation(pin, new Windows.Devices.Geolocation.Geopoint(new Windows.Devices.Geolocation.BasicGeoposition { Latitude = location.Location.Latitude, Longitude = location.Location.Longitude }));
            MapControl.SetNormalizedAnchorPoint(pin, new Point(0.5, 1));

            Map.Children.Add(pin);
            //throw new NotImplementedException();
        }

        public void UpdateMessageContent(Message message)
        {
            var pin = Map.Children.OfType<Button>().FirstOrDefault(x => x.Tag is int id && id == message.Id);
            if (pin == null)
            {
                return;
            }

            var location = message.Content as MessageLocation;
            if (location == null)
            {
                return;
            }

            MapControl.SetLocation(pin, new Windows.Devices.Geolocation.Geopoint(new Windows.Devices.Geolocation.BasicGeoposition { Latitude = location.Location.Latitude, Longitude = location.Location.Longitude }));

            //throw new NotImplementedException();
        }
    }
}
