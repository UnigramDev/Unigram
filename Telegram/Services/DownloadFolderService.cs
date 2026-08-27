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

        public bool IsStaged(string uniqueId)
        {
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
            if (Extensions.IsRelativePath(ApplicationData.Current.LocalFolder.Path, file.Path, out _))
            {
                return false;
            }

            try
            {
                return SAP.FutureAccessList.CheckAccess(file);
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
                    return new DownloadFolder(true, await SAP.FutureAccessList.GetFolderAsync(FolderToken));
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
                else if (Extensions.IsRelativePath(ApplicationData.Current.LocalFolder.Path, folder.Path, out _))
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
                    return await KnownFolders.GetFolderAsync(KnownFolderId.DownloadsFolder);
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
                    var folder = await SAP.FutureAccessList.GetFolderAsync(FolderToken);
                    return await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
                }
                catch
                {
                    Remove(FolderToken);
                }
            }

            return await DownloadsFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
        }

        /// <summary>
        /// The folder used to be held in the most recently used list, which evicts on its own.
        /// </summary>
        private async Task MigrateAsync()
        {
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
                return SAP.FutureAccessList.ContainsItem(token);
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
                    return await SAP.FutureAccessList.GetFileAsync(token);
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
                SAP.FutureAccessList.AddOrReplace(token, item);
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
                    SAP.FutureAccessList.Remove(token);
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
                if (SAP.FutureAccessList.Entries.Count >= SAP.FutureAccessList.MaximumItemsAllowed - 10)
                {
                    for (int i = SAP.FutureAccessList.Entries.Count - 1; i >= 0; i--)
                    {
                        var entry = SAP.FutureAccessList.Entries[i];
                        if (entry.Token != FolderToken)
                        {
                            SAP.FutureAccessList.Remove(entry.Token);
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
                    SAP.FutureAccessList.Clear();
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
