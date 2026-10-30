//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Common;
using Windows.Storage;
using SAP = Windows.Storage.AccessCache.StorageApplicationPermissions;
#if UNPACKAGED
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
#endif

namespace Telegram.Services
{
    /// <summary>
    /// Where downloaded files are kept, and which file went where.
    ///
    /// Two things live behind this rather than one: the destination folder the user chose, and the
    /// record of each downloaded file's copy in it. Both are expressed in download terms - staged,
    /// saved, discarded - because the mechanism underneath is not shared. UWP has no path it may
    /// keep, so it holds tokens in the future access list; an unpackaged host has nothing to hold
    /// but paths, and no notion of a grant to lose.
    ///
    /// App-wide, not per-session: the access list belongs to the package, and unique ids are
    /// global. Deliberately knows nothing about TDLib, so that the client service can depend on it
    /// without the storage service - which already depends on the client service - forming a cycle.
    /// </summary>
    public interface IDownloadFolderService
    {
        /// <summary>
        /// False where the platform has no download folder at all and everything stays in the
        /// cache. Every other member is then a no-op returning null or false.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Whether the user picked a folder of their own that the app still holds a grant for.
        /// </summary>
        bool HasCustomFolder { get; }

        Task<DownloadFolder> GetFolderAsync();

        Task<DownloadFolder> SetFolderAsync(StorageFolder folder);

        Task<StorageFolder> GetDefaultFolderAsync();

        /// <summary>
        /// Creates a file in the download folder without recording it, for a copy that is made
        /// in one go rather than staged and filled in later.
        /// </summary>
        Task<StorageFile> CreateAsync(string fileName);

        /// <summary>
        /// Reserves a placeholder in the download folder for a file that is about to download, so
        /// that the folder is known to be writable before the bytes arrive rather than after.
        /// </summary>
        Task<StorageFile> StageAsync(string uniqueId, string fileName);

        Task<StorageFile> GetStagedAsync(string uniqueId);

        /// <summary>
        /// Gives a file created by <see cref="CreateAsync"/> or <see cref="StageAsync"/> the bytes
        /// of the downloaded <paramref name="source"/>, replacing whatever it held.
        /// </summary>
        Task FillAsync(StorageFile source, StorageFile destination);

        /// <summary>
        /// Marks a file the app wrote outside its own folders as coming from the internet, so
        /// that SmartScreen and Protected View treat it as a browser download.
        /// </summary>
        void MarkFromInternet(StorageFile file);

        bool IsStaged(string uniqueId);

        /// <summary>
        /// Drops the placeholder and deletes it, for a download that was cancelled or failed.
        /// </summary>
        Task DiscardAsync(string uniqueId);

        /// <summary>
        /// Promotes a placeholder to the saved copy, once its bytes are in place.
        /// </summary>
        void Commit(string uniqueId, StorageFile file);

        Task<StorageFile> GetSavedAsync(string uniqueId);

        Task<bool> ContainsAsync(string uniqueId);

        void Forget(string uniqueId);

        /// <summary>
        /// Whether the file is already somewhere durable that the app is allowed to keep, and so
        /// can be remembered where it is instead of being copied into the download folder.
        /// </summary>
        bool CanRemember(StorageFile file);
    }

    public partial class DownloadFolderService : IDownloadFolderService
    {
        // The token the user's chosen folder is held under. Anything else in the list is a file.
        private const string FolderToken = "FilesDirectory";

#if UNPACKAGED
        // Not the localized app name: a translation would move the folder.
        private const string UnpackagedFolderName = "Telegram";
#endif

        public bool IsSupported => ApiInfo.HasDownloadFolder;

        public bool HasCustomFolder => Contains(FolderToken);

        #region Files

        public async Task<StorageFile> StageAsync(string uniqueId, string fileName)
        {
            var file = await CreateAsync(fileName);
            if (file != null)
            {
                AddOrReplace(Staged(uniqueId), file);
            }

            return file;
        }

        public Task<StorageFile> GetStagedAsync(string uniqueId)
        {
            return GetFileAsync(Staged(uniqueId));
        }

        public async Task FillAsync(StorageFile source, StorageFile destination)
        {
#if UNPACKAGED
            // Outside the app container both are plain paths, and the cache and the download
            // folder are usually on one volume: a second name for the cache's file costs neither
            // time nor space, and outlives TDLib deleting its own. Elsewhere it is a copy.
            if (!TryLink(source.Path, destination.Path))
            {
                await Task.Run(() => File.Copy(source.Path, destination.Path, true));
            }

            MarkFromInternet(destination);
#else
            await source.CopyAndReplaceAsync(destination);
#endif
        }

