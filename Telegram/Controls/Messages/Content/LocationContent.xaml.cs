//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Globalization;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
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
        private TextBlock LivePeriod;
        private SelfDestructTimer LiveRing;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Texture = GetTemplateChild(nameof(Texture)) as ImageView;
            PinPhoto = GetTemplateChild(nameof(PinPhoto)) as ProfilePicture;
            PinDot = GetTemplateChild(nameof(PinDot)) as Path;
            LivePanel = GetTemplateChild(nameof(LivePanel)) as Grid;
            Title = GetTemplateChild(nameof(Title)) as Run;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as Run;
            LivePeriod = GetTemplateChild(nameof(LivePeriod)) as TextBlock;
            LiveRing = GetTemplateChild(nameof(LiveRing)) as SelfDestructTimer;

            Texture.Click += Button_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        private string _prevUrl;

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var location = message.Content as MessageLocation;
            if (location == null || !_templateApplied)
            {
                return;
            }

            var width = 320 * WindowContext.Current.RasterizationScale;
            var height = 200 * WindowContext.Current.RasterizationScale;

            var latitude = location.Location.Latitude.ToString(CultureInfo.InvariantCulture);
            var longitude = location.Location.Longitude.ToString(CultureInfo.InvariantCulture);

            var nextUrl = string.Format("https://dev.virtualearth.net/REST/v1/Imagery/Map/Road/{0},{1}/{2}?mapSize={3:F0},{4:F0}&key={5}",
                latitude, longitude, 15, width, height, Constants.BingMapsApiKey);

            if (nextUrl != _prevUrl)
            {
                Texture.Constraint = message;
                Texture.Source = new BitmapImage(new Uri(_prevUrl = nextUrl));
            }

            if (location.LivePeriod > 0)
            {
                PinPhoto.SetMessageSender(message.ClientService, message.SenderId, 32);

                if (location.IsExpired(message.Date))
                {
                    LivePanel.Visibility = Visibility.Collapsed;
                    LiveRing.Value = null;

                    PinDot.Visibility = Visibility.Collapsed;
                }
                else
                {
                    LivePanel.Visibility = Visibility.Visible;
                    PinDot.Visibility = Visibility.Collapsed;

                    Title.Text = Strings.AttachLiveLocation;
                    Subtitle.Text = Locale.FormatLocationUpdateDate(message.EditDate > 0 ? message.EditDate : message.Date);

                    LivePeriod.Text = Locale.FormatLivePeriod(location.LivePeriod, message.Date);
                    LiveRing.Maximum = location.LivePeriod;

                    if (location.LivePeriod == int.MaxValue)
                    {
                        LiveRing.Fill();
                    }
                    else
                    {
                        LiveRing.Value = Formatter.ToLocalTime(message.Date + location.LivePeriod);
                    }
                }
            }
            else
            {
                LivePanel.Visibility = Visibility.Collapsed;
                LiveRing.Value = null;

                PinDot.Visibility = Visibility.Visible;
                PinPhoto.Clear();
            }
        }

        public void Recycle()
        {
            _message = null;
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
