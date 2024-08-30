//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class ChatPill : HyperlinkButton
    {
        private ProfilePicture Photo;
        private TextBlock Title;

        private bool _templateApplied;

        private IClientService _clientService;
        private Chat _chat;
        private User _user;

        public ChatPill()
        {
            DefaultStyleKey = typeof(ChatPill);
        }

        public long ChatId { get; private set; }

        public long UserId { get; private set; }

        protected override void OnApplyTemplate()
        {
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
            Title = GetTemplateChild(nameof(Title)) as TextBlock;

            _templateApplied = true;

            if (_clientService != null && _chat != null)
            {
                SetChat(_clientService, _chat);
            }
            else if (_clientService != null && _user != null)
            {
                SetUser(_clientService, _user);
            }

            base.OnApplyTemplate();
        }

        public void SetChat(IClientService clientService, Chat chat)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _chat = chat;
                _user = null;

                return;
            }

            _clientService = null;
            _chat = null;
            _user = null;

            ChatId = chat.Id;
            UserId = 0;

            Photo.SetChat(clientService, chat, 28);
            Title.Text = clientService.GetTitle(chat);

            Background = clientService.GetAccentBrush(chat.AccentColorId);
        }

        public void SetUser(IClientService clientService, User user)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _user = user;
                _chat = null;

                return;
            }

            _clientService = null;
            _user = null;
            _chat = null;

            UserId = user.Id;
            ChatId = 0;

            Photo.SetUser(clientService, user, 28);
            Title.Text = user.FullName();

            Background = clientService.GetAccentBrush(user.AccentColorId);
        }
    }
}
