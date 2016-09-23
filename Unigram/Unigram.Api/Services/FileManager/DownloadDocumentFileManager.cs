﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public class DownloadDocumentFileManager : IDownloadDocumentFileManager
    {
        private readonly object _randomRoot = new object();

        private readonly Random _random = new Random();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly object _itemsSyncRoot = new object();

        private readonly List<DownloadableItem> _items = new List<DownloadableItem>();

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public DownloadDocumentFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
        {
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;


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
                    if (item.Canceled)
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
            
            part.File = GetFile(part.ParentItem.DCId, part.ParentItem.InputDocumentLocation, part.Offset, part.Limit);
            while (part.File == null)
            {
                part.File = GetFile(part.ParentItem.DCId, part.ParentItem.InputDocumentLocation, part.Offset, part.Limit);
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

                isCanceled = part.ParentItem.Canceled;

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
                        Execute.BeginOnThreadPool(() =>
                        {
                            part.ParentItem.Callback(part.ParentItem);
                            if (part.ParentItem.Callbacks != null)
                            {
                                foreach (var callback in part.ParentItem.Callbacks)
                                {
                                    callback.SafeInvoke(part.ParentItem);
                                }
                            }
                        });
                    }
                    else
                    {
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(part.ParentItem));
                    }
                }
                else
                {
                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new ProgressChangedEventArgs(part.ParentItem, progress)));
                }
            }
        }

        private static string GetFileName(TLInputDocumentFileLocation fileLocation, string fileExtension)
        {
            var fileLocation54 = fileLocation as TLInputDocumentFileLocation;

            var id = fileLocation.Id;
            var accessHash = fileLocation.AccessHash;
            var version = fileLocation54.Version;

            if (version != null && version > 0)
            {
                return string.Format("document{0}_{1}{2}", id, version, fileExtension);
            }

            return string.Format("document{0}_{1}{2}", id, accessHash, fileExtension);
        }

        private TLUploadFile GetFile(int dcId, TLInputDocumentFileLocation location, int offset, int limit)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLUploadFile result = null;

            _mtProtoService.GetFileCallback(dcId, location, offset, limit,
                file =>
                {
                    result = file;
                    manualResetEvent.Set();
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

            manualResetEvent.WaitOne();
            return result;
        }

        public Task<DownloadableItem> DownloadFileAsync(string originalFileName, int dcId, TLInputDocumentFileLocation fileLocation, TLObject owner, int fileSize, Action<double> startCallback)
        {
            var tsc = new TaskCompletionSource<DownloadableItem>();
            DownloadFileAsync(originalFileName, dcId, fileLocation, owner, fileSize, startCallback, (item) => tsc.TrySetResult(item));
            return tsc.Task;
        }

        public void DownloadFileAsync(string originalFileName, int dcId, TLInputDocumentFileLocation fileLocation, TLObject owner, int fileSize, Action<double> startCallback, Action<DownloadableItem> callback = null)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var downloadableItem = GetDownloadableItem(originalFileName, dcId, fileLocation, owner, fileSize, callback);

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
                    startCallback.SafeInvoke(progress);

                    lock (_itemsSyncRoot)
                    {
                        bool addFile = true;
                        foreach (var item in _items)
                        {
                            if (item.InputDocumentLocation.AccessHash == fileLocation.AccessHash
                                && item.InputDocumentLocation.Id == fileLocation.Id)
                            {

                                //item.SuppressMerge = true;

                                if (callback != null)
                                {
                                    if (item.Callbacks == null)
                                    {
                                        item.Callbacks = new List<Action<DownloadableItem>>();
                                    }
                                    item.Callbacks.Add(callback);
                                    addFile = false;
                                    break;
                                }
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

        private DownloadableItem GetDownloadableItem(string fileName, int dcId, TLInputDocumentFileLocation location, TLObject owner, int fileSize, Action<DownloadableItem> callback)
        {
            var item = new DownloadableItem
            {
                DCId = dcId,
                FileName = fileName,
                Owner = owner,
                InputDocumentLocation = location,
                Callback = callback
            };
            item.Parts = GetItemParts(fileSize, item);

            return item;
        }

        private List<DownloadablePart> GetItemParts(int size, DownloadableItem item)
        {
            var chunkSize = Constants.DownloadedChunkSize;
            var parts = new List<DownloadablePart>();
            var partsCount = size / chunkSize + (size % chunkSize > 0 ? 1 : 0);

            for (var i = 0; i < partsCount; i++)
            {
                var part = new DownloadablePart(item, i * chunkSize, size == 0 ? 0 : chunkSize, i);
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

        public void CancelDownloadFileAsync(TLObject owner)
        {
            Execute.BeginOnThreadPool(() =>
            {
                lock (_itemsSyncRoot)
                {
                    var items = _items.Where(x => x.Owner == owner);

                    foreach (var item in items)
                    {
                        item.Canceled = true;
                    }
                }
            });
        }
    }
}
