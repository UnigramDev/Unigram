//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class VenueContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public VenueContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(VenueContent);
        }

        #region InitializeComponent

        private HyperlinkButton Button;
        private ImageView Texture;
        private BitmapIcon VenueGlyph;
        private Path VenueDot;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Button = GetTemplateChild(nameof(Button)) as HyperlinkButton;
            Texture = GetTemplateChild(nameof(Texture)) as ImageView;
            VenueGlyph = GetTemplateChild(nameof(VenueGlyph)) as BitmapIcon;
            VenueDot = GetTemplateChild(nameof(VenueDot)) as Path;

            Button.Click += Button_Click;

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

            var venue = GetContent(message);
            if (venue == null || !_templateApplied)
            {
                return;
            }

            Texture.Constraint = message;
            Texture.XamlRoot = XamlRoot;
            Texture.SetSource(message.ClientService, venue.Location, 320, 200, message.ChatId);

            if (string.IsNullOrEmpty(venue.Type))
            {
                VenueDot.Visibility = Visibility.Visible;
                VenueGlyph.UriSource = null;
            }
            else
            {
                VenueDot.Visibility = Visibility.Collapsed;
                VenueGlyph.UriSource = new Uri(string.Format("https://ss3.4sqi.net/img/categories_v2/{0}_88.png", venue.Type));
            }
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content switch
            {
                MessageVenue => true,
                MessagePoll poll when poll.Media is PollMediaVenue && !primary => true,
                _ => false,
            };
        }

        private Venue GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            switch (content)
            {
                case MessageVenue venue:
                    return venue.Venue;
                case MessagePoll poll when poll.Media is PollMediaVenue pollVenue:
                    return pollVenue.Venue;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var venue = GetContent(_message);
            if (venue == null)
            {
                return;
            }

            if (_message.ClientService.TryGetUser(_message.SenderId, out User senderUser))
            {
                _message.Delegate.OpenLocation(venue.Location, senderUser.FullName());
            }
            else if (_message.ClientService.TryGetChat(_message.SenderId, out Chat senderChat))
            {
                _message.Delegate.OpenLocation(venue.Location, _message.ClientService.GetTitle(senderChat));
            }
        }
    }
}
