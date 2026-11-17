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

namespace Telegram.Storage
{
    // Forwards to Windows.Storage until each member gets its Win32 implementation, so the rename
    // and the change in behaviour land separately.
    public sealed class FolderItem : IFileSystemItem
    {
        internal readonly StorageFolder Source;

        internal FolderItem(StorageFolder source)
        {
            Source = source;
        }

        public string Name => Source.Name;

        public string Path => Source.Path;

        public string DisplayName => Source.DisplayName;

        public DateTimeOffset DateCreated => Source.DateCreated;

        public FileAttributes Attributes => Source.Attributes;

        public static async Task<FolderItem> GetFolderFromPathAsync(string path)
        {
            return new FolderItem(await StorageFolder.GetFolderFromPathAsync(path));
        }

        public async Task<FileItem> GetFileAsync(string name)
        {
            return new FileItem(await Source.GetFileAsync(name));
        }

        public async Task<FolderItem> GetFolderAsync(string name)
        {
            return new FolderItem(await Source.GetFolderAsync(name));
        }

        public async Task<IFileSystemItem> TryGetItemAsync(string name)
        {
            return (await Source.TryGetItemAsync(name)).ToFileSystemItem();
        }

        public async Task<IReadOnlyList<FileItem>> GetFilesAsync()
        {
            return (await Source.GetFilesAsync()).ToFileItems();
        }

        public async Task<FileItem> CreateFileAsync(string desiredName)
        {
            return new FileItem(await Source.CreateFileAsync(desiredName));
        }

        public async Task<FileItem> CreateFileAsync(string desiredName, CreationCollisionOption options)
        {
            return new FileItem(await Source.CreateFileAsync(desiredName, options));
        }

        public async Task<FolderItem> CreateFolderAsync(string desiredName)
        {
            return new FolderItem(await Source.CreateFolderAsync(desiredName));
        }

        public async Task<FolderItem> CreateFolderAsync(string desiredName, CreationCollisionOption options)
        {
            return new FolderItem(await Source.CreateFolderAsync(desiredName, options));
        }

        public Task RenameAsync(string desiredName)
        {
            return Source.RenameAsync(desiredName).AsTask();
        }

        public Task RenameAsync(string desiredName, NameCollisionOption option)
        {
            return Source.RenameAsync(desiredName, option).AsTask();
        }

        public Task DeleteAsync()
        {
            return Source.DeleteAsync().AsTask();
        }

        public Task DeleteAsync(StorageDeleteOption option)
        {
            return Source.DeleteAsync(option).AsTask();
        }

        public async Task<FileItemProperties> GetBasicPropertiesAsync()
        {
            return new FileItemProperties(await Source.GetBasicPropertiesAsync());
        }
    }
}
