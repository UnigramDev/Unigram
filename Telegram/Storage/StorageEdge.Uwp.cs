//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;

namespace Telegram.Storage
{
    // Every conversion is the identity: on UWP the app's types are the WinRT ones.
    public static class StorageEdge
    {
        public static FileItem ToFileItem(this StorageFile file)
        {
            return file;
        }

        public static FolderItem ToFolderItem(this StorageFolder folder)
        {
            return folder;
        }

        public static IFileSystemItem ToFileSystemItem(this IStorageItem item)
        {
            return item;
        }

        public static IReadOnlyList<FileItem> ToFileItems(this IReadOnlyList<StorageFile> files)
        {
            return files;
        }

        public static IReadOnlyList<IFileSystemItem> ToFileSystemItems(this IReadOnlyList<IStorageItem> items)
        {
            return items;
        }

        public static Task<StorageFile> ToStorageFileAsync(this FileItem file)
        {
            return Task.FromResult(file);
        }

        public static Task<StorageFolder> ToStorageFolderAsync(this FolderItem folder)
        {
            return Task.FromResult(folder);
        }

        public static Task<IStorageItem> ToStorageItemAsync(this IFileSystemItem item)
        {
            return Task.FromResult(item);
        }
    }
}
