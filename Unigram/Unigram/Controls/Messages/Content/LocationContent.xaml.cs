using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class LocationContent : StackPanel, IContent
    {
        private MessageViewModel _message;

        public LocationContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var location = message.Content as MessageLocation;
            if (location == null)
            {
                return;
            }

            var latitude = location.Location.Latitude.ToString(CultureInfo.InvariantCulture);
            var longitude = location.Location.Longitude.ToString(CultureInfo.InvariantCulture);

            Texture.Source = new BitmapImage(new Uri(string.Format("http://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, 15, "320,200")));
            Texture.Constraint = message;

            //VenueDot.Visibility = Visibility.Visible;
            //VenueGlyph.UriSource = null;

            if (location.LivePeriod > 0)
            {
                var expired = BindConvert.Current.DateTime(message.Date + location.LivePeriod) < DateTime.Now;
                if (expired)
                {
                    LivePanel.Visibility = Visibility.Collapsed;
                    PinDot.Visibility = Visibility.Visible;
                    PinPhoto.Source = null;
                }
                else
                {
                    LivePanel.Visibility = Visibility.Visible;
                    PinDot.Visibility = Visibility.Collapsed;

                    var user = _message.ProtoService.GetUser(message.SenderUserId);
                    if (user != null)
                    {
                        PinPhoto.Source = PlaceholderHelper.GetUser(message.ProtoService, user, 32);
                    }

                    Title.Text = Strings.Resources.AttachLiveLocation;
                    Subtitle.Text = Locale.FormatLocationUpdateDate(message.EditDate > 0 ? message.EditDate : message.Date);
                }
            }
            else
            {
                LivePanel.Visibility = Visibility.Collapsed;
                PinDot.Visibility = Visibility.Visible;
                PinPhoto.Source = null;
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageLocation;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var location = _message.Content as MessageLocation;
            if (location == null)
            {
                return;
            }

            if (location.LivePeriod > 0)
            {
                _message.Delegate.OpenLiveLocation(_message);
            }
            else
            {
                var user = _message.GetSenderUser();
                if (user != null)
                {
                    _message.Delegate.OpenLocation(location.Location, user.GetFullName());
                }
            }
        }
    }
}
