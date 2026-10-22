//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.Storage;

namespace Telegram.Services
{
    public partial class ClientService
    {
        // Files TDLib believes are downloaded, waiting to be confirmed on disk.
        //
        // TDLib's own database goes out of sync with the file system - a download saved elsewhere,
        // an upload whose source moved, or a user who opened the folder and cleaned it out - and
        // the first time an id is seen is when that is worth noticing. The check itself is a
        // syscall: 74 µs against a cold cache at startup, where parsing an entire update takes
        // 10.7, and 1,152 of them at app start were a third of everything the TDLib thread spent
        // parsing. See Telegram.Benchmarks/README.md.
        //
        // Nothing waits on the answer. The only outcome is a DeleteFile that TDLib acts on whenever
        // it arrives, so the check does not have to happen in the middle of a parse - only before
        // the app tries to use the file, which is a user action away.
        private readonly ConcurrentQueue<(int Id, string Path)> _unverifiedFiles = new();
        private int _verifyingFiles;

        private void VerifyFileExists(int fileId, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            _unverifiedFiles.Enqueue((fileId, path));

            // One drain at a time. These arrive in bursts - a chat list is a thousand files in a
            // few hundred milliseconds - and a work item each would be a thousand thread pool hops
            // for syscalls that queue up behind one another on the disk regardless.
            if (Interlocked.CompareExchange(ref _verifyingFiles, 1, 0) == 0)
            {
                Task.Run(VerifyFiles);
            }
        }

        private void VerifyFiles()
        {
            do
            {
                while (_unverifiedFiles.TryDequeue(out var file))
                {
                    var started = TdThroughput.BeginHandler();
                    var exists = NativeFile.Exists(file.Path);
                    TdThroughput.RecordFileCheck(started);

                    if (!exists)
                    {
                        Send(new DeleteFile(file.Id));
                    }
                }

                Volatile.Write(ref _verifyingFiles, 0);
            }
            // Anything enqueued between the last dequeue and releasing the flag found the drain
            // still running and did not start one, so it would sit here until the next file
            // arrived. Whoever re-acquires the flag - this loop or that producer - runs; only one
            // of them can.
            while (!_unverifiedFiles.IsEmpty && Interlocked.CompareExchange(ref _verifyingFiles, 1, 0) == 0);
        }

        /*
         * How does this work?
         * 
         * As a general rule, all files are downloaded by TDLib into the app cache.
         * The goal however, is to make the local cache folder invisible to the user,
         * and to only provide access to the files through the Downloads folder instead.
         * 
         * # Automatic downloads
         * Nothing happens in this case, automatic downloads always end up in cache.
         * 
         * # Manual downloads
         * All the downloads that pass through the download manager (aka manual downloads)
         * are automatically copied to the user Downloads folder as soon as the download is completed.
         * We do this operation in two steps:
         * 
         * 1. AddFileToDownloads
         * - When the download is started, a temporary file is created in the final location.
         * - The file will look something like this: Unconfirmed {fileId}.tdownload
         * - The file is then staged with IDownloadFolderService under the file UniqueId.
         * Note: this only happens if the file is neither already staged nor already saved.
         * 
         * 2. TrackDownloadedFile
         * - Whenever an UpdateFile event is received and the download is actually completed,
         * - we check with IDownloadFolderService whether anything was staged for it.
         * - if this is the case, we retrieve both the file from cache and the temporary file in the Downloads folder.
         * - we then proceed by replacing the latter with a copy with the cache file, that is then renamed with the final name.
         * - finally we commit it, which drops the staged entry and records the saved one.
         * 
         * # Using the files
         * The app will always rely on TDLib LocalFile to determine a file status.
         * This means that if the user clears the app cache, the link between cached and permanent files will be broken.
         * This considered, the user must be able to perform different actions on the downloaded files, including:
         * 
         * 1. OpenFile(With)Async and OpenFolderAsync (IStorageService)
         * - We make sure that the LocalFile from TDLib reports IsDownloadingCompleted as true
         * - If yes, we ask IDownloadFolderService for the saved file
         *   - If the saved file doesn't exist or it was edited after being copied, we do nothing
         *   - Otherwise we create a new unique copy of the file in the Downloads folder and record it
         * - We launch the file
         * 
         * 2. SaveFileAsAsync (IStorageService)
         * - We make sure that the LocalFile from TDLib reports IsDownloadingCompleted as true
         * - If yes, we try to retrieve the cache file
         *   - We save the copy
         * - If not, and the download didn't start yet
         *   - We call AddFileToDownloads passing the custom location
         * 
         * # Other scenarios
         * All the stuff that needs to be also considered:
         * 
         * 1. User manually deletes the permanent file
         * The record is not kept synchronized with the file system, so asking whether it exists is not
         * enough: only reading the file back proves it is still there, which is what the service does.
         * Note: the file will still be visible as "downloaded" within the app.
         * 
         */

