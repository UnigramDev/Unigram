//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Common
{
    public static class VideoPreloader
    {
        // What Telegram Desktop preloads of a video the server names no prefix for.
        private const long PrefixSize = 4 * 1024 * 1024;

        public static void Load(IClientService clientService, File file)
        {
            if (file.Local.DownloadedSize > 0)
            {
                return;
            }

            clientService.DownloadFile(file.Id, 1, 0, file.Size > 0 ? Math.Min(PrefixSize, file.Size) : PrefixSize);
        }
    }
}
