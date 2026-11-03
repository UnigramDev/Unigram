//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if UNPACKAGED
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Telegram.Common;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Storage;
using File = System.IO.File;
using FileAttributes = System.IO.FileAttributes;

namespace Telegram.Services
{
    /// <summary>
    /// Updates for the build Telegram.Installer puts on disk, which has no identity for
    /// PackageManager to install over. The app downloads Telegram-&lt;version&gt;-&lt;arch&gt;.zip,
    /// checks it against its manifest and extracts the files that differ from the running version;
    /// once the app has exited, Telegram.Updater copies them over it.
    ///
    ///   %LOCALAPPDATA%\TelegramFZ-LLC.Windows\
    ///     Updater.exe   copied out of app\ before each run, because it overwrites app\
    ///     app\          the running version
    ///     app.new\      the changed files, complete once app.new\.update holds the version;
    ///                   app.new\.delete lists what the new version no longer ships
    ///     app.old\      the files the update replaced, deleted on the next start
    ///
    /// Nothing renames app\: anything holding it - a process with it as its current directory, a
    /// file open in it - would block that, and nothing stops another process from doing either.
    /// </summary>
    public static class UnpackagedUpdates
    {
        // Held from the moment the updater is started, never released: the process is on its way
        // out, and a download finishing in the meantime must not replace the files being applied.
        private static readonly SemaphoreSlim _stageLock = new(1, 1);

        // The one thing the app writes into app\ itself: WebView2's default profile, beside the exe.
        private const string WebViewProfile = "Telegram.exe.WebView2";

        private static string RootPath => Path.GetDirectoryName(AppPaths.LocalPath);

        private static string StagedPath => Path.Combine(RootPath, "app.new");

        private static string PreviousPath => Path.Combine(RootPath, "app.old");

        private static string MarkerPath => Path.Combine(StagedPath, ".update");

        private static string RemovedPath => Path.Combine(StagedPath, ".delete");

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
                ClearStage();

                using var archive = ZipFile.OpenRead(package);
                using var manifest = ReadManifest(archive);

                var root = manifest.RootElement;
                CheckManifest(root, version, package);

                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];

                foreach (var file in root.GetProperty("files").EnumerateArray())
                {
                    var path = file.GetProperty("path").GetString();

                    using var input = Open(archive, "payload/" + path);
                    Extract(input, ResolveTarget(staged, path), file, hash, buffer);
                }

                Complete(staged, root, version, package);

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

        /// <summary>
        /// Stages an update by downloading only what differs from the running version: the
        /// package's central directory and manifest first, then each payload entry whose file is
        /// not already installed as the manifest describes it, all of them ranges of the same
        /// download. Unchanged files are not staged at all: the updater only copies what app.new
        /// holds.
        ///
        /// False when the package cannot be read this way; the caller then downloads it whole. A
        /// partial result is never left behind: the same checks as a full extraction gate the marker.
        /// </summary>
        public static async Task<bool> StageChangedFilesAsync(IClientService clientService, CloudUpdate update)
        {
            if (update.Document.Size <= 0)
            {
                return false;
            }

            await _stageLock.WaitAsync();

            try
            {
                // Every read blocks until its range is in, so this needs a thread of its own.
                return await Task.Run(() => StageChangedFiles(clientService, update));
            }
            finally
            {
                _stageLock.Release();
            }
        }

        public static bool IsStaged(Version version)
        {
            return ReadStagedVersion() == version;
        }

        private static bool StageChangedFiles(IClientService clientService, CloudUpdate update)
        {
            var file = update.Document;
            var version = update.Version;

            if (ReadStagedVersion() == version)
            {
                return true;
            }

            var staged = StagedPath;
            var source = $"the {version} package";

            try
            {
                ClearStage();

                using var package = new RemotePackage(clientService, file);

                var entries = package.ReadDirectory();

                using var manifest = JsonDocument.Parse(package.ReadAll(Find(entries, "manifest.json")));

                var root = manifest.RootElement;
                CheckManifest(root, version, source);

                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                var installed = AppPaths.InstalledPath;
                var unchanged = 0;
                var changed = new List<JsonElement>();
                long expected = 0;

                foreach (var entry in root.GetProperty("files").EnumerateArray())
                {
                    var path = entry.GetProperty("path").GetString();
                    var current = Path.Combine(installed, path.Replace('/', Path.DirectorySeparatorChar));

                    if (IsUnchanged(current, entry, hash, buffer))
                    {
                        unchanged++;
                    }
                    else
                    {
                        changed.Add(entry);
                        expected += Find(entries, "payload/" + path).CompressedSize;
                    }
                }

                // Against what has to be fetched, which the package's own size says nothing about.
                package.Track(update, expected);

                foreach (var entry in changed)
                {
                    var path = entry.GetProperty("path").GetString();

                    using var input = package.Open(Find(entries, "payload/" + path));
                    Extract(input, ResolveTarget(staged, path), entry, hash, buffer);
                }

                Complete(staged, root, version, source);

                // The parts fetched are of no use once staged, and a download that never completes
                // is otherwise left in TDLib's temporary folder.
                clientService.Send(new DeleteFile(file.Id));

                Logger.Info($"Staged {version}: {changed.Count} files downloaded, {package.Downloaded} bytes, {unchanged} unchanged");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Staging the changed files of {version} failed", ex);

                try
                {
                    Directory.Delete(staged, true);
                }
                catch { }

                return false;
            }
        }

