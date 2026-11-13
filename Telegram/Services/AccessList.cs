//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Windows.Storage;
using SAP = Windows.Storage.AccessCache.StorageApplicationPermissions;
#if WINUI3
using System.IO;
using Telegram.Common;
using Telegram.Services.Settings;
#else
using System.Collections.Generic;
#endif

namespace Telegram.Services
{
    /// <summary>
    /// The app's handles on files outside its own folders: the user's download folder, a file
    /// being uploaded, a file already saved. A token names one and outlives the process.
    ///
    /// Two implementations, because the thing a token stands for is not the same on both hosts.
    /// Inside the app container a path grants nothing, so the token is a grant held by the broker
    /// and the list is capped. WinUI runs full trust, packaged or not: the process holds the
    /// user's own token, so a path *is* the access, and the list is only a record of paths.
    /// </summary>
    public interface IAccessList
    {
        bool ContainsItem(string token);

        void AddOrReplace(string token, IStorageItem item);

        void Remove(string token);

        /// <summary>
        /// Throws when the file is gone, as the access list does: every caller either reports the
        /// failure back to TDLib or drops the token.
        /// </summary>
        Task<StorageFile> GetFileAsync(string token);

        Task<StorageFolder> GetFolderAsync(string token);

        /// <summary>
        /// Whether the item is somewhere the app may still reach after a restart.
        /// </summary>
        bool CheckAccess(IStorageItem item);
    }

    public static class AccessList
    {
#if WINUI3
        public static PathAccessList Future { get; } = new PathAccessList();
#else
        public static FutureAccessList Future { get; } = new FutureAccessList();
#endif
    }

#if !WINUI3
    /// <summary>
    /// The broker's list, one call deep. The try-catch every one of these needs stays with the
    /// callers, which is where it already is.
    /// </summary>
    public partial class FutureAccessList : IAccessList
    {
        public int Count => SAP.FutureAccessList.Entries.Count;

        // uint on the broker's list, and 1000 in practice.
        public int MaximumItemsAllowed => (int)SAP.FutureAccessList.MaximumItemsAllowed;

        public IReadOnlyList<string> Tokens
        {
            get
            {
                var entries = SAP.FutureAccessList.Entries;
                var tokens = new List<string>(entries.Count);

                foreach (var entry in entries)
                {
                    tokens.Add(entry.Token);
                }

                return tokens;
            }
        }

        public bool ContainsItem(string token) => SAP.FutureAccessList.ContainsItem(token);

        public void AddOrReplace(string token, IStorageItem item) => SAP.FutureAccessList.AddOrReplace(token, item);

        public void Remove(string token) => SAP.FutureAccessList.Remove(token);

        public void Clear() => SAP.FutureAccessList.Clear();

        public Task<StorageFile> GetFileAsync(string token) => SAP.FutureAccessList.GetFileAsync(token).AsTask();

        public Task<StorageFolder> GetFolderAsync(string token) => SAP.FutureAccessList.GetFolderAsync(token).AsTask();

        public bool CheckAccess(IStorageItem item) => SAP.FutureAccessList.CheckAccess(item);
    }
#endif

#if WINUI3
    /// <summary>
    /// Key to path, for what the app remembers across restarts: the download folder and each
    /// downloaded file's copy. Files being sent are not here - their token is the path itself, see
    /// <see cref="GenerationSources"/> - so the list grows only with downloads and needs no cap.
    ///
    /// Two older lists are read through and never written. Unpackaged builds put a token for every
    /// file sent in this same container, and packaged ones used the broker's list for everything;
    /// an upload TDLib resumes still names its file by one of those tokens.
    /// </summary>
    public partial class PathAccessList : IAccessList
    {
        private readonly ISettingsStore _store = SettingsStore.Local.GetContainer("AccessList");

        public bool ContainsItem(string token) => _store.ContainsKey(token) || ContainsLegacy(token);

        public void AddOrReplace(string token, IStorageItem item) => _store.SetValue(token, item.Path);

        public void Remove(string token)
        {
            _store.Remove(token);

            if (ContainsLegacy(token))
            {
                SAP.FutureAccessList.Remove(token);
            }
        }

        public async Task<StorageFile> GetFileAsync(string token)
        {
            if (_store.TryGet(token, out string path))
            {
                return await StorageFile.GetFileFromPathAsync(path);
            }
            else if (ContainsLegacy(token))
            {
                return await SAP.FutureAccessList.GetFileAsync(token);
            }

            throw new FileNotFoundException($"No file is held under {token}");
        }

        public async Task<StorageFolder> GetFolderAsync(string token)
        {
            if (_store.TryGet(token, out string path))
            {
                return await StorageFolder.GetFolderFromPathAsync(path);
            }
            else if (ContainsLegacy(token))
            {
                return await SAP.FutureAccessList.GetFolderAsync(token);
            }

            throw new DirectoryNotFoundException($"No folder is held under {token}");
        }

        private static bool ContainsLegacy(string token)
        {
            return ApiInfo.HasPackageIdentity && SAP.FutureAccessList.ContainsItem(token);
        }

        // There is no grant to check: the process holds the user's own token, so anything the user
        // can reach is reachable again after a restart.
        public bool CheckAccess(IStorageItem item) => true;
    }
#endif
}
