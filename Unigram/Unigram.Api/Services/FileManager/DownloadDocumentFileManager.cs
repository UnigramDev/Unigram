using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Native.TL;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;
using Telegram.Api.TL.Upload;
using Telegram.Api.TL.Upload.Methods;
using Windows.Foundation;
using Windows.Security.Cryptography;

namespace Telegram.Api.Services.FileManager
{
    public interface IDownloadManager
    {
        IAsyncOperationWithProgress<DownloadableItem, double> DownloadFileAsync(string fileName, int dcId, TLInputDocumentFileLocation file, int fileSize, IProgress<double> test = null);

        void DownloadFile(string fileName, int dcId, TLInputDocumentFileLocation file, TLObject owner, int fileSize);

        void Cancel(TLDocument document);
    }

    public interface IDownloadVideoFileManager : IDownloadManager
    {
    }

    public interface IDownloadAudioFileManager : IDownloadManager
    {
    }

    public interface IDownloadDocumentFileManager : IDownloadManager
    {
    }

    public class DownloadDocumentFileManager : IDownloadDocumentFileManager, IDownloadVideoFileManager, IDownloadAudioFileManager
    {
        private readonly object _randomRoot = new object();

        private readonly Random _random = new Random();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly object _itemsSyncRoot = new object();

        private readonly List<DownloadableItem> _items = new List<DownloadableItem>();

        private readonly ITelegramEventAggregator _eventAggregator;
        private readonly IMTProtoService _protoService;
        private readonly IStatsService _statsService;
        private readonly DataType _dataType;

        public DownloadDocumentFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService, IStatsService statsService, DataType dataType)
        {
            _eventAggregator = eventAggregator;
            _protoService = mtProtoService;
            _statsService = statsService;
            _dataType = dataType;

            var timer = Stopwatch.StartNew();
            for (int i = 0; i < Constants.BigFileDownloadersCount; i++)
            {
                var worker = new Worker(OnDownloading, "documentDownloader" + i);
                _workers.Add(worker);
            }

            TLUtils.WritePerformance("Start workers timer: " + timer.Elapsed);
        }

        private void OnDownloading(object state)
        {
            DownloadablePart part = null;
            lock (_itemsSyncRoot)
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    if (item.IsCancelled)
                    {
                        _items.RemoveAt(i--);
                        try
                        {
                            //_eventAggregator.Publish(new UploadingCanceledEventArgs(item));
                        }
                        catch (Exception e)
                        {
                            TLUtils.WriteException(e);
                        }
                    }
                }

