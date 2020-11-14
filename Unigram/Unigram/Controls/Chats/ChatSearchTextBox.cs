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
            get { return _from; }
            set
            {
                _from = value;
                //Header = _from?.FirstName ?? string.Empty;
            }
        }

        private ChatSearchMediaFilter _filter;
        public ChatSearchMediaFilter Filter
        {
            get { return _filter; }
            set
            {
                _filter = value;
                //Header = _filter?.Text ?? string.Empty;
            }
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
