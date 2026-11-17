//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

// The shim BannedSymbols.WinUI.txt sends every other caller to.
#pragma warning disable RS0030

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Telegram.Storage
{
    public static class FileItemIO
    {
        public static Task<string> ReadTextAsync(FileItem file)
        {
            return FileIO.ReadTextAsync(file.Source).AsTask();
        }

        public static Task<IList<string>> ReadLinesAsync(FileItem file)
        {
            return FileIO.ReadLinesAsync(file.Source).AsTask();
        }

        public static Task<IBuffer> ReadBufferAsync(FileItem file)
        {
            return FileIO.ReadBufferAsync(file.Source).AsTask();
        }

        public static Task WriteTextAsync(FileItem file, string contents)
        {
            return FileIO.WriteTextAsync(file.Source, contents).AsTask();
        }

        public static Task WriteBufferAsync(FileItem file, IBuffer buffer)
        {
            return FileIO.WriteBufferAsync(file.Source, buffer).AsTask();
        }

        public static Task WriteBytesAsync(FileItem file, byte[] buffer)
        {
            return FileIO.WriteBytesAsync(file.Source, buffer).AsTask();
        }
    }
}
