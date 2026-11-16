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
    // Where a Windows.Storage object crosses into the app or out of it: pickers, drag-drop and
    // paste, activation, share, Launcher, recording. Extensions rather than statics on the types,
    // because on UWP FileItem is StorageFile and has no such statics.
    public static class StorageEdge
    {
        public static FileItem ToFileItem(this StorageFile file)
        {
            return file == null ? null : new FileItem(file);
        }

        public static FolderItem ToFolderItem(this StorageFolder folder)
        {
            return folder == null ? null : new FolderItem(folder);
        }

        // Null for an item that is neither a file nor a folder, which nothing has produced so far.
        public static IFileSystemItem ToFileSystemItem(this IStorageItem item)
        {
            return item switch
            {
                StorageFile file => new FileItem(file),
                StorageFolder folder => new FolderItem(folder),
                _ => null
            };
        }

        public static IReadOnlyList<FileItem> ToFileItems(this IReadOnlyList<StorageFile> files)
        {
            var items = new FileItem[files.Count];

            for (int i = 0; i < items.Length; i++)
            {
                items[i] = new FileItem(files[i]);
            }

            return items;
        }

        // Skips what ToFileSystemItem would turn into null, so the result never holds one.
        public static IReadOnlyList<IFileSystemItem> ToFileSystemItems(this IReadOnlyList<IStorageItem> items)
        {
            var result = new List<IFileSystemItem>(items.Count);

            foreach (var item in items)
            {
                if (item.ToFileSystemItem() is IFileSystemItem converted)
                {
                    result.Add(converted);
                }
            }

            return result;
        }

        public static Task<StorageFile> ToStorageFileAsync(this FileItem file)
        {
            return Task.FromResult(file?.Source);
        }

        public static Task<StorageFolder> ToStorageFolderAsync(this FolderItem folder)
        {
            return Task.FromResult(folder?.Source);
        }

        public static Task<IStorageItem> ToStorageItemAsync(this IFileSystemItem item)
        {
            return Task.FromResult<IStorageItem>(item switch
            {
                FileItem file => file.Source,
                FolderItem folder => folder.Source,
                _ => null
            });
        }

        // Call sites written against IAsyncOperation call AsTask() on what is a Task here.
        public static Task<T> AsTask<T>(this Task<T> task)
        {
            return task;
        }

        public static Task AsTask(this Task task)
        {
            return task;
        }
    }
}
