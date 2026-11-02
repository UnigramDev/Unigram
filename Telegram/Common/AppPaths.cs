//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
#if UNPACKAGED
using Telegram.Common;
#endif

namespace Telegram
{
    /// <summary>
    /// Where the app lives: its payload, and the roots <see cref="ApplicationData.Current"/> hands
    /// out. Both throw without package identity, and the data roots are the app's whole idea of
    /// where its state is - the TDLib databases, the langpack, the themes, the crash reports and
    /// every cached file are under one of them. So this is the single place that answers where
    /// they are, and everything downstream keeps taking the same paths and the same
    /// <see cref="StorageFolder"/>s.
    ///
    /// Unpackaged the container layout is rebuilt verbatim under
    /// <c>%LOCALAPPDATA%\TelegramFZ-LLC.Windows</c>, LocalState, LocalCache and TempState included,
    /// so a profile can be copied either way by hand. Telegram.Native's AppModelPaths.h answers the
    /// same paths and has to agree.
    /// </summary>
    public static class AppPaths
    {
#if UNPACKAGED
        // First: every initializer below reads it, and they run in the order they are written.
        private static readonly bool _packaged = ApiInfo.HasPackageIdentity;

        /// <summary>
        /// Where the payload is. <c>AppContext.BaseDirectory</c> ends in a separator and
        /// <c>InstalledLocation.Path</c> does not, and the difference reaches string
        /// concatenations, so it is trimmed here rather than at every call site.
        /// </summary>
        public static string InstalledPath { get; } = _packaged
            ? Package.Current.InstalledLocation.Path
            : AppContext.BaseDirectory.TrimEnd('\\');

        public static string LocalPath { get; } = _packaged
            ? ApplicationData.Current.LocalFolder.Path
            : EnsureFolder("LocalState");

        public static string TemporaryPath { get; } = _packaged
            ? ApplicationData.Current.TemporaryFolder.Path
            : EnsureFolder("TempState");

        public static string LocalCachePath { get; } = _packaged
            ? ApplicationData.Current.LocalCacheFolder.Path
            : EnsureFolder("LocalCache");

        public static AppFolder LocalFolder { get; } = _packaged
            ? new AppFolder(ApplicationData.Current.LocalFolder)
            : new AppFolder(LocalPath);

        public static AppFolder TemporaryFolder { get; } = _packaged
            ? new AppFolder(ApplicationData.Current.TemporaryFolder)
            : new AppFolder(TemporaryPath);

        public static Task<StorageFolder> GetLocalFolderAsync() => LocalFolder.GetAsync();

        public static Task<StorageFolder> GetTemporaryFolderAsync() => TemporaryFolder.GetAsync();

        /// <summary>
        /// A URI naming a file under <see cref="LocalFolder"/>. <c>ms-appdata:</c> is resolved by
        /// the app model against the package's own folders, so unpackaged there is nothing to
        /// resolve it with and the path itself is the answer.
        /// </summary>
        public static Uri GetLocalUri(string relativePath)
        {
            return _packaged
                ? new Uri("ms-appdata:///local/" + relativePath.Replace('\\', '/'))
                : new Uri(Path.Combine(LocalPath, relativePath));
        }

        private static string _installedUri;

        /// <summary>
        /// A XAML font family in the form Win2D can load. Win2D opens an <c>ms-appx:</c> font with
        /// GetFileFromApplicationUriAsync, which fails E_INVALIDARG without package identity. The
        /// Windows App SDK build takes a <c>file:</c> URI to the same file instead.
        /// </summary>
        public static string GetCanvasFontFamily(string source)
        {
            if (_packaged || source == null || !source.Contains("ms-appx:///", StringComparison.OrdinalIgnoreCase))
            {
                return source;
            }

            _installedUri ??= new Uri(InstalledPath + "\\").AbsoluteUri;
            return source.Replace("ms-appx:///", _installedUri, StringComparison.OrdinalIgnoreCase);
        }

