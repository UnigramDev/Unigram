//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.ViewModels;

namespace Telegram.Td.Api
{
    public struct MessageId
    {
        public MessageId(MessageWithOwner message)
        {
            ChatId = message.ChatId;
            Id = message.Id;
            MediaAlbumId = message.MediaAlbumId;
        }

        public MessageId(Message message)
        {
            ChatId = message.ChatId;
            Id = message.Id;
            MediaAlbumId = message.MediaAlbumId;
        }

        public long ChatId;

        public long Id;

        public long MediaAlbumId;
    }
}
