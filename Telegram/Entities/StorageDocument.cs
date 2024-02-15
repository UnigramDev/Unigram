//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Storage;

namespace Telegram.Entities
{
    public class StorageDocument : StorageMedia
    {
        public StorageDocument(StorageFile file, ulong fileSize)
            : base(file, fileSize)
        {
        }
    }
}
