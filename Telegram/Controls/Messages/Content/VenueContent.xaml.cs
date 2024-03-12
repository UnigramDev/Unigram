//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Globalization;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
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

            var width = 320 * WindowContext.Current.RasterizationScale;
            var height = 200 * WindowContext.Current.RasterizationScale;

            var latitude = venue.Venue.Location.Latitude.ToString(CultureInfo.InvariantCulture);
            var longitude = venue.Venue.Location.Longitude.ToString(CultureInfo.InvariantCulture);

            Texture.Constraint = message;
            Texture.Source = new BitmapImage(new Uri(string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3:F0},{4:F0}&key={5}",
                latitude, longitude, 15, width, height, Constants.BingMapsApiKey)));

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

        public void Recycle()
        {
            _message = null;
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
