//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Td.Api;
using Windows.Storage;
using Future = Telegram.Services.StorageService.Future;

namespace Telegram.Services
{
    public partial class ClientService
    {
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
         * - The file is then added to the system FutureAccessList using the file UniqueId+temp as token.
         * Note: this only happens if FutureAccessList doesn't contain any of UniqueId or UniqueId+temp tokens.
         * 
         * 2. TrackDownloadedFile
         * - Whenever an UpdateFile event is received and the download is actually completed,
         * - we check in the FutureAccessList if there's any file belonging to it, by using UniqueId+temp as token.
         * - if this is the case, we retrieve both the file from cache and the temporary file in the Downloads folder.
         * - we then proceed by replacing the latter with a copy with the cache file, that is then renamed with the final name.
         * - finally we can remove UniqueId+temp from FutureAccessList and add the final UniqueId to the list.
         * 
         * # Using the files
         * The app will always rely on TDLib LocalFile to determine a file status.
         * This means that if the user clears the app cache, the link between cached and permanent files will be broken.
         * This considered, the user must be able to perform different actions on the downloaded files, including:
         * 
         * 1. OpenFile(With)Async and OpenFolderAsync (IStorageService)
         * - We make sure that the LocalFile from TDLib reports IsDownloadingCompleted as true
         * - If yes, we try to retrieve the permanent file from FutureAccessList using UniqueId
         *   - If the permanent file doesn't exist or it was edited after being copied, we do nothing
         *   - Otherwise we create a new unique copy of the file in the Downloads folder and we add it to the FutureAccessList
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
         * FutureAccessList is not kept synchronized by the system, so it's not enough to call ContainsItem,
         * a try-catch on GetFileAsync is needed to make sure that the file is still accessible.
         * Note: the file will still be visible as "downloaded" within the app.
         * 
         */

        private readonly HashSet<int> _canceledDownloads = new();
        private readonly HashSet<string> _completedDownloads = new();
        private readonly object _downloadsLock = new();

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
            else if (ApiInfo.HasCacheOnly || !SettingsService.Current.IsDownloadFolderEnabled)
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
                    var permanent = await Future.GetFileAsync(file.Remote.UniqueId);
                    if (permanent == null)
                    {
                        lock (_downloadsLock)
                        {
                            _completedDownloads.Add(file.Remote.UniqueId);
                        }

                        var source = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                        if (Future.CheckAccess(source))
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

                            var destination = await Future.CreateFileAsync(sourceName);

                            await source.CopyAndReplaceAsync(destination);
                            Future.AddOrReplace(file.Remote.UniqueId, destination);

                            return destination;
                        }
                    }

                    return permanent;
                }
                catch
                {
                    Future.Remove(file.Remote.UniqueId);
                }
            }

            return null;
        }

        public async void AddFileToDownloads(File file, long chatId, long messageId, int priority = 30)
        {
            Send(new AddFileToDownloads(file.Id, chatId, messageId, priority));

            if (ApiInfo.HasCacheOnly || !SettingsService.Current.IsDownloadFolderEnabled || Future.Contains(file.Remote.UniqueId, true) || await Future.ContainsAsync(file.Remote.UniqueId))
            {
                return;
            }

            try
            {
                StorageFile destination = await Future.CreateFileAsync($"Unconfirmed {file.Id}.tdownload");
                Future.AddOrReplace(file.Remote.UniqueId, destination, true);
            }
            catch
            {
                Future.Remove(file.Remote.UniqueId, true);
            }
        }

        private async void TrackDownloadedFile(File file)
        {
            if (ApiInfo.HasDownloadFolder
                && SettingsService.Current.IsDownloadFolderEnabled
                && file.Local.IsDownloadingCompleted
                && file.Remote.IsUploadingCompleted
                && Future.Contains(file.Remote.UniqueId, true))
            {
                lock (_downloadsLock)
                {
                    if (_completedDownloads.Contains(file.Remote.UniqueId))
                    {
                        return;
                    }

                    _completedDownloads.Add(file.Remote.UniqueId);
                }

                try
                {
                    StorageFile source = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                    StorageFile destination = await Future.GetFileAsync(file.Remote.UniqueId, true);

                    var sourceName = source.Name;

                    var response = await SendAsync(new GetSuggestedFileName(file.Id, string.Empty));
                    if (response is Text text)
                    {
                        sourceName = text.TextValue;
                    }

                    await source.CopyAndReplaceAsync(destination);
                    await destination.RenameAsync(sourceName, NameCollisionOption.GenerateUniqueName);

                    Future.Remove(file.Remote.UniqueId, true);
                    Future.AddOrReplace(file.Remote.UniqueId, destination);
                }
                catch
                {
                    Future.Remove(file.Remote.UniqueId, true);
                }
            }
        }

        public async void CancelDownloadFile(File file, bool onlyIfPending = false)
        {
            lock (_downloadsLock)
            {
                _canceledDownloads.Add(file.Id);
                _completedDownloads.Remove(file.Remote.UniqueId);
            }

            Send(new CancelDownloadFile(file.Id, onlyIfPending));
            Send(new RemoveFileFromDownloads(file.Id, false));

            if (ApiInfo.HasCacheOnly)
            {
                return;
            }

            try
            {
                var destination = await Future.GetFileAsync(file.Remote.UniqueId, true);

                Future.Remove(file.Remote.UniqueId, true);

                if (destination != null)
                {
                    await destination.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public bool IsDownloadFileCanceled(int fileId)
        {
            lock (_downloadsLock)
            {
                return _canceledDownloads.Contains(fileId);
            }
        }

        public void PrepareLogs(int fileId, int verbosityLevel)
        {
            _preparedLogsFileIds ??= new();
            _preparedLogsFileIds.Add(fileId);

            if (_preparedLogsVerbosity == -1)
            {
                _preparedLogsVerbosity = verbosityLevel;
            }
        }

        private File ProcessFile(File file)
        {
            if (_files.TryGetValue(file.Id, out File singleton))
            {
                singleton.Update(file);
                return singleton;
            }
            else
            {
                _files[file.Id] = file;

                if (file.Local.IsDownloadingCompleted && !NativeUtils.FileExists(file.Local.Path))
                {
                    Send(new DeleteFile(file.Id));
                }

                return file;
            }
        }
    }
}