        private static void CheckManifest(JsonElement root, Version version, string source)
        {
            if (!Version.TryParse(root.GetProperty("version").GetString(), out Version manifestVersion) || manifestVersion != version)
            {
                throw new InvalidDataException($"{source} is not version {version}");
            }

            if (root.GetProperty("arch").GetString() != ArchitectureName)
            {
                throw new InvalidDataException($"{source} is not for {ArchitectureName}");
            }
        }

        private static string ResolveTarget(string staged, string path)
        {
            // A path that climbs out, or a rooted one, would write outside app.new.
            var target = Path.GetFullPath(Path.Combine(staged, path));
            if (!target.StartsWith(staged + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{path} is outside the payload");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            return target;
        }

        private static void Extract(Stream input, string target, JsonElement file, IncrementalHash hash, byte[] buffer)
        {
            var expected = file.GetProperty("size").GetInt64();
            long size = 0;

            using (var output = File.Create(target))
            {
                // Bounded by the manifest: a stored entry read in place in the package has the
                // next entry after it, not the end of a stream.
                int read;
                while (size < expected && (read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, expected - size))) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    output.Write(buffer, 0, read);
                    size += read;
                }
            }

            if (!Matches(file, size, hash))
            {
                throw new InvalidDataException($"{file.GetProperty("path").GetString()} does not match the manifest");
            }
        }

