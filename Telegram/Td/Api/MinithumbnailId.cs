//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;

namespace Telegram.Td.Api
{
    public partial class MinithumbnailId
    {
        public MinithumbnailId(int id, Minithumbnail minithumbnail, bool videoNote)
        {
            Id = id;
            Data = minithumbnail.Data;
            Height = minithumbnail.Height;
            Width = minithumbnail.Width;
            IsVideoNote = videoNote;
        }

        public int Id { get; set; }

        /// <summary>
        /// The thumbnail in JPEG format.
        /// </summary>
        public IList<byte> Data { get; set; }

        /// <summary>
        /// Thumbnail height, usually doesn't exceed 40.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Thumbnail width, usually doesn't exceed 40.
        /// </summary>
        public int Width { get; set; }

        public bool IsVideoNote { get; set; }
    }
}
