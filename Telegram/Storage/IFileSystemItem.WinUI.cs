//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace Telegram.Storage
{
    public interface IFileSystemItem
    {
        string Name { get; }

        string Path { get; }

        DateTimeOffset DateCreated { get; }

        FileAttributes Attributes { get; }

        Task<FileItemProperties> GetBasicPropertiesAsync();

        Task RenameAsync(string desiredName);

        Task RenameAsync(string desiredName, NameCollisionOption option);

        Task DeleteAsync();

        Task DeleteAsync(StorageDeleteOption option);
    }
}
