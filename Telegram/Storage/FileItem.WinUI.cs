//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

// The shim BannedSymbols.WinUI.txt sends every other caller to.
#pragma warning disable RS0030

using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace Telegram.Storage
{
    // Forwards to Windows.Storage until each member gets its Win32 implementation, so the rename
    // and the change in behaviour land separately.
    public sealed class FileItem : IFileSystemItem
    {
        internal readonly StorageFile Source;

        internal FileItem(StorageFile source)
        {
            Source = source;
        }

        public string Name => Source.Name;

        public string Path => Source.Path;

        public string DisplayName => Source.DisplayName;

        public string FileType => Source.FileType;

        public string ContentType => Source.ContentType;

        public DateTimeOffset DateCreated => Source.DateCreated;

        public FileAttributes Attributes => Source.Attributes;

        public bool IsAvailable => Source.IsAvailable;

        public string FolderRelativeId => Source.FolderRelativeId;

        public StorageItemContentProperties Properties => Source.Properties;

        public static async Task<FileItem> GetFileFromPathAsync(string path)
        {
            return new FileItem(await StorageFile.GetFileFromPathAsync(path));
        }

        public Task<IRandomAccessStreamWithContentType> OpenReadAsync()
        {
            return Source.OpenReadAsync().AsTask();
        }

        public Task<IRandomAccessStream> OpenAsync(FileAccessMode accessMode)
        {
            return Source.OpenAsync(accessMode).AsTask();
        }

#if CSWINRT3_0
        // CsWinRT 3 has no storage extensions. This is what theirs does: read-write at offset 0, not truncated.
        public async Task<System.IO.Stream> OpenStreamForWriteAsync()
        {
            var stream = await Source.OpenAsync(FileAccessMode.ReadWrite);
            return Windows.Storage.Streams.WindowsRuntimeStreamExtensions.AsStream(stream);
        }
#else
        public Task<System.IO.Stream> OpenStreamForWriteAsync()
        {
            return System.IO.WindowsRuntimeStorageExtensions.OpenStreamForWriteAsync(Source);
        }
#endif

        public async Task<FileItem> CopyAsync(FolderItem destinationFolder)
        {
            return new FileItem(await Source.CopyAsync(destinationFolder.Source));
        }

        public async Task<FileItem> CopyAsync(FolderItem destinationFolder, string desiredNewName)
        {
            return new FileItem(await Source.CopyAsync(destinationFolder.Source, desiredNewName));
        }

        public async Task<FileItem> CopyAsync(FolderItem destinationFolder, string desiredNewName, NameCollisionOption option)
        {
            return new FileItem(await Source.CopyAsync(destinationFolder.Source, desiredNewName, option));
        }

        public Task CopyAndReplaceAsync(FileItem fileToReplace)
        {
            return Source.CopyAndReplaceAsync(fileToReplace.Source).AsTask();
        }

        public Task MoveAndReplaceAsync(FileItem fileToReplace)
        {
            return Source.MoveAndReplaceAsync(fileToReplace.Source).AsTask();
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

        public async Task<FolderItem> GetParentAsync()
        {
            return (await Source.GetParentAsync()).ToFolderItem();
        }

        public Task<StorageItemThumbnail> GetThumbnailAsync(ThumbnailMode mode, uint requestedSize)
        {
            return Source.GetThumbnailAsync(mode, requestedSize).AsTask();
        }
    }
}
