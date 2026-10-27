//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using Telegram.Common;

namespace Windows.Storage
{
    /// <summary>
    /// <see cref="ApplicationData.Current"/> throws without package identity, and the two roots it
    /// hands out are the app's whole idea of where it lives - the TDLib databases, the langpack,
    /// the themes, the crash reports and every cached file are under one of them. So this is the
    /// single place that answers where they are, and everything downstream keeps taking the same
    /// paths and the same <see cref="StorageFolder"/>s.
    ///
    /// Unpackaged the container layout is rebuilt verbatim under <c>%LOCALAPPDATA%\Unigram</c>,
    /// LocalState, LocalCache and TempState included, so a profile can be copied either way by hand.
    /// </summary>
    public static class ApplicationDataEx
    {
#if UNPACKAGED
        // Read once. The answer cannot change while the process lives, and every path below is on
        // the startup path where a P/Invoke per call would be noise.
        private static readonly bool _packaged = ApiInfo.HasPackageIdentity;

        private static string _localPath;
        public static string LocalPath => _localPath ??= _packaged
            ? ApplicationData.Current.LocalFolder.Path
            : EnsureFolder("LocalState");

        private static string _temporaryPath;
        public static string TemporaryPath => _temporaryPath ??= _packaged
            ? ApplicationData.Current.TemporaryFolder.Path
            : EnsureFolder("TempState");

        private static string _localCachePath;
        public static string LocalCachePath => _localCachePath ??= _packaged
            ? ApplicationData.Current.LocalCacheFolder.Path
            : EnsureFolder("LocalCache");

        private static StorageFolder _localFolder;
        public static StorageFolder LocalFolder => _localFolder ??= _packaged
            ? ApplicationData.Current.LocalFolder
            : GetFolder(LocalPath);

        private static StorageFolder _temporaryFolder;
        public static StorageFolder TemporaryFolder => _temporaryFolder ??= _packaged
            ? ApplicationData.Current.TemporaryFolder
            : GetFolder(TemporaryPath);

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
                "Unigram",
                name);

            Directory.CreateDirectory(path);
            return path;
        }

        // Blocking, and it has to be: every consumer of these two roots is synchronous, and the
        // first of them runs from App's constructor, where there is nothing to await from. The
        // directory was just created locally, and the completion arrives on a pool thread rather
        // than on this one, so neither the cost nor the wait is a risk.
        private static StorageFolder GetFolder(string path)
        {
            return StorageFolder.GetFolderFromPathAsync(path).AsTask().GetAwaiter().GetResult();
        }
#else
        public static string LocalPath => ApplicationData.Current.LocalFolder.Path;

        public static string TemporaryPath => ApplicationData.Current.TemporaryFolder.Path;

        public static string LocalCachePath => ApplicationData.Current.LocalCacheFolder.Path;

        public static StorageFolder LocalFolder => ApplicationData.Current.LocalFolder;

        public static StorageFolder TemporaryFolder => ApplicationData.Current.TemporaryFolder;

        public static Uri GetLocalUri(string relativePath) => new Uri("ms-appdata:///local/" + relativePath);
#endif
    }
}