        private readonly IDownloadFolderService _downloadFolder;

        private readonly HashSet<int> _canceledDownloads = new();
        // Unique ids are global, but the copy is not: two accounts holding the same file each
        // save their own, so this is per-session and not the download folder's business.
        private readonly HashSet<string> _completedDownloads = new();
        private readonly HashSet<int> _explicitDownloads = new();
        private readonly Dictionary<int, List<FileReadLease>> _fileReads = new();
        private long _fileReadSequence;
        private readonly object _downloadsLock = new();

        /// <summary>
        /// File ids and unique ids only mean anything within one session, so a new
        /// authorization must not inherit them. They are also the only state here that
        /// grows for as long as the process lives, one entry per file ever downloaded.
        /// </summary>
        private void ClearDownloads()
        {
            lock (_downloadsLock)
            {
                _canceledDownloads.Clear();
                _completedDownloads.Clear();
                _explicitDownloads.Clear();

                // Their readers still hold them, and must find them closed.
                foreach (var leases in _fileReads.Values)
                {
                    foreach (var lease in leases)
                    {
                        lease.IsClosed = true;
                    }
                }

                _fileReads.Clear();
            }
        }

        public Task<File> GetFileAsync(int fileId)
        {
            var tsc = new TaskCompletionSource<File>();
            Send(new GetFile(fileId), result =>
            {
                if (result is File file)
                {
                    tsc.SetResult(file);
                }
                else
                {
                    tsc.SetResult(null);
                }
            });

            return tsc.Task;
        }

        public async Task<StorageFile> GetFileAsync(File file, bool completed = true)
        {
            if (file == null)
            {
                return null;
            }

            // Extremely important to do this only for completed,
            // as this method is being used by RemoteFileStream as well.
            if (completed)
            {
                // Counts as asking for the file: the caller wants all of it, and on a file
                // that is not in the cache this starts the download rather than checking it.
                TrackExplicitDownload(file.Id);

                await SendAsync(new DownloadFile(file.Id, 16, 0, 0, false));
            }

            if (file.Local.IsDownloadingCompleted || !completed)
            {
                try
                {
                    return await StorageFile.GetFileFromPathAsync(file.Local.Path);
                }
                catch (System.IO.FileNotFoundException)
                {
                    Send(new DeleteFile(file.Id));
                }
                catch { }

                return null;
            }

            return null;
        }

        public async Task<StorageFile> GetPermanentFileAsync(File file)
        {
            if (file == null)
            {
                return null;
            }
            else if (!_downloadFolder.IsSupported || !AppSettings.IsDownloadFolderEnabled)
            {
                return await GetFileAsync(file, true);
            }

            // Let's TDLib check the file integrity
            if (file.Local.IsDownloadingCompleted)
            {
                await SendAsync(new DownloadFile(file.Id, 16, 0, 0, false));
            }

            // If it's still valid, we can proceed with the operation
            if (file.Local.IsDownloadingCompleted && file.Remote.UniqueId.Length > 0)
            {
                try
                {
                    var permanent = await _downloadFolder.GetSavedAsync(file.Remote.UniqueId);
                    if (permanent == null)
                    {
                        lock (_downloadsLock)
                        {
                            _completedDownloads.Add(file.Remote.UniqueId);
                        }

                        var source = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                        if (_downloadFolder.CanRemember(source))
                        {
                            return source;
                        }
                        else
                        {
                            var sourceName = source.Name;

                            var response = await SendAsync(new GetSuggestedFileName(file.Id, string.Empty));
                            if (response is Text text)
                            {
                                sourceName = text.TextValue;
                            }

                            var destination = await _downloadFolder.CreateAsync(sourceName);

                            await source.CopyAndReplaceAsync(destination);
                            _downloadFolder.Commit(file.Remote.UniqueId, destination);

                            return destination;
                        }
                    }

                    return permanent;
                }
                catch
                {
                    _downloadFolder.Forget(file.Remote.UniqueId);
                }
            }

            return null;
        }