        public void MarkFromInternet(StorageFile file)
        {
#if UNPACKAGED
            // The broker tags what a packaged app writes to Downloads; nothing does it for us.
            try
            {
                using var stream = new FileStream(file.Path + ":Zone.Identifier", FileMode.Create, FileAccess.Write);

                // Writing a stream moves the file's modification time, which TDLib reads as the
                // file having been modified and forgets it - and a linked download shares its time
                // with the cache. -1 keeps this handle's writes from moving it.
                SetFileTime(stream.SafeFileHandle, IntPtr.Zero, IntPtr.Zero, -1L);

                stream.Write("[ZoneTransfer]\r\nZoneId=3\r\n"u8);
            }
            catch
            {
                // FAT volumes and some shares have no alternate streams
            }
#endif
        }

#if UNPACKAGED
        private static bool TryLink(string source, string destination)
        {
            // A link cannot be made over an existing name, so it is made beside the file it
            // replaces and moved over it, which swaps the two in one step.
            var link = Path.Combine(Path.GetDirectoryName(destination), $"Unconfirmed {Guid.NewGuid():N}.tdownload");

            if (!CreateHardLinkW(link, source, IntPtr.Zero))
            {
                return false;
            }

            try
            {
                File.Move(link, destination, true);
                return true;
            }
            catch
            {
                try
                {
                    File.Delete(link);
                }
                catch
                {
                    // All the file system calls must be wrapped in a try-catch block
                }

                return false;
            }
        }

        // Fails across volumes, and on file systems without links such as FAT and exFAT.
        [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetFileTime(SafeFileHandle hFile, IntPtr lpCreationTime, IntPtr lpLastAccessTime, in long lpLastWriteTime);
#endif

        public bool IsStaged(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId))
            {
                return false;
            }

            return Contains(Staged(uniqueId));
        }

