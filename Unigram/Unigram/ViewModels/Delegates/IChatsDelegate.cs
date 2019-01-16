using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Unigram.ViewModels.Delegates
{
    public interface IChatsDelegate : IViewModelDelegate
    {
        void DeleteChat(Chat chat, bool clear, Action<Chat> action, Action<Chat> undo);
    }
}