        public async void AddFileToDownloads(File file, long chatId, long messageId, int priority = 30)
        {
            TrackExplicitDownload(file.Id);

            Send(new AddFileToDownloads(file.Id, chatId, messageId, priority));

            if (!_downloadFolder.IsSupported || !AppSettings.IsDownloadFolderEnabled || _downloadFolder.IsStaged(file.Remote.UniqueId) || await _downloadFolder.ContainsAsync(file.Remote.UniqueId))
            {
                return;
            }

            try
            {
                await _downloadFolder.StageAsync(file.Remote.UniqueId, $"Unconfirmed {file.Id}.tdownload");
            }
            catch
            {
                _downloadFolder.Forget(file.Remote.UniqueId);
            }
        }

        private async void TrackDownloadedFile(File file)
        {
            if (_downloadFolder.IsSupported
                && AppSettings.IsDownloadFolderEnabled
                && file.Local.IsDownloadingCompleted
                && file.Remote.IsUploadingCompleted
                && _downloadFolder.IsStaged(file.Remote.UniqueId))
            {
                // TDLib reports a file as downloaded again every time it is asked for, so the
                // copy is claimed once and only the claiming call makes it.
                lock (_downloadsLock)
                {
                    if (!_completedDownloads.Add(file.Remote.UniqueId))
                    {
                        return;
                    }
                }

                try
                {
                    StorageFile source = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    StorageFile destination = await _downloadFolder.GetStagedAsync(file.Remote.UniqueId);

                    var sourceName = source.Name;

                    var response = await SendAsync(new GetSuggestedFileName(file.Id, string.Empty));
                    if (response is Text text)
                    {
                        sourceName = text.TextValue;
                    }

                    await source.CopyAndReplaceAsync(destination);
                    await destination.RenameAsync(sourceName, NameCollisionOption.GenerateUniqueName);

                    _downloadFolder.Commit(file.Remote.UniqueId, destination);
                }
                catch
                {
                    _downloadFolder.Forget(file.Remote.UniqueId);
                }
            }
        }

        public async void CancelDownloadFile(File file, bool onlyIfPending = false)
        {
            lock (_downloadsLock)
            {
                _canceledDownloads.Add(file.Id);

                // The file may be downloaded again, and must then be free to be saved again.
                _completedDownloads.Remove(file.Remote.UniqueId);

                // The download the user is cancelling is the one that made the file
                // explicit, so a reader left streaming it now owns what remains of it.
                _explicitDownloads.Remove(file.Id);
            }

            Send(new CancelDownloadFile(file.Id, onlyIfPending));
            Send(new RemoveFileFromDownloads(file.Id, false));

            if (!_downloadFolder.IsSupported)
            {
                return;
            }

            await _downloadFolder.DiscardAsync(file.Remote.UniqueId);
        }

        public bool IsDownloadFileCanceled(int fileId)
        {
            lock (_downloadsLock)
            {
                return _canceledDownloads.Contains(fileId);
            }
        }

        /// <summary>
        /// Whether the file is only downloading to feed a reader that is playing it.
        ///
        /// Such a reader asks for the window it is about to need and moves it forwards as
        /// it plays, so TDLib reports the download as active again every time the window
        /// moves, and inactive every time one is satisfied. Nothing asked for the file
        /// itself, so the download buttons must not follow that: they would flicker
        /// between download and cancel for as long as the media plays.
        /// </summary>
        public bool IsDownloadFileImplicit(int fileId)
        {
            lock (_downloadsLock)
            {
                return _fileReads.ContainsKey(fileId) && !_explicitDownloads.Contains(fileId);
            }
        }