        public async Task DiscardAsync(string uniqueId)
        {
            try
            {
                var file = await GetStagedAsync(uniqueId);

                Remove(Staged(uniqueId));

                if (file != null)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public void Commit(string uniqueId, StorageFile file)
        {
            Remove(Staged(uniqueId));
            AddOrReplace(uniqueId, file);
        }

        public Task<StorageFile> GetSavedAsync(string uniqueId)
        {
            return GetFileAsync(uniqueId);
        }

        public async Task<bool> ContainsAsync(string uniqueId)
        {
            return await GetFileAsync(uniqueId) != null;
        }

        public void Forget(string uniqueId)
        {
            Remove(uniqueId);
            Remove(Staged(uniqueId));
        }

        public bool CanRemember(StorageFile file)
        {
            // A file in our own local folder is TDLib's copy, which it is free to evict.
            if (Extensions.IsRelativePath(AppPaths.LocalPath, file.Path, out _))
            {
                return false;
            }

            try
            {
                return AccessList.Future.CheckAccess(file);
            }
            catch
            {
                return false;
            }
        }

        // One list holds both, so the staged placeholder and the saved copy of the same file need
        // different keys.
        private static string Staged(string uniqueId)
        {
            return uniqueId + "temp";
        }

        #endregion

        #region Folder

        public async Task<DownloadFolder> GetFolderAsync()
        {
            if (!IsSupported)
            {
                return null;
            }

            await MigrateAsync();

            if (Contains(FolderToken))
            {
                try
                {
                    return new DownloadFolder(true, await AccessList.Future.GetFolderAsync(FolderToken));
                }
                catch
                {
                    Remove(FolderToken);
                }
            }

            if (ApiInfo.HasKnownFolders)
            {
                var folder = await GetDefaultFolderAsync();
                if (folder != null)
                {
                    return new DownloadFolder(false, folder);
                }
            }

            return new DownloadFolder(false, Strings.DownloadFolderDefault);
        }

        public async Task<DownloadFolder> SetFolderAsync(StorageFolder folder)
        {
            if (folder == null)
            {
                Remove(FolderToken);
                return await GetFolderAsync();
            }

            if (ApiInfo.HasKnownFolders)
            {
                // The default folder and our own are both reachable without a grant, so holding
                // one would only take a slot and go stale.
                var downloads = await GetDefaultFolderAsync();

                if (downloads != null && Extensions.IsRelativePath(downloads.Path, folder.Path, out _))
                {
                    Remove(FolderToken);
                }
                else if (Extensions.IsRelativePath(AppPaths.LocalPath, folder.Path, out _))
                {
                    Remove(FolderToken);
                }
                else
                {
                    AddOrReplace(FolderToken, folder);
                }
            }
            else
            {
                AddOrReplace(FolderToken, folder);
            }

            return await GetFolderAsync();
        }

        public async Task<StorageFolder> GetDefaultFolderAsync()
        {
            if (ApiInfo.HasKnownFolders)
            {
                try
                {
                    var downloads = await KnownFolders.GetFolderAsync(KnownFolderId.DownloadsFolder);
#if UNPACKAGED
                    // A packaged app's downloads go to a folder of its own that Windows keeps
                    // under Downloads; without package identity there is none, so this is it.
                    if (!ApiInfo.HasPackageIdentity)
                    {
                        return await downloads.CreateFolderAsync(UnpackagedFolderName, CreationCollisionOption.OpenIfExists);
                    }
#endif
                    return downloads;
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }

            return null;
        }

        public async Task<StorageFile> CreateAsync(string fileName)
        {
            if (!IsSupported)
            {
                return null;
            }

            await MigrateAsync();

            if (Contains(FolderToken))
            {
                try
                {
                    var folder = await AccessList.Future.GetFolderAsync(FolderToken);
                    return await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
                }
                catch
                {
                    Remove(FolderToken);
                }
            }

#if UNPACKAGED
            // DownloadsFolder writes to Downloads itself without package identity.
            if (!ApiInfo.HasPackageIdentity)
            {
                var downloads = await GetDefaultFolderAsync();
                return downloads == null
                    ? null
                    : await downloads.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
            }
#endif

            return await DownloadsFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
        }

        /// <summary>
        /// The folder used to be held in the most recently used list, which evicts on its own.
        /// </summary>
        private async Task MigrateAsync()
        {
            // There is no list to migrate out of where there was never one to write to: the
            // unpackaged host has only ever kept paths.
            if (!ApiInfo.HasPackageIdentity)
            {
                return;
            }

            try
            {
                if (SAP.MostRecentlyUsedList.ContainsItem(FolderToken))
                {
                    try
                    {
                        var folder = await SAP.MostRecentlyUsedList.GetFolderAsync(FolderToken);
                        AddOrReplace(FolderToken, folder);
                    }
                    catch
                    {
                        // The app still remembers about the custom folder
                        // but we have no longer access to it (deleted, or whatever)
                    }

                    SAP.MostRecentlyUsedList.Remove(FolderToken);
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        #endregion

        #region Access list

        // The access list is not kept in sync with the file system - a saved file the user then
        // deleted or moved is still an entry that resolves to nothing - so every read has to try
        // the file itself rather than trust ContainsItem.

        private static bool Contains(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            try
            {
                return AccessList.Future.ContainsItem(token);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<StorageFile> GetFileAsync(string token)
        {
            try
            {
                if (Contains(token))
                {
                    return await AccessList.Future.GetFileAsync(token);
                }

                return null;
            }
            catch
            {
                Remove(token);
                return null;
            }
        }

        private static void AddOrReplace(string token, IStorageItem item)
        {
            RemoveOverflow();

            try
            {
                AccessList.Future.AddOrReplace(token, item);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        private static void Remove(string token)
        {
            try
            {
                if (Contains(token))
                {
                    AccessList.Future.Remove(token);
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        /// <summary>
        /// Internal because the outgoing-file tokens in <see cref="StorageService.Future"/> are
        /// held in this same list and against this same cap - which is why either can evict the
        /// other, and why they want separate stores.
        /// </summary>
        internal static void RemoveOverflow()
        {
            try
            {
                // Access to entries should probably be locked around the app...
                if (AccessList.Future.Count >= AccessList.Future.MaximumItemsAllowed - 10)
                {
                    var tokens = AccessList.Future.Tokens;

                    for (int i = tokens.Count - 1; i >= 0; i--)
                    {
                        if (tokens[i] != FolderToken)
                        {
                            AccessList.Future.Remove(tokens[i]);
                            break;
                        }
                    }
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block

                // TODO: I'm not sure how often it happens, but in some conditions trying to access
                // the Entities list, throws a FileNotFoundException.
                // When this happens, and the MaximumItemsAllowed count is reached, the app
                // won't be able to upload nor download any new file.
                // The only solution that I can think of, is to just clear the list when this happens
                // Hoping that this situation is isolated enough not to be an actual problem.

                // Can throw:
                // - ArgumentException
                // - FileNotFoundException

                try
                {
                    AccessList.Future.Clear();
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        #endregion
    }
}
