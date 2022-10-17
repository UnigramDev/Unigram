using System;
using System.Globalization;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls.Messages.Content
{
    public sealed class LocationContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public LocationContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(LocationContent);
        }

        #region InitializeComponent

        private ImageView Texture;
        private ProfilePicture PinPhoto;
        private Path PinDot;
        private Grid LivePanel;
        private Run Title;
        private Run Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Texture = GetTemplateChild(nameof(Texture)) as ImageView;
            PinPhoto = GetTemplateChild(nameof(PinPhoto)) as ProfilePicture;
            PinDot = GetTemplateChild(nameof(PinDot)) as Path;
            LivePanel = GetTemplateChild(nameof(LivePanel)) as Grid;
            Title = GetTemplateChild(nameof(Title)) as Run;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as Run;

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

            var location = message.Content as MessageLocation;
            if (location == null || !_templateApplied)
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
                var expired = Converter.DateTime(message.Date + location.LivePeriod) < DateTime.Now;
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

                    PinPhoto.SetMessageSender(message.ClientService, message.SenderId, 32);

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

            //if (location.LivePeriod > 0)
            //{
            //    _message.Delegate.OpenLiveLocation(_message);
            //}
            //else
            {
                if (_message.ClientService.TryGetUser(_message.SenderId, out User senderUser))
                {
                    _message.Delegate.OpenLocation(location.Location, senderUser.FullName());
                }
                else if (_message.ClientService.TryGetChat(_message.SenderId, out Chat senderChat))
                {
                    _message.Delegate.OpenLocation(location.Location, _message.ClientService.GetTitle(senderChat));
                }
            }
        }
    }
}