        /// <param name="preempted">
        /// Called when a reader of a higher rank opens the file, outside any lock. Nothing the
        /// lease asks for is sent after that, so its reader should close.
        /// </param>
        public FileReadLease OpenFileRead(int fileId, FileReadRank rank, Action preempted = null)
        {
            var lease = new FileReadLease(fileId, rank, preempted);
            List<FileReadLease> outranked = null;

            lock (_downloadsLock)
            {
                if (!_fileReads.TryGetValue(fileId, out var leases))
                {
                    _fileReads[fileId] = leases = new List<FileReadLease>();
                }

                foreach (var other in leases)
                {
                    if (other.IsPreempted)
                    {
                        continue;
                    }

                    if (other.Rank > rank)
                    {
                        lease.Preempt();
                    }
                    else if (other.Rank < rank)
                    {
                        other.Preempt();

                        outranked ??= new List<FileReadLease>();
                        outranked.Add(other);
                    }
                }

                leases.Add(lease);
            }

            if (outranked != null)
            {
                foreach (var other in outranked)
                {
                    other.Preempted?.Invoke();
                }
            }

            return lease;
        }

        /// <summary>
        /// Asks for the window the reader is about to need. Readers of the same rank each send
        /// theirs, the last one winning, as the same video can play in the gallery over the
        /// bubble behind it.
        ///
        /// Sent directly rather than through <see cref="DownloadFile"/>, which records the
        /// file as one that was asked for in its own right.
        /// </summary>
        public void RequestFileRead(FileReadLease lease, int priority, long offset, long limit)
        {
            lock (_downloadsLock)
            {
                if (lease.IsClosed || lease.IsPreempted)
                {
                    return;
                }

                lease.HasWindow = true;
                lease.Priority = priority;
                lease.Offset = offset;
                lease.Limit = limit;
                lease.Sequence = ++_fileReadSequence;

                SendFileRead(lease);
            }
        }

        /// <param name="cancel">
        /// Whether to stop the download if no other reader is left, otherwise the last window
        /// keeps running for a video nobody is watching. It never stops a download of the
        /// whole file that something asked for: see <see cref="TrackExplicitDownload"/>.
        /// </param>
        public void CloseFileRead(FileReadLease lease, bool cancel)
        {
            lock (_downloadsLock)
            {
                if (lease.IsClosed)
                {
                    return;
                }

                lease.IsClosed = true;

                if (!_fileReads.TryGetValue(lease.FileId, out var leases))
                {
                    return;
                }

                leases.Remove(lease);

                FileReadLease next = null;

                foreach (var other in leases)
                {
                    if (other.HasWindow && !other.IsPreempted && (next == null || other.Sequence > next.Sequence))
                    {
                        next = other;
                    }
                }

                if (leases.Count == 0)
                {
                    _fileReads.Remove(lease.FileId);
                }

                if (next != null)
                {
                    // The window running was this lease's, and the reader left behind would
                    // otherwise wait for its next retry to ask for its own.
                    if (lease.Sequence > next.Sequence && !lease.IsPreempted)
                    {
                        SendFileRead(next);
                    }
                }
                else if (cancel && !lease.IsPreempted && !_explicitDownloads.Contains(lease.FileId))
                {
                    // Nothing else to undo: a download that exists only to feed a reader was
                    // never added to the download list nor staged in the download folder, and
                    // it must not be remembered as canceled, which would keep auto-download from
                    // ever picking the file up again.
                    Send(new CancelDownloadFile(lease.FileId, false));
                }
            }
        }

        private void SendFileRead(FileReadLease lease)
        {
            // A window would replace a download of the whole file, which then stops for good
            // where the window ends. With no limit TDLib downloads from the offset to the end and
            // wraps around to the start, so the file still completes.
            var limit = _explicitDownloads.Contains(lease.FileId) ? 0 : lease.Limit;
            Send(new DownloadFile(lease.FileId, lease.Priority, lease.Offset, limit, false));
        }

        /// <summary>
        /// Records that the whole file was asked for, by the user, by auto-download or by a
        /// caller that needs all of it, rather than a reader pulling in the parts it needs.
        ///
        /// A reader closing stops the download it leaves behind, but never one of these, and
        /// while one runs a reader moves its offset without limiting it.
        ///
        /// A prefix is not recorded: it is a preload, which a reader can take over and stop.
        /// </summary>
        private void TrackExplicitDownload(int fileId)
        {
            lock (_downloadsLock)
            {
                _explicitDownloads.Add(fileId);
            }
        }

        public void PrepareLogs(int fileId, int verbosityLevel)
        {
            lock (_preparedLogsLock)
            {
                _preparedLogsFileIds ??= new();
                _preparedLogsFileIds.Add(fileId);

                if (_preparedLogsVerbosity == -1)
                {
                    _preparedLogsVerbosity = verbosityLevel;
                }
            }
        }
    }
}

