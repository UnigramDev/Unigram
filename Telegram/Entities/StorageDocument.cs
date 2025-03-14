//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Storage;

namespace Telegram.Entities
{
    public partial class StorageDocument : StorageMedia
    {
        public StorageDocument(StorageFile file, ulong fileSize)
            : base(file, fileSize)
        {
        }

        public StorageDocument(StorageMedia original)
            : base(original.File, original.Size)
        {
            Original = original;
        }

        public StorageMedia Original { get; }
    }
}
