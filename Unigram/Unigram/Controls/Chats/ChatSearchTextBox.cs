//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.ViewModels.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Chats
{
    public class ChatSearchTextBox : TextBox
    {
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
                case ChatSearchState.Media:
                case ChatSearchState.Members:
                    VisualStateManager.GoToState(this, "Members", false);
                    break;
                case ChatSearchState.TextByMember:
                case ChatSearchState.TextByMedia:
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

        private ChatSearchMediaFilter _filter;
        public ChatSearchMediaFilter Filter
        {
            get => _filter;
            set => _filter = value;//Header = _filter?.Text ?? string.Empty;
        }
    }

    public enum ChatSearchState
    {
        Text,

        Members,
        TextByMember,

        Media,
        TextByMedia
    }
}
