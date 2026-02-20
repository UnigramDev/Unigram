//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Td.Api;
using Windows.UI.Xaml;

namespace Telegram.Controls.Chats
{
    public partial class ChatSearchTextBox : SuggestTextBox
    {
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            UpdateState(_state);
        }

        private ChatSearchState _state;
        public ChatSearchState State
        {
            get => _state;
            set
            {
                _state = value;
                UpdateState(value);
            }
        }

        private void UpdateState(ChatSearchState value)
        {
            switch (value)
            {
                case ChatSearchState.Text:
                    VisualStateManager.GoToState(this, "Text", false);
                    break;
                case ChatSearchState.Members:
                    VisualStateManager.GoToState(this, "Members", false);
                    break;
                case ChatSearchState.TextByMember:
                    VisualStateManager.GoToState(this, "TextByMember", false);
                    break;
            }
        }

        private MessageSender _from;
        public MessageSender From
        {
            get => _from;
            set => _from = value;//Header = _from?.FirstName ?? string.Empty;
        }
    }

    public enum ChatSearchState
    {
        Text,

        Members,
        TextByMember,
    }
}
