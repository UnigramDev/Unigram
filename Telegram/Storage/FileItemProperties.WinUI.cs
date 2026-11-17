//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

// The shim BannedSymbols.WinUI.txt sends every other caller to.
#pragma warning disable RS0030

using System;
using Windows.Storage.FileProperties;

namespace Telegram.Storage
{
    public sealed class FileItemProperties
    {
        internal FileItemProperties(BasicProperties properties)
        {
            Size = properties.Size;
            DateModified = properties.DateModified;
            ItemDate = properties.ItemDate;
        }

        public ulong Size { get; }

        public DateTimeOffset DateModified { get; }

        public DateTimeOffset ItemDate { get; }
    }
}
