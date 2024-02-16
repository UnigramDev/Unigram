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

        public ChatPill()
        {
            DefaultStyleKey = typeof(ChatPill);
        }

        public long ChatId { get; private set; }

        protected override void OnApplyTemplate()
        {
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
            Title = GetTemplateChild(nameof(Title)) as TextBlock;

            _templateApplied = true;

            if (_clientService != null && _chat != null)
            {
                SetChat(_clientService, _chat);
            }

            base.OnApplyTemplate();
        }

        public void SetChat(IClientService clientService, Chat chat)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _chat = chat;

                return;
            }

            _clientService = null;
            _chat = null;

            ChatId = chat.Id;

            Photo.SetChat(clientService, chat, 28);
            Title.Text = clientService.GetTitle(chat);

            Background = clientService.GetAccentBrush(chat.AccentColorId);
        }
    }
}
