using System;
using System.Globalization;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Messages.Content
{
    public sealed class VenueContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public VenueContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(VenueContent);
        }

        #region InitializeComponent

        private ImageView Texture;
        private BitmapIcon VenueGlyph;
        private Path VenueDot;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Texture = GetTemplateChild(nameof(Texture)) as ImageView;
            VenueGlyph = GetTemplateChild(nameof(VenueGlyph)) as BitmapIcon;
            VenueDot = GetTemplateChild(nameof(VenueDot)) as Path;

            Texture.Click += Button_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var venue = message.Content as MessageVenue;
            if (venue == null || !_templateApplied)
            {
                return;
            }

            var latitude = venue.Venue.Location.Latitude.ToString(CultureInfo.InvariantCulture);
            var longitude = venue.Venue.Location.Longitude.ToString(CultureInfo.InvariantCulture);

            Texture.Source = new BitmapImage(new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3}&key=FgqXCsfOQmAn9NRf4YJ2~61a_LaBcS6soQpuLCjgo3g~Ah_T2wZTc8WqNe9a_yzjeoa5X00x4VJeeKH48wAO1zWJMtWg6qN-u4Zn9cmrOPcL", latitude, longitude, 15, "320,200")));
            Texture.Constraint = message;

            if (string.IsNullOrEmpty(venue.Venue.Type))
            {
                VenueDot.Visibility = Visibility.Visible;
                VenueGlyph.UriSource = null;
            }
            else
            {
                VenueDot.Visibility = Visibility.Collapsed;
                VenueGlyph.UriSource = new Uri(string.Format("https://ss3.4sqi.net/img/categories_v2/{0}_88.png", venue.Venue.Type));
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageVenue;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var venue = _message.Content as MessageVenue;
            if (venue == null)
            {
                return;
            }

            if (_message.ClientService.TryGetUser(_message.SenderId, out User senderUser))
            {
                _message.Delegate.OpenLocation(venue.Venue.Location, senderUser.FullName());
            }
            else if (_message.ClientService.TryGetChat(_message.SenderId, out Chat senderChat))
            {
                _message.Delegate.OpenLocation(venue.Venue.Location, _message.ClientService.GetTitle(senderChat));
            }
        }
    }
}
