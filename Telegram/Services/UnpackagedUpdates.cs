//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if UNPACKAGED
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Windows.Storage;

namespace Telegram.Services
{
    /// <summary>
    /// Updates for the build Telegram.Installer puts on disk, which has no identity for
    /// PackageManager to install over. The app downloads Telegram-&lt;version&gt;-&lt;arch&gt;.zip,
    /// checks it against its manifest and extracts it beside the running version; once the app has
    /// exited, Telegram.Updater swaps the two folders.
    ///
    ///   %LOCALAPPDATA%\TelegramFZ-LLC.Windows\
    ///     Updater.exe   copied out of app\ before each run, because app\ is what it renames
    ///     app\          the running version
    ///     app.new\      the staged one, complete once app.new\.update holds its version
    ///     app.old\      the one replaced, deleted on the next start
    /// </summary>
    public static class UnpackagedUpdates
    {
        // Held from the moment the updater is started, never released: the process is on its way
        // out, and a download finishing in the meantime must not replace the folder being swapped.
        private static readonly SemaphoreSlim _stageLock = new(1, 1);

        private static string RootPath => Path.GetDirectoryName(AppPaths.LocalPath);

        private static string StagedPath => Path.Combine(RootPath, "app.new");

        private static string MarkerPath => Path.Combine(StagedPath, ".update");

        /// <summary>
        /// Whether this is the copy the installer laid out. A build run from anywhere else, an
        /// output folder say, shares the data root but not the folder the updater would replace.
        /// </summary>
        public static bool IsInstalled => !ApiInfo.HasPackageIdentity
            && string.Equals(AppPaths.InstalledPath, Path.Combine(RootPath, "app"), StringComparison.OrdinalIgnoreCase);

        private static string ArchitectureName => RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        public const string Extension = ".zip";

        /// <summary>
        /// Telegram-13.0.1.0-x64.zip. Anything else in the channel is for another flavour or
        /// another architecture.
        /// </summary>
        public static bool TryParseFileName(string fileName, out Version version)
        {
            version = null;

            if (!fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var split = Path.GetFileNameWithoutExtension(fileName).Split('-');
            return split.Length == 3
                && split[0] == "Telegram"
                && split[2] == ArchitectureName
                && Version.TryParse(split[1], out version);
        }

        /// <summary>
        /// The package, once it has been extracted to app.new; null if it could not be. Until then
        /// it is not an update the user can restart into.
        /// </summary>
        public static async Task<StorageFile> StageAsync(StorageFile package, Version version)
        {
            if (package == null)
            {
                return null;
            }

            await _stageLock.WaitAsync();

            try
            {
                return await Task.Run(() => Stage(package.Path, version)) ? package : null;
            }
            finally
            {
                _stageLock.Release();
            }
        }

        private static bool Stage(string package, Version version)
        {
            if (ReadStagedVersion() == version)
            {
                return true;
            }

            var staged = StagedPath;

            try
            {
                if (Directory.Exists(staged))
                {
                    Directory.Delete(staged, true);
                }

                using var archive = ZipFile.OpenRead(package);
                using var manifest = ReadManifest(archive);

                var root = manifest.RootElement;

                if (!Version.TryParse(root.GetProperty("version").GetString(), out Version manifestVersion) || manifestVersion != version)
                {
                    throw new InvalidDataException($"{package} is not version {version}");
                }

                if (root.GetProperty("arch").GetString() != ArchitectureName)
                {
                    throw new InvalidDataException($"{package} is not for {ArchitectureName}");
                }

                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                var prefix = staged + Path.DirectorySeparatorChar;

                foreach (var file in root.GetProperty("files").EnumerateArray())
                {
                    var path = file.GetProperty("path").GetString();

                    // A path that climbs out, or a rooted one, would write outside app.new.
                    var target = Path.GetFullPath(Path.Combine(staged, path));
                    if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"{path} is outside the payload");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target));

                    long size = 0;

                    using (var input = Open(archive, "payload/" + path))
                    using (var output = File.Create(target))
                    {
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            hash.AppendData(buffer, 0, read);
                            output.Write(buffer, 0, read);
                            size += read;
                        }
                    }

