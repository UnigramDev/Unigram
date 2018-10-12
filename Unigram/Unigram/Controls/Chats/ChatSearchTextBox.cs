using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Chats
{
    public class ChatSearchTextBox : TextBox
    {
        private ChatSearchState _state;
        public ChatSearchState State
        {
            get { return _state; }
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

        private User _from;
        public User From
        {
            get { return _from; }
            set
            {
                _from = value;
                Header = _from?.FirstName ?? string.Empty;
            }
        }
    }

    public enum ChatSearchState
    {
        Text,
        Members,
        TextByMember
    }
}