                foreach (var item in _items)
                {
                    part = item.Parts.FirstOrDefault(x => x.Status == PartStatus.Ready);
                    if (part != null)
                    {
                        part.Status = PartStatus.Processing;
                        break;
                    }
                }
            }

            if (part == null)
            {
                var currentWorker = (Worker)state;
                currentWorker.Stop();
                return;
            }

            var partName = part.ParentItem.InputDocumentLocation.GetPartFileName(part.Number);//string.Format("document{0}_{1}_{2}.dat", part.ParentItem.InputDocumentLocation.Id, part.ParentItem.InputDocumentLocation.AccessHash, part.Number);

            TLRPCError error;
            bool canceled;
            do
            {
                TLUploadFileBase result;
                if (part.ParentItem.CdnRedirect != null)
                    result = GetCdnFile(part.ParentItem.CdnRedirect, part.ParentItem.DCId, part.ParentItem.InputDocumentLocation, part.Offset, part.Limit, out error, out canceled);
                else
                    result = GetFile(part.ParentItem.DCId, part.ParentItem.InputDocumentLocation, part.Offset, part.Limit, out error, out canceled);

                if (result is TLUploadFileCdnRedirect redirect)
                {
                    part.ParentItem.CdnRedirect = redirect;
                    continue;
                }
                else
                {
                    part.File = result as TLUploadFile;
                }

                if (canceled)
                {
                    lock (_itemsSyncRoot)
                    {
                        part.ParentItem.IsCancelled = true;
                        part.Status = PartStatus.Processed;
                        _items.Remove(part.ParentItem);
                    }

                    return;
                }
            } while (part.File == null);

            // indicate progress
            // indicate complete
            bool isComplete;
            bool isCanceled;
            var progress = 0.0;
            lock (_itemsSyncRoot)
            {
                part.Status = PartStatus.Processed;

                if (!part.ParentItem.SuppressMerge)
                {
                    FileUtils.CheckMissingPart(_itemsSyncRoot, part, partName);
                }

                isCanceled = part.ParentItem.IsCancelled;

                isComplete = part.ParentItem.Parts.All(x => x.Status == PartStatus.Processed);
                if (!isComplete)
                {
                    var downloadedCount = part.ParentItem.Parts.Count(x => x.Status == PartStatus.Processed);
                    var count = part.ParentItem.Parts.Count;
                    progress = downloadedCount / (double)count;
                }
                else
                {
                    //var id = part.ParentItem.InputDocumentLocation.Id;
                    //var accessHash = part.ParentItem.InputDocumentLocation.AccessHash;
                    //var fileExtension = Path.GetExtension(part.ParentItem.FileName.ToString());
                    //var fileName = string.Format("document{0}_{1}{2}", id, accessHash, fileExtension);

                    //if (fileName.EndsWith(".mp4"))
                    //{
                    //    Logs.Log.SyncWrite("FileManager.IsComplete " + fileName + " hash=" + part.ParentItem.GetHashCode());
                    //}

                    _items.Remove(part.ParentItem);
                }
            }

            if (!isCanceled)
            {
                if (isComplete)
                {
                    //var id = part.ParentItem.InputDocumentLocation.Id;
                    //var accessHash = part.ParentItem.InputDocumentLocation.AccessHash;
                    //var version = part.ParentItem.InputDocumentLocation.Version;
                    var fileExtension = Path.GetExtension(part.ParentItem.FileName.ToString());
                    var fileName = GetFileName(part.ParentItem.InputDocumentLocation, fileExtension);
                    Func<DownloadablePart, string> getPartName = x => part.ParentItem.InputDocumentLocation.GetPartFileName(x.Number);

                    if (!part.ParentItem.SuppressMerge)
                    {
                        FileUtils.MergePartsToFile(getPartName, part.ParentItem.Parts, fileName);
                    }

                    part.ParentItem.IsoFileName = fileName;
                    if (part.ParentItem.Callback != null)
                    {
                        //Execute.BeginOnThreadPool(() =>
                        //{
                        //    part.ParentItem.Callback(part.ParentItem);
                        //    if (part.ParentItem.Callbacks != null)
                        //    {
                        //        foreach (var callback in part.ParentItem.Callbacks)
                        //        {
                        //            callback?.Invoke(part.ParentItem);
                        //        }
                        //    }
                        //});

                        part.ParentItem.Progress.Report(1.0);
                        part.ParentItem.Callback.TrySetResult(part.ParentItem);
                    }
                    else
                    {
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(part.ParentItem));
                    }

                    _statsService.IncrementReceivedItemsCount(_protoService.NetworkType, _dataType, 1);
                }
                else
                {
                    if (part.ParentItem.Callback != null)
                    {
                        part.ParentItem.Progress.Report(progress);
                    }
                    else
                    {
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new DownloadProgressChangedEventArgs(part.ParentItem, progress)));
                    }
                }
            }
        }

        public static string GetFileName(TLInputDocumentFileLocation fileLocation, string fileExtension)
        {
            var fileLocation54 = fileLocation as TLInputDocumentFileLocation;

            var id = fileLocation.Id;
            var accessHash = fileLocation.AccessHash;
            var version = fileLocation54.Version;

            if (version > 0)
            {
                return string.Format("document{0}_{1}{2}", id, version, fileExtension);
            }

            return string.Format("document{0}_{1}{2}", id, accessHash, fileExtension);
        }



        private TLUploadFileBase GetFile(int dcId, TLInputDocumentFileLocation location, int offset, int limit, out TLRPCError er, out bool isCanceled)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLUploadFileBase result = null;
            TLRPCError outError = null;
            var outIsCanceled = false;
            _protoService.GetFileAsync(dcId, location, offset, limit,
                callback =>
                {
                    result = callback;
                    manualResetEvent.Set();

                    if (callback is TLUploadFile file)
                    {
                        _statsService.IncrementReceivedBytesCount(_protoService.NetworkType, _dataType, 4 + 4 + file.Bytes.Length + 4);
                    }
                },
                error =>
                {
                    outError = error;

                    if (error.CodeEquals(TLErrorCode.INTERNAL)
                        || (error.CodeEquals(TLErrorCode.BAD_REQUEST) && (error.TypeEquals(TLErrorType.LOCATION_INVALID) || error.TypeEquals(TLErrorType.VOLUME_LOC_NOT_FOUND)))
                        || (error.CodeEquals(TLErrorCode.NOT_FOUND) && error.ErrorMessage != null && error.ErrorMessage.StartsWith("Incorrect dhGen")))
                    {
                        outIsCanceled = true;

                        manualResetEvent.Set();
                        return;
                    }

                    int delay;
                    lock (_randomRoot)
                    {
                        delay = _random.Next(1000, 3000);
                    }

                    Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
                });

            manualResetEvent.WaitOne(20 * 1000);
            er = outError;
            isCanceled = outIsCanceled;

            return result;
        }

        private TLUploadFileBase GetCdnFile(TLUploadFileCdnRedirect redirect, int dcId, TLInputDocumentFileLocation location, int offset, int limit, out TLRPCError er, out bool isCanceled)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLUploadFileBase result = null;
            TLRPCError outError = null;
            var outIsCanceled = false;

            _protoService.GetCdnFileAsync(redirect.DCId, redirect.FileToken, offset, limit, callback =>
            {
                if (callback is TLUploadCdnFile file)
                {
                    result = new TLUploadFile { Bytes = file.Bytes };
                    manualResetEvent.Set();

                    _statsService.IncrementReceivedBytesCount(_protoService.NetworkType, _dataType, file.Bytes.Length + 4);
                }
                else if (callback is TLUploadCdnFileReuploadNeeded reupload)
                {
                    result = ReuploadFile(redirect, reupload.RequestToken, dcId, location, offset, limit, out outError, out outIsCanceled);
                    while (result == null)
                    {
                        result = ReuploadFile(redirect, reupload.RequestToken, dcId, location, offset, limit, out outError, out outIsCanceled);
                        if (outIsCanceled)
                        {
                            break;
                        }
                    }

                    manualResetEvent.Set();
                }
            },
            error =>
            {
                outError = error;

                if (error.CodeEquals(TLErrorCode.INTERNAL)
                    || (error.CodeEquals(TLErrorCode.BAD_REQUEST) && (error.TypeEquals(TLErrorType.LOCATION_INVALID) || error.TypeEquals(TLErrorType.VOLUME_LOC_NOT_FOUND)))
                    || (error.CodeEquals(TLErrorCode.NOT_FOUND) && error.ErrorMessage != null && error.ErrorMessage.StartsWith("Incorrect dhGen")))
                {
                    outIsCanceled = true;

                    manualResetEvent.Set();
                    return;
                }

                int delay;
                lock (_randomRoot)
                {
                    delay = _random.Next(1000, 3000);
                }

                Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
            });

            manualResetEvent.WaitOne(20 * 1000);
            er = outError;
            isCanceled = outIsCanceled;

            return result;
        }

        private TLUploadFileBase ReuploadFile(TLUploadFileCdnRedirect redirect, byte[] requestToken, int dcId, TLInputDocumentFileLocation location, int offset, int limit, out TLRPCError er, out bool isCanceled)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLUploadFileBase result = null;
            TLRPCError outError = null;
            var outIsCanceled = false;

            _protoService.ReuploadCdnFileAsync(dcId, redirect.FileToken, requestToken, callback =>
            {
                if (callback != null)
                {
                    result = GetCdnFile(redirect, dcId, location, offset, limit, out outError, out outIsCanceled);
                    while (result == null)
                    {
                        result = GetCdnFile(redirect, dcId, location, offset, limit, out outError, out outIsCanceled);
                        if (outIsCanceled)
                        {
                            break;
                        }
                    }

                    manualResetEvent.Set();
                }
            },
            error =>
            {
                outError = error;

                if (error.CodeEquals(TLErrorCode.INTERNAL)
                    || (error.CodeEquals(TLErrorCode.BAD_REQUEST) && (error.TypeEquals(TLErrorType.LOCATION_INVALID) || error.TypeEquals(TLErrorType.VOLUME_LOC_NOT_FOUND)))
                    || (error.CodeEquals(TLErrorCode.NOT_FOUND) && error.ErrorMessage != null && error.ErrorMessage.StartsWith("Incorrect dhGen")))
                {
                    outIsCanceled = true;

                    manualResetEvent.Set();
                    return;
                }

                int delay;
                lock (_randomRoot)
                {
                    delay = _random.Next(1000, 3000);
                }

                Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
            });

            manualResetEvent.WaitOne(20 * 1000);
            er = outError;
            isCanceled = outIsCanceled;

            return result;
        }



        public IAsyncOperationWithProgress<DownloadableItem, double> DownloadFileAsync(string originalFileName, int dcId, TLInputDocumentFileLocation fileLocation, int fileSize, IProgress<double> test = null)
        {
            return AsyncInfo.Run<DownloadableItem, double>((token, progress) =>
            {
                var tsc = new TaskCompletionSource<DownloadableItem>();
                //var boh = new TaskCompletionSource<string>();

                //FileLoader.Current.LoadFile(new TLDocument
                //{
                //    Id = fileLocation.Id,
                //    AccessHash = fileLocation.AccessHash,
                //    Version = fileLocation.Version,
                //    Size = fileSize,
                //    DCId = dcId,
                //    Attributes = new TLVector<TLDocumentAttributeBase>{
                //    new TLDocumentAttributeFilename { FileName = originalFileName }
                //}
                //}, false, false, boh);
                //var name = await boh.Task;

                //return new DownloadableItem { DestFileName = name };

                var downloadableItem = GetDownloadableItem(originalFileName, dcId, fileLocation, null, fileSize);
                downloadableItem.Callback = tsc;
                downloadableItem.Progress = test ?? progress;

                var downloadedCount = downloadableItem.Parts.Count(x => x.Status == PartStatus.Processed);
                var count = downloadableItem.Parts.Count;
                var isComplete = downloadedCount == count;

                if (isComplete)
                {
                    //var id = downloadableItem.InputDocumentLocation.Id;
                    //var accessHash = downloadableItem.InputDocumentLocation.AccessHash;
                    var fileExtension = Path.GetExtension(downloadableItem.FileName.ToString());
                    var fileName = GetFileName(downloadableItem.InputDocumentLocation, fileExtension); //string.Format("document{0}_{1}{2}", id, accessHash, fileExtension);
                    Func<DownloadablePart, string> getPartName = x => downloadableItem.InputDocumentLocation.GetPartFileName(x.Number); //string.Format("document{0}_{1}_{2}.dat", id, accessHash, x.Number);

                    FileUtils.MergePartsToFile(getPartName, downloadableItem.Parts, fileName);

                    downloadableItem.IsoFileName = fileName;
                    downloadableItem.Progress.Report(1.0);
                    downloadableItem.Callback.TrySetResult(downloadableItem);
                }
                else
                {
                    downloadableItem.Progress.Report(downloadedCount / (double)count);

                    lock (_itemsSyncRoot)
                    {
                        bool addFile = true;
                        foreach (var item in _items)
                        {
                            if (item.InputDocumentLocation.AccessHash == fileLocation.AccessHash &&
                                item.InputDocumentLocation.Id == fileLocation.Id)
                            {
                                downloadableItem.Callback = item.Callback;
                                downloadableItem.Progress = item.Progress;
                                addFile = false;

                                Debug.WriteLine("Already downloading document");

                                //item.SuppressMerge = true;

                                //item.
                                //if (item.Owner == owner)
                                //{
                                //    Execute.ShowDebugMessage("Cancel document=" + fileLocation.Id);
                                //    addFile = false;
                                //    break;
                                //}
                            }
                        }

                        if (addFile)
                        {
                            _items.Add(downloadableItem);
                        }
                    }

                    StartAwaitingWorkers();
                }

                return tsc.Task;
            });
        }

        public void DownloadFile(string originalFileName, int dcId, TLInputDocumentFileLocation fileLocation, TLObject owner, int fileSize)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var downloadableItem = GetDownloadableItem(originalFileName, dcId, fileLocation, owner, fileSize);

                var downloadedCount = downloadableItem.Parts.Count(x => x.Status == PartStatus.Processed);
                var count = downloadableItem.Parts.Count;
                var isComplete = downloadedCount == count;

                if (isComplete)
                {
                    //var id = downloadableItem.InputDocumentLocation.Id;
                    //var accessHash = downloadableItem.InputDocumentLocation.AccessHash;
                    var fileExtension = Path.GetExtension(downloadableItem.FileName.ToString());
                    var fileName = GetFileName(downloadableItem.InputDocumentLocation, fileExtension); //string.Format("document{0}_{1}{2}", id, accessHash, fileExtension);
                    Func<DownloadablePart, string> getPartName = x => downloadableItem.InputDocumentLocation.GetPartFileName(x.Number); //string.Format("document{0}_{1}_{2}.dat", id, accessHash, x.Number);

                    FileUtils.MergePartsToFile(getPartName, downloadableItem.Parts, fileName);

                    downloadableItem.IsoFileName = fileName;
                    _eventAggregator.Publish(downloadableItem);
                }
                else
                {
                    var progress = downloadedCount / (double)count;
                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new DownloadProgressChangedEventArgs(downloadableItem, progress)));

                    lock (_itemsSyncRoot)
                    {
                        bool addFile = true;
                        foreach (var item in _items)
                        {
                            if (item.InputDocumentLocation.AccessHash == fileLocation.AccessHash
                                && item.InputDocumentLocation.Id == fileLocation.Id)
                            {

                                //item.SuppressMerge = true;

                                //item.
                                if (item.Owner == owner)
                                {
                                    Execute.ShowDebugMessage("Cancel document=" + fileLocation.Id);
                                    addFile = false;
                                    break;
                                }
                            }
                        }

                        if (addFile)
                        {
                            _items.Add(downloadableItem);
                        }
                    }

                    StartAwaitingWorkers();
                }
            });
        }

        private void StartAwaitingWorkers()
        {
            var awaitingWorkers = _workers.Where(x => x.IsWaiting);

            foreach (var awaitingWorker in awaitingWorkers)
            {
                awaitingWorker.Start();
            }
        }

        private DownloadableItem GetDownloadableItem(string fileName, int dcId, TLInputDocumentFileLocation location, TLObject owner, int fileSize)
        {
            var item = new DownloadableItem
            {
                DCId = dcId,
                FileName = fileName,
                Owner = owner,
                InputDocumentLocation = location
            };
            item.Parts = GetItemParts(fileSize, item);

            return item;
        }

        private List<DownloadablePart> GetItemParts(int size, DownloadableItem item)
        {
            //var chunkSize = Constants.DownloadChunkSize;

            //var chunkSize = size > 1024 * 1024 ? 1024 * 128 : 1024 * 32;
            var chunkSize = 1024 * 128;
            var parts = new List<DownloadablePart>();
            var partsCount = size / chunkSize + (size % chunkSize > 0 ? 1 : 0);

            for (var i = 0; i < partsCount; i++)
            {
                var part = new DownloadablePart(item, i * chunkSize, chunkSize, i);
                var partName = item.InputDocumentLocation.GetPartFileName(part.Number); //string.Format("document{0}_{1}_{2}.dat", item.InputDocumentLocation.Id, item.InputDocumentLocation.AccessHash, part.Number);
                var partLength = FileUtils.GetLocalFileLength(partName);

                if (partLength >= 0)
                {
                    var isCompletePart = (part.Number + 1 == partsCount) || partLength == part.Limit;
                    part.Status = isCompletePart ? PartStatus.Processed : PartStatus.Ready;
                }

                parts.Add(part);
            }

            return parts;
        }

        public void Cancel(TLDocument document)
        {
            Execute.BeginOnThreadPool(() =>
            {
                lock (_itemsSyncRoot)
                {
                    //var items = _items.Where(x => x.Owner == owner);
                    var items = _items.Where(x => x.InputDocumentLocation.AccessHash == document.AccessHash && x.InputDocumentLocation.Id == document.Id);

                    foreach (var item in items)
                    {
                        item.IsCancelled = true;
                    }
                }
            });
        }
    }
}