                    var sha256 = Convert.ToHexString(hash.GetHashAndReset());

                    if (size != file.GetProperty("size").GetInt64() || !string.Equals(sha256, file.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"{path} does not match the manifest");
                    }
                }

                // The updater launches one and is relaunched from the other, so a payload without
                // either would leave nothing to start.
                if (!File.Exists(Path.Combine(staged, "Telegram.exe")) || !File.Exists(Path.Combine(staged, "Updater.exe")))
                {
                    throw new InvalidDataException($"{package} has no Telegram.exe or Updater.exe");
                }

                // Last: its presence is what tells the updater the folder is complete.
                File.WriteAllText(MarkerPath, version.ToString());

                Logger.Info($"Staged {version}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Staging {version} failed", ex);

                try
                {
                    Directory.Delete(staged, true);
                }
                catch { }

                return false;
            }
        }

        // Disposed before any payload entry is opened, rather than held across the extraction.
        private static JsonDocument ReadManifest(ZipArchive archive)
        {
            using var stream = Open(archive, "manifest.json");
            return JsonDocument.Parse(stream);
        }

        private static Stream Open(ZipArchive archive, string name)
        {
            var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"{name} is missing");
            return entry.Open();
        }

        private static Version ReadStagedVersion()
        {
            try
            {
                var marker = MarkerPath;
                if (File.Exists(marker) && Version.TryParse(File.ReadAllText(marker).Trim(), out Version version))
                {
                    return version;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Starts Telegram.Updater if a newer version is staged, after which the caller has to exit
        /// for it to proceed: it waits for this process before touching app\.
        ///
        /// Logs nothing, because Program calls it before the logger can be brought up.
        /// </summary>
        public static bool TryStartUpdater()
        {
            if (!IsInstalled || !_stageLock.Wait(0))
            {
                return false;
            }

            var staged = ReadStagedVersion();
            if (staged == null || staged <= AppIdentity.Version.ToVersion())
            {
                _stageLock.Release();
                return false;
            }

            try
            {
                var root = RootPath;
                var updater = Path.Combine(root, "Updater.exe");

                File.Copy(AppPaths.GetInstalledPath("Updater.exe"), updater, true);

                // The creation time tells the updater whether the id it was given still names this
                // process by the time it looks, rather than one that reused it.
                using var current = Process.GetCurrentProcess();

                var info = new ProcessStartInfo(updater)
                {
                    UseShellExecute = false,
                    WorkingDirectory = root
                };

                info.ArgumentList.Add("--pid");
                info.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
                info.ArgumentList.Add("--created");
                info.ArgumentList.Add(current.StartTime.ToFileTime().ToString(CultureInfo.InvariantCulture));

                using var process = Process.Start(info);
                return true;
            }
            catch
            {
                _stageLock.Release();
                return false;
            }
        }

        /// <summary>
        /// What a finished update leaves behind: the version it replaced, a staged folder no newer
        /// than this one, and the packages already installed.
        /// </summary>
        public static void RemoveLeftovers()
        {
            if (!IsInstalled || !_stageLock.Wait(0))
            {
                return;
            }

            try
            {
                var current = AppIdentity.Version.ToVersion();

                var previous = Path.Combine(RootPath, "app.old");
                if (Directory.Exists(previous))
                {
                    Directory.Delete(previous, true);
                }

                var staged = ReadStagedVersion();
                if (Directory.Exists(StagedPath) && (staged == null || staged <= current))
                {
                    Directory.Delete(StagedPath, true);
                }

                var updates = Path.Combine(AppPaths.LocalPath, "updates");
                if (Directory.Exists(updates))
                {
                    foreach (var file in Directory.EnumerateFiles(updates, "*" + Extension))
                    {
                        if (Version.TryParse(Path.GetFileNameWithoutExtension(file), out Version version) && version <= current)
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Removing update leftovers failed", ex);
            }
            finally
            {
                _stageLock.Release();
            }
        }
    }
}
#endif
