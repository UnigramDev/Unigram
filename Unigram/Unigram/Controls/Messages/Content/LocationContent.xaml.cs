using System;
using System.Globalization;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class LocationContent : StackPanel, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

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

            Texture.Source = new BitmapImage(new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, 15, "320,200")));
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

                    if (_message.ProtoService.TryGetUser(message.Sender, out User senderUser))
                    {
                        PinPhoto.Source = PlaceholderHelper.GetUser(message.ProtoService, senderUser, 32);
                    }
                    else if (_message.ProtoService.TryGetChat(message.Sender, out Chat senderChat))
                    {
                        PinPhoto.Source = PlaceholderHelper.GetChat(message.ProtoService, senderChat, 32);
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
                if (_message.ProtoService.TryGetUser(_message.Sender, out User senderUser))
                {
                    _message.Delegate.OpenLocation(location.Location, senderUser.GetFullName());
                }
                else if (_message.ProtoService.TryGetChat(_message.Sender, out Chat senderChat))
                {
                    _message.Delegate.OpenLocation(location.Location, _message.ProtoService.GetTitle(senderChat));
                }
            }
        }
    }
}