        // A debug build keeps a profile of its own, so that it can run beside the installed app:
        // two processes cannot share the TDLib databases. AppModelPaths.h switches on _DEBUG to the
        // same name, which holds as long as the natives are built in the app's configuration.
#if DEBUG
        private const string RootName = "TelegramFZ-LLC.Windows.Debug";
#else
        private const string RootName = "TelegramFZ-LLC.Windows";
#endif

        private static string EnsureFolder(string name)
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                RootName,
                name);

            Directory.CreateDirectory(path);
            return path;
        }
#else
        public static string InstalledPath => Package.Current.InstalledLocation.Path;

        public static string LocalPath => ApplicationData.Current.LocalFolder.Path;

        public static string TemporaryPath => ApplicationData.Current.TemporaryFolder.Path;

        public static string LocalCachePath => ApplicationData.Current.LocalCacheFolder.Path;

        public static string GetCanvasFontFamily(string source) => source;

        public static StorageFolder LocalFolder => ApplicationData.Current.LocalFolder;

        public static StorageFolder TemporaryFolder => ApplicationData.Current.TemporaryFolder;

        public static Task<StorageFolder> GetLocalFolderAsync() => Task.FromResult(ApplicationData.Current.LocalFolder);

        public static Task<StorageFolder> GetTemporaryFolderAsync() => Task.FromResult(ApplicationData.Current.TemporaryFolder);

        public static Uri GetLocalUri(string relativePath) => new Uri("ms-appdata:///local/" + relativePath);
#endif

        /// <summary>
        /// A path to something in the payload, as <c>InstalledLocation.GetFileAsync</c> takes one:
        /// relative, and with backslashes.
        /// </summary>
        public static string GetInstalledPath(string path)
        {
            return Path.Combine(InstalledPath, path);
        }
    }
#if UNPACKAGED

    /// <summary>
    /// A data root that may not be a <see cref="StorageFolder"/> yet. Unpackaged one can only be
    /// had from a path, which is an RPC, and the first caller can come before activation does -
    /// a TDLib update, say. So the folder is resolved on first use and every method waits for it.
    /// The names mirror StorageFolder's so that call sites read the same on both flavours; one
    /// that needs the folder itself, as an argument or for its properties, takes
    /// <see cref="GetAsync"/>.
    /// </summary>
    public sealed class AppFolder
    {
        private readonly string _path;
        private Task<StorageFolder> _folder;

        internal AppFolder(StorageFolder folder)
        {
            _folder = Task.FromResult(folder);
        }

        internal AppFolder(string path)
        {
            _path = path;
        }

        // Two callers racing here both resolve the same folder, and either result will do.
        public Task<StorageFolder> GetAsync()
        {
            return _folder ??= StorageFolder.GetFolderFromPathAsync(_path).AsTask();
        }

        public async Task<StorageFile> CreateFileAsync(string desiredName, CreationCollisionOption options)
        {
            var folder = await GetAsync().ConfigureAwait(false);
            return await folder.CreateFileAsync(desiredName, options);
        }

        public async Task<StorageFolder> CreateFolderAsync(string desiredName, CreationCollisionOption options)
        {
            var folder = await GetAsync().ConfigureAwait(false);
            return await folder.CreateFolderAsync(desiredName, options);
        }

        public async Task<StorageFile> GetFileAsync(string name)
        {
            var folder = await GetAsync().ConfigureAwait(false);
            return await folder.GetFileAsync(name);
        }

        public async Task<StorageFolder> GetFolderAsync(string name)
        {
            var folder = await GetAsync().ConfigureAwait(false);
            return await folder.GetFolderAsync(name);
        }

        public async Task<IStorageItem> TryGetItemAsync(string name)
        {
            var folder = await GetAsync().ConfigureAwait(false);
            return await folder.TryGetItemAsync(name);
        }
    }
#endif
}
