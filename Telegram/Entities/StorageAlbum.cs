//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;

namespace Telegram.Entities
{
    public enum StorageAlbumType
    {
        None,
        Media,
        Audio,
        Documents,
        NotSupported
    }

    /// <summary>
    /// One outgoing message carrying several media. This is a grouping for sending and nothing
    /// else — how it is drawn is the caller's business, and a popup showing it as a mosaic uses a
    /// row of its own rather than this.
    /// </summary>
    public partial class StorageAlbum : StorageMedia
    {
        public IList<StorageMedia> Media { get; }

        public StorageAlbum(StorageAlbumType type, IList<StorageMedia> media)
            : base(null, 0)
        {
            Type = type;
            Media = media.ToList();
        }

        /// <summary>
        /// What the grouping decided this album is. Only <see cref="StorageAlbumType.Media"/> and
        /// <see cref="StorageAlbumType.NotSupported"/> have a mosaic to draw; documents and audio
        /// are grouped for sending but shown as plain rows.
        /// </summary>
        public StorageAlbumType Type { get; }

        /// <summary>
        /// Telegram groups at most ten media into one message.
        /// </summary>
        public const int MAX_ITEMS = 10;
    }
}
