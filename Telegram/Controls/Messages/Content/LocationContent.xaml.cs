//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class LocationContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public LocationContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(LocationContent);
        }

        #region InitializeComponent

        private HyperlinkButton Button;
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
            Button = GetTemplateChild(nameof(Button)) as HyperlinkButton;
            Texture = GetTemplateChild(nameof(Texture)) as ImageView;
            PinPhoto = GetTemplateChild(nameof(PinPhoto)) as ProfilePicture;
            PinDot = GetTemplateChild(nameof(PinDot)) as Path;
            LivePanel = GetTemplateChild(nameof(LivePanel)) as Grid;
            Title = GetTemplateChild(nameof(Title)) as Run;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as Run;
            LivePeriod = GetTemplateChild(nameof(LivePeriod)) as TextBlock;
            LiveRing = GetTemplateChild(nameof(LiveRing)) as SelfDestructTimer;

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

            var location = GetContent(message);
            if (location == null || !_templateApplied)
            {
                return;
            }

            Texture.Constraint = message;
            Texture.XamlRoot = XamlRoot;
            Texture.SetSource(message.ClientService, location, 320, 200, message.ChatId);

            LivePanel.Visibility = Visibility.Collapsed;
            LiveRing.Value = null;

            PinDot.Visibility = Visibility.Visible;
            PinPhoto.Source = null;
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content switch
            {
                MessageLocation => true,
                MessagePoll poll when poll.Media is PollMediaLocation && !primary => true,
                _ => false,
            };
        }

        private Location GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            var content = message.Content;
            switch (content)
            {
                case MessageLocation location:
                    return location.Location;
                case MessagePoll poll when poll.Media is PollMediaLocation pollLocation:
                    return pollLocation.Location;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var location = GetContent(_message);
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
                    _message.Delegate.OpenLocation(location, senderUser.FullName());
                }
                else if (_message.ClientService.TryGetChat(_message.SenderId, out Chat senderChat))
                {
                    _message.Delegate.OpenLocation(location, _message.ClientService.GetTitle(senderChat));
                }
                else
                {
                    _message.Delegate.OpenLocation(location, null);
                }
            }
        }
    }
}