        private static bool IsUnchanged(string current, JsonElement file, IncrementalHash hash, byte[] buffer)
        {
            var info = new FileInfo(current);
            if (!info.Exists || info.Length != file.GetProperty("size").GetInt64())
            {
                return false;
            }

            using (var input = File.OpenRead(current))
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                }
            }

            return Matches(file, info.Length, hash);
        }

        private static bool Matches(JsonElement file, long size, IncrementalHash hash)
        {
            var sha256 = Convert.ToHexString(hash.GetHashAndReset());
            return size == file.GetProperty("size").GetInt64()
                && string.Equals(sha256, file.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The updater backs up into app.old only the files it does not already hold there, so
        /// that a run picked up again after being cut short keeps the originals. That holds only
        /// if every update starts from an empty app.old.
        /// </summary>
        private static void ClearStage()
        {
            if (Directory.Exists(StagedPath))
            {
                Directory.Delete(StagedPath, true);
            }

            if (Directory.Exists(PreviousPath))
            {
                Directory.Delete(PreviousPath, true);
            }
        }

        private static void Complete(string staged, JsonElement root, Version version, string source)
        {
            var shipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in root.GetProperty("files").EnumerateArray())
            {
                shipped.Add(file.GetProperty("path").GetString().Replace('/', Path.DirectorySeparatorChar));
            }

            // The updater relaunches one and is run from a copy of the other, and whatever the
            // manifest does not list is deleted from app\.
            if (!shipped.Contains("Telegram.exe") || !shipped.Contains("Updater.exe"))
            {
                throw new InvalidDataException($"{source} has no Telegram.exe or Updater.exe");
            }

            // Every file may have been unchanged, leaving nothing to stage but the marker.
            Directory.CreateDirectory(staged);

            var removed = FindRemoved(shipped);
            if (removed.Count > 0)
            {
                File.WriteAllLines(RemovedPath, removed);
            }

            // Last: its presence is what tells the updater the folder is complete.
            File.WriteAllText(MarkerPath, version.ToString());
        }

        /// <summary>
        /// The files in app\ that the new version does not ship, relative to it. Links are not
        /// followed, and the WebView2 profile is the app's data rather than part of a version.
        /// </summary>
        private static List<string> FindRemoved(HashSet<string> shipped)
        {
            var installed = AppPaths.InstalledPath;
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            var files = new FileSystemEnumerable<string>(installed, static (ref FileSystemEntry entry) => entry.ToFullPath(), options)
            {
                ShouldIncludePredicate = static (ref FileSystemEntry entry) => !entry.IsDirectory,
                ShouldRecursePredicate = static (ref FileSystemEntry entry) => !entry.FileName.Equals(WebViewProfile, StringComparison.OrdinalIgnoreCase)
            };

            var removed = new List<string>();

            foreach (var file in files)
            {
                var path = Path.GetRelativePath(installed, file);
                if (!shipped.Contains(path))
                {
                    removed.Add(path);
                }
            }

            return removed;
        }

        private static ZipEntryRange Find(Dictionary<string, ZipEntryRange> entries, string name)
        {
            return entries.TryGetValue(name, out var entry) ? entry : throw new InvalidDataException($"{name} is missing");
        }

        private record ZipEntryRange(int Method, long CompressedSize, long Size, long LocalHeaderOffset);

        /// <summary>
        /// The update package read in place while TDLib downloads only the ranges asked for: the
        /// central directory says where every entry is, so nothing but the entries that are wanted
        /// is fetched. Only what Build.WinUI.ps1 writes is understood - stored or deflated entries,
        /// no zip64.
        ///
        /// The waiting is RemoteFileSource's, the one media plays through, and so is the read lease
        /// that keeps this from taking the download from anything else reading the same file.
        /// </summary>
        private sealed class RemotePackage : IDisposable
        {
            private const uint EndOfDirectorySignature = 0x06054b50;
            private const uint DirectoryEntrySignature = 0x02014b50;
            private const uint LocalHeaderSignature = 0x04034b50;

            private const int EndOfDirectorySize = 22;
            private const int DirectoryEntrySize = 46;
            private const int LocalHeaderSize = 30;

            private readonly RemoteFileSource _source;
            private readonly long _size;

            private SafeFileHandle _handle;
            private string _path;

            // What the source last reported as downloaded. Reads inside it are not asked for again,
            // because every ask sends the download window again too.
            private long _availableBegin;
            private long _availableEnd;

            public RemotePackage(IClientService clientService, Td.Api.File file)
            {
                _size = file.Size;

                // A preload because it is one: it downloads behind everything the chat shows. Not
                // streaming, so the window is the one each read asks for rather than a rate.
                _source = new RemoteFileSource(clientService, file, 0, false, FileReadRank.Preload);
                _source.Open();
            }

            public long Downloaded { get; private set; }

            private CloudUpdate _update;
            private long _expected;
            private long _consumed;

            /// <summary>
            /// Reports the entry data read from here on, out of expected bytes, as the update's
            /// staging progress. Reads block until their range is in, so it follows the download.
            /// </summary>
            public void Track(CloudUpdate update, long expected)
            {
                _update = update;
                _expected = expected;
                _consumed = 0;

                update.StagingProgress = 0;
            }

            private void OnConsumed(int count)
            {
                _consumed += count;

                if (_update != null && _expected > 0)
                {
                    _update.StagingProgress = (int)Math.Min(100, _consumed * 100 / _expected);
                }
            }

            public Dictionary<string, ZipEntryRange> ReadDirectory()
            {
                // The end record is last, followed only by a comment of up to 64 KB.
                var length = (int)Math.Min(_size, EndOfDirectorySize + ushort.MaxValue);
                var tail = Read(_size - length, length);

                var end = tail.Length - EndOfDirectorySize;
                while (end >= 0 && BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end)) != EndOfDirectorySignature)
                {
                    end--;
                }

                if (end < 0)
                {
                    throw new InvalidDataException("No end of central directory");
                }

                var count = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end + 10));
                var directorySize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end + 12));
                var directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(end + 16));

                // Saturated fields are how zip64 says the real values are elsewhere.
                if (count == ushort.MaxValue || directorySize == uint.MaxValue || directoryOffset == uint.MaxValue)
                {
                    throw new InvalidDataException("A zip64 package");
                }

                var directory = Read(directoryOffset, (int)directorySize);
                var entries = new Dictionary<string, ZipEntryRange>(count, StringComparer.Ordinal);
                var position = 0;

                for (int i = 0; i < count; i++)
                {
                    var record = directory.AsSpan(position);
                    if (BinaryPrimitives.ReadUInt32LittleEndian(record) != DirectoryEntrySignature)
                    {
                        throw new InvalidDataException("A broken central directory");
                    }

                    var method = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(10));
                    var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(20));
                    var size = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(24));
                    var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(28));
                    var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(30));
                    var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(32));
                    var localHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(42));

                    var name = Encoding.UTF8.GetString(record.Slice(DirectoryEntrySize, nameLength));
                    entries[name] = new ZipEntryRange(method, compressedSize, size, localHeaderOffset);

                    position += DirectoryEntrySize + nameLength + extraLength + commentLength;
                }

                return entries;
            }

            /// <summary>
            /// The entry's uncompressed bytes, read out of the partial download as they arrive.
            /// </summary>
            public Stream Open(ZipEntryRange entry)
            {
                if (entry.Method is not (0 or 8))
                {
                    throw new InvalidDataException($"Compression method {entry.Method}");
                }

                // The local header's name and extra field can differ from the directory's, so the
                // data starts where it says.
                var header = Read(entry.LocalHeaderOffset, LocalHeaderSize);
                if (BinaryPrimitives.ReadUInt32LittleEndian(header) != LocalHeaderSignature)
                {
                    throw new InvalidDataException("A broken local header");
                }

                var start = entry.LocalHeaderOffset + LocalHeaderSize
                    + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(26))
                    + BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28));

                Downloaded += entry.CompressedSize;

                var stream = new RangeStream(this, start, start + entry.CompressedSize);
                return entry.Method == 8 ? new DeflateStream(stream, CompressionMode.Decompress) : stream;
            }

            public byte[] ReadAll(ZipEntryRange entry)
            {
                using var input = Open(entry);

                var bytes = new byte[entry.Size];
                input.ReadExactly(bytes);
                return bytes;
            }

            private byte[] Read(long offset, int count)
            {
                var bytes = new byte[count];

                for (int done = 0; done < count;)
                {
                    var read = ReadAt(offset + done, bytes.AsSpan(done), offset + count);
                    if (read <= 0)
                    {
                        throw new IOException($"{count} bytes at {offset} could not be read");
                    }

                    done += read;
                }

                return bytes;
            }

            /// <summary>
            /// Up to as much as the buffer holds at the offset, once it has downloaded. The window
            /// asked for runs to the end of the range being read, so a range downloads in one go
            /// rather than a read at a time.
            /// </summary>
            private int ReadAt(long offset, Span<byte> buffer, long end)
            {
                if (offset < _availableBegin || offset >= _availableEnd)
                {
                    _source.SeekCallback(offset);
                    _source.ReadCallback(Math.Min(buffer.Length, end - offset), end - offset, out long available);

                    // -1 is the source giving up after its own timeout, and 0 the end of the file,
                    // which no range here reaches past.
                    if (available <= 0)
                    {
                        throw new IOException($"Nothing at {offset} downloaded");
                    }

                    _availableBegin = offset;
                    _availableEnd = offset + available;
                }

                // Asked again rather than kept: TDLib moves the file out of its temporary folder
                // once the last part is in, which a range can be.
                var path = _source.FilePath;
                if (_handle == null || _path != path)
                {
                    _handle?.Dispose();

                    // Sharing delete is what lets TDLib still move the file while it is open
                    // here; the handle keeps reading the same data from its new place.
                    _handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    _path = path;
                }

                return RandomAccess.Read(_handle, buffer.Slice(0, (int)Math.Min(buffer.Length, _availableEnd - offset)), offset);
            }

            public void Dispose()
            {
                _handle?.Dispose();
                _source.Close();
            }

            private sealed class RangeStream : Stream
            {
                private readonly RemotePackage _package;
                private readonly long _end;

                private long _position;

                public RangeStream(RemotePackage package, long begin, long end)
                {
                    _package = package;
                    _position = begin;
                    _end = end;
                }

                public override int Read(Span<byte> buffer)
                {
                    if (_position >= _end || buffer.IsEmpty)
                    {
                        return 0;
                    }

                    var read = _package.ReadAt(_position, buffer.Slice(0, (int)Math.Min(buffer.Length, _end - _position)), _end);
                    _position += read;
                    _package.OnConsumed(read);
                    return read;
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    return Read(buffer.AsSpan(offset, count));
                }

                public override bool CanRead => true;
                public override bool CanSeek => false;
                public override bool CanWrite => false;

                public override long Length => throw new NotSupportedException();

                public override long Position
                {
                    get => throw new NotSupportedException();
                    set => throw new NotSupportedException();
                }

                public override void Flush()
                {
                }

                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

                public override void SetLength(long value) => throw new NotSupportedException();

                public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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
        /// Blocks on copying the updater out and starting it, which has been seen to take seconds.
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
        /// What a finished update leaves behind: the files it replaced, a staged folder no newer
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

                // A newer version still staged may be an update cut short, and its updater needs
                // the originals in app.old to put back if it is cancelled.
                var staged = ReadStagedVersion();
                if (staged == null || staged <= current)
                {
                    ClearStage();
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
