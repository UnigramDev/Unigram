//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services.Settings;
using Windows.Storage;
using SAP = Windows.Storage.AccessCache.StorageApplicationPermissions;

namespace Telegram.Services
{
    /// <summary>
    /// The app's handles on files outside its own folders: the user's download folder, a file
    /// being uploaded, a file already saved. A token names one and outlives the process.
    ///
    /// Two implementations, because the thing a token stands for is not the same on both hosts.
    /// Inside the app container a path grants nothing, so the token is a grant held by the broker
    /// and the list is capped. Unpackaged the process holds the user's own token, so a path *is*
    /// the access - which makes the list a dictionary, and the cap only a way of keeping it from
    /// growing without end.
    /// </summary>
    public interface IAccessList
    {
        int Count { get; }

        int MaximumItemsAllowed { get; }

        IReadOnlyList<string> Tokens { get; }

        bool ContainsItem(string token);

        string Add(IStorageItem item);

        void AddOrReplace(string token, IStorageItem item);

        void Remove(string token);

        void Clear();

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
#if UNPACKAGED
        private static IAccessList _future;
        public static IAccessList Future => _future ??= ApiInfo.HasPackageIdentity
            ? new FutureAccessList()
            : new PathAccessList();
#else
        public static IAccessList Future { get; } = new FutureAccessList();
#endif
    }

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

        public string Add(IStorageItem item) => SAP.FutureAccessList.Add(item);

        public void AddOrReplace(string token, IStorageItem item) => SAP.FutureAccessList.AddOrReplace(token, item);

        public void Remove(string token) => SAP.FutureAccessList.Remove(token);

        public void Clear() => SAP.FutureAccessList.Clear();

        public Task<StorageFile> GetFileAsync(string token) => SAP.FutureAccessList.GetFileAsync(token).AsTask();

        public Task<StorageFolder> GetFolderAsync(string token) => SAP.FutureAccessList.GetFolderAsync(token).AsTask();

        public bool CheckAccess(IStorageItem item) => SAP.FutureAccessList.CheckAccess(item);
    }

#if UNPACKAGED
    /// <summary>
    /// Token to path, kept in the settings store so that it persists the same way everything else
    /// unpackaged does, and so that this needs no file format of its own.
    ///
    /// The cap is carried over from the broker's list rather than inherited from anything real:
    /// nothing here is ever released - see <see cref="GenerationSources"/> for why - so without
    /// one the map is a leak that survives restarts.
    /// </summary>
    public partial class PathAccessList : IAccessList
    {
        private const string ContainerName = "AccessList";

        private readonly ISettingsStore _store = SettingsStore.Local.GetContainer(ContainerName);

        // Mirrored rather than read back: the store is the only copy that survives a restart, but
        // the counting RemoveOverflow does on every Add wants an answer that costs nothing.
        private readonly Dictionary<string, string> _paths = new();

        public PathAccessList()
        {
            foreach (var token in _store.Keys)
            {
                if (_store.TryGet(token, out string path))
                {
                    _paths[token] = path;
                }
            }
        }

        public int Count => _paths.Count;

        public int MaximumItemsAllowed => 1000;

        public IReadOnlyList<string> Tokens => new List<string>(_paths.Keys);

        public bool ContainsItem(string token) => token != null && _paths.ContainsKey(token);

        public string Add(IStorageItem item)
        {
            var token = Guid.NewGuid().ToString("N");
            AddOrReplace(token, item);
            return token;
        }

        public void AddOrReplace(string token, IStorageItem item)
        {
            _paths[token] = item.Path;
            _store.SetValue(token, item.Path);
        }

        public void Remove(string token)
        {
            _paths.Remove(token);
            _store.Remove(token);
        }

        public void Clear()
        {
            _paths.Clear();
            _store.Clear();
        }

        public async Task<StorageFile> GetFileAsync(string token)
        {
            if (_paths.TryGetValue(token, out string path))
            {
                return await StorageFile.GetFileFromPathAsync(path);
            }

            throw new FileNotFoundException($"No file is held under {token}");
        }

        public async Task<StorageFolder> GetFolderAsync(string token)
        {
            if (_paths.TryGetValue(token, out string path))
            {
                return await StorageFolder.GetFolderFromPathAsync(path);
            }

            throw new DirectoryNotFoundException($"No folder is held under {token}");
        }

        // There is no grant to check: the process holds the user's own token, so anything the user
        // can reach is reachable again after a restart.
        public bool CheckAccess(IStorageItem item) => true;
    }
#endif
}
