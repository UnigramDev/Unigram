//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Delegates
{
    public interface IChatListDelegate : IViewModelDelegate
    {
        void SetSelectionMode(bool enabled);

        void SetSelectedItem(Chat chat);
        void SetSelectedItems(IList<Chat> chats);



        void Navigate(object item);

        void UpdateChatLastMessage(Chat chat);

        void UpdateChatFolders();

        void UpdateChatListArchive();

        Task UpdateLayoutAsync();
    }
}
