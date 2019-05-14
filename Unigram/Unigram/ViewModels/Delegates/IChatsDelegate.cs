using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls;

namespace Unigram.ViewModels.Delegates
{
    public interface IChatsDelegate : IViewModelDelegate
    {
        void ShowChatsUndo(IList<Chat> chats, UndoType type, Action<IList<Chat>> action, Action<IList<Chat>> undo);

        void SetSelectionMode(bool enabled);

        void SetSelectedItem(Chat chat);
        void SetSelectedItems(IList<Chat> chats);
    }
}
