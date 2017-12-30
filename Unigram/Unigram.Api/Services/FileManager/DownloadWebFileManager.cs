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
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;
using Telegram.Api.TL.Upload;
using Windows.Foundation;

namespace Telegram.Api.Services.FileManager
{
    public interface IDownloadWebManager
    {
        IAsyncOperationWithProgress<DownloadableItem, double> DownloadFileAsync(string fileName, int dcId, TLInputWebFileLocation file, int fileSize, IProgress<double> test = null);

        void DownloadFile(string fileName, int dcId, TLInputWebFileLocation file, TLObject owner, int fileSize);

        void CancelDownloadFile(TLWebDocument document);
    }

    public interface IDownloadWebFileManager : IDownloadWebManager
    {
    }

    public class DownloadWebFileManager : IDownloadWebFileManager
    {
        private readonly object _randomRoot = new object();

        private readonly Random _random = new Random();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly object _itemsSyncRoot = new object();

        private readonly List<DownloadableItem> _items = new List<DownloadableItem>();

        private readonly ITelegramEventAggregator _eventAggregator;
        private readonly IMTProtoService _protoService;
        private readonly IStatsService _statsService;
        private readonly DataType _dataType = DataType.Files;

        public DownloadWebFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService, IStatsService statsService)
        {
            _eventAggregator = eventAggregator;
            _protoService = mtProtoService;
            _statsService = statsService;

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

            var partName = part.ParentItem.InputWebFileLocation.GetPartFileName(part.Number);//string.Format("document{0}_{1}_{2}.dat", part.ParentItem.InputDocumentLocation.Id, part.ParentItem.InputDocumentLocation.AccessHash, part.Number);
            part.WebFile = GetFile(part.ParentItem.DCId, part.ParentItem.InputWebFileLocation, part.Offset, part.Limit);

            while (part.WebFile == null)
            {
                part.WebFile = GetFile(part.ParentItem.DCId, part.ParentItem.InputWebFileLocation, part.Offset, part.Limit);
            }


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
                    var fileName = GetFileName(part.ParentItem.InputWebFileLocation, fileExtension);
                    Func<DownloadablePart, string> getPartName = x => part.ParentItem.InputWebFileLocation.GetPartFileName(x.Number);

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

        public static string GetFileName(TLInputWebFileLocation fileLocation, string fileExtension)
        {
            return BitConverter.ToString(Utils.ComputeMD5(fileLocation.Url)).Replace("-", "") + fileExtension;
        }

        private TLUploadWebFile GetFile(int dcId, TLInputWebFileLocation location, int offset, int limit)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLUploadWebFile result = null;

            _protoService.GetWebFileAsync(dcId, location, offset, limit,
                file =>
                {
                    result = file;
                    manualResetEvent.Set();

                    _statsService.IncrementReceivedBytesCount(_protoService.NetworkType, _dataType, 4 + 4 + file.Bytes.Length + 4);
                },
                error =>
                {
                    int delay;
                    lock (_randomRoot)
                    {
                        delay = _random.Next(1000, 3000);
                    }

                    Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
                });

            manualResetEvent.WaitOne(20 * 1000);
            return result;
        }

        public IAsyncOperationWithProgress<DownloadableItem, double> DownloadFileAsync(string originalFileName, int dcId, TLInputWebFileLocation fileLocation, int fileSize, IProgress<double> test = null)
        {
            return AsyncInfo.Run<DownloadableItem, double>((token, progress) =>
            {
                var tsc = new TaskCompletionSource<DownloadableItem>();

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
                    var fileName = GetFileName(downloadableItem.InputWebFileLocation, fileExtension); //string.Format("document{0}_{1}{2}", id, accessHash, fileExtension);
                    Func<DownloadablePart, string> getPartName = x => downloadableItem.InputWebFileLocation.GetPartFileName(x.Number); //string.Format("document{0}_{1}_{2}.dat", id, accessHash, x.Number);

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
                            if (item.InputWebFileLocation.AccessHash == fileLocation.AccessHash &&
                                item.InputWebFileLocation.Url == fileLocation.Url)
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

        public void DownloadFile(string originalFileName, int dcId, TLInputWebFileLocation fileLocation, TLObject owner, int fileSize)
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
                    var fileName = GetFileName(downloadableItem.InputWebFileLocation, fileExtension); //string.Format("document{0}_{1}{2}", id, accessHash, fileExtension);
                    Func<DownloadablePart, string> getPartName = x => downloadableItem.InputWebFileLocation.GetPartFileName(x.Number); //string.Format("document{0}_{1}_{2}.dat", id, accessHash, x.Number);

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
                            if (item.InputWebFileLocation.AccessHash == fileLocation.AccessHash
                                && item.InputWebFileLocation.Url == fileLocation.Url)
                            {

                                //item.SuppressMerge = true;

                                //item.
                                if (item.Owner == owner)
                                {
                                    Execute.ShowDebugMessage("Cancel document=" + fileLocation.Url);
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

        private DownloadableItem GetDownloadableItem(string fileName, int dcId, TLInputWebFileLocation location, TLObject owner, int fileSize)
        {
            var item = new DownloadableItem
            {
                DCId = dcId,
                FileName = fileName,
                Owner = owner,
                InputWebFileLocation = location
            };
            item.Parts = GetItemParts(fileSize, item);

            return item;
        }

        private List<DownloadablePart> GetItemParts(int size, DownloadableItem item)
        {
            var chunkSize = Constants.DocumentDownloadChunkSize;
            var parts = new List<DownloadablePart>();
            var partsCount = size / chunkSize + (size % chunkSize > 0 ? 1 : 0);

            for (var i = 0; i < partsCount; i++)
            {
                var part = new DownloadablePart(item, i * chunkSize, size == 0 ? 0 : chunkSize, i);
                var partName = item.InputWebFileLocation.GetPartFileName(part.Number); //string.Format("document{0}_{1}_{2}.dat", item.InputDocumentLocation.Id, item.InputDocumentLocation.AccessHash, part.Number);
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

        public void CancelDownloadFile(TLWebDocument document)
        {
            Execute.BeginOnThreadPool(() =>
            {
                lock (_itemsSyncRoot)
                {
                    //var items = _items.Where(x => x.Owner == owner);
                    var items = _items.Where(x => x.InputWebFileLocation.AccessHash == document.AccessHash && x.InputWebFileLocation.Url == document.Url);

                    foreach (var item in items)
                    {
                        item.IsCancelled = true;
                    }
                }
            });
        }
    }
}
