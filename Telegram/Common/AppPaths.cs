//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using Windows.ApplicationModel;
using Windows.Storage;
#if UNPACKAGED
using System.Threading.Tasks;
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

        private static StorageFolder _localFolder;
        private static StorageFolder _temporaryFolder;

        public static StorageFolder LocalFolder => _packaged
            ? ApplicationData.Current.LocalFolder
            : _localFolder ?? throw new InvalidOperationException("AppPaths.InitializeAsync has not completed");

        public static StorageFolder TemporaryFolder => _packaged
            ? ApplicationData.Current.TemporaryFolder
            : _temporaryFolder ?? throw new InvalidOperationException("AppPaths.InitializeAsync has not completed");

        /// <summary>
        /// Unpackaged, a <see cref="StorageFolder"/> can only be had from a path, and that is an
        /// RPC. The host awaits this before it activates the app, so that the two properties above
        /// stay plain reads rather than blocking on one.
        /// </summary>
        public static async Task InitializeAsync()
        {
            if (_packaged)
            {
                return;
            }

            var local = StorageFolder.GetFolderFromPathAsync(LocalPath).AsTask();
            var temporary = StorageFolder.GetFolderFromPathAsync(TemporaryPath).AsTask();

            _localFolder = await local;
            _temporaryFolder = await temporary;
        }

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

        private static string EnsureFolder(string name)
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TelegramFZ-LLC.Windows",
                name);

            Directory.CreateDirectory(path);
            return path;
        }
#else
        public static string InstalledPath => Package.Current.InstalledLocation.Path;

        public static string LocalPath => ApplicationData.Current.LocalFolder.Path;

        public static string TemporaryPath => ApplicationData.Current.TemporaryFolder.Path;

        public static string LocalCachePath => ApplicationData.Current.LocalCacheFolder.Path;

        public static StorageFolder LocalFolder => ApplicationData.Current.LocalFolder;

        public static StorageFolder TemporaryFolder => ApplicationData.Current.TemporaryFolder;

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
}
