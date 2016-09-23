﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public class DownloadVideoFileManager : IDownloadVideoFileManager
    {
        private readonly object _randomRoot = new object();

        private readonly Random _random = new Random();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly object _itemsSyncRoot = new object();

        private readonly List<DownloadableItem> _items = new List<DownloadableItem>();

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public DownloadVideoFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
        {
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;


            var timer = Stopwatch.StartNew();
            for (int i = 0; i < Constants.BigFileDownloadersCount; i++)
            {
                var worker = new Worker(OnDownloading, "videoDownloader"+i);
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

            var partName = string.Format("video{0}_{1}_{2}.dat", part.ParentItem.InputVideoLocation.Id, part.ParentItem.InputVideoLocation.AccessHash, part.Number);
            part.File = GetFile(part.ParentItem.DCId, (TLInputFileLocationBase)part.ParentItem.InputVideoLocation, part.Offset, part.Limit);
            while (part.File == null)
            {
                part.File = GetFile(part.ParentItem.DCId, (TLInputFileLocationBase)part.ParentItem.InputVideoLocation, part.Offset, part.Limit);
            }

            // indicate progress
            // indicate complete
            bool isComplete;
            bool isCanceled;
            var progress = 0.0;
            lock (_itemsSyncRoot)
            {
                part.Status = PartStatus.Processed;

                FileUtils.CheckMissingPart(_itemsSyncRoot, part, partName);

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
                    _items.Remove(part.ParentItem);    
                }
            }

            if (!isCanceled)
            {
                if (isComplete)
                {
                    var id = part.ParentItem.InputVideoLocation.Id;
                    var accessHash = part.ParentItem.InputVideoLocation.AccessHash;
                    var fileName = string.Format("video{0}_{1}.mp4", id, accessHash);
                    var getPartName = new Func<DownloadablePart, string>( x => string.Format("video{0}_{1}_{2}.dat", id, accessHash, x.Number));  
         
                    FileUtils.MergePartsToFile(getPartName, part.ParentItem.Parts, fileName);

                    part.ParentItem.IsoFileName = fileName;
                    _eventAggregator.Publish(part.ParentItem);
                }
                else
                {
                    _eventAggregator.Publish(new ProgressChangedEventArgs(part.ParentItem, progress));
                }
            }
        }

        private TLUploadFile GetFile(int dcId, TLInputFileLocationBase location, int offset, int limit)
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

        //public Task<DownloadableItem> DownloadFileAsync(int dcId, TLInputDocumentFileLocation fileLocation, TLObject owner, int fileSize)
        //{
        //    var tsc = new TaskCompletionSource<DownloadableItem>();
        //    DownloadFileAsync(dcId, fileLocation, owner, fileSize, (item) => tsc.TrySetResult(item));
        //    return tsc.Task;
        //}

        public void DownloadFileAsync(int dcId, TLInputDocumentFileLocation fileLocation, TLObject owner, int fileSize, Action<double> callback)
        {
            Execute.BeginOnThreadPool(() =>
            {
                var downloadableItem = GetDownloadableItem(dcId, fileLocation, owner, fileSize);

                var downloadedCount = downloadableItem.Parts.Count(x => x.Status == PartStatus.Processed);
                var count = downloadableItem.Parts.Count;
                var isComplete = downloadedCount == count;

                if (isComplete)
                {
                    var id = downloadableItem.InputVideoLocation.Id;
                    var accessHash = downloadableItem.InputVideoLocation.AccessHash;
                    var fileName = string.Format("video{0}_{1}.mp4", id, accessHash);
                    var getPartName = new Func<DownloadablePart, string>(x => string.Format("video{0}_{1}_{2}.dat", id, accessHash, x.Number));

                    FileUtils.MergePartsToFile(getPartName, downloadableItem.Parts, fileName);

                    downloadableItem.IsoFileName = fileName;
                    _eventAggregator.Publish(downloadableItem);
                }
                else
                {
                    var progress = downloadedCount / (double)count;
                    callback.SafeInvoke(progress);

                    lock (_itemsSyncRoot)
                    {
                        bool addFile = true;
                        foreach (var item in _items)
                        {
                            if (item.InputVideoLocation.AccessHash == fileLocation.AccessHash
                                && item.InputVideoLocation.Id == fileLocation.Id
                                && item.Owner == owner)
                            {
                                addFile = false;
                                break;
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

        private DownloadableItem GetDownloadableItem(int dcId, TLInputDocumentFileLocation location, TLObject owner, int fileSize)
        {
            var item = new DownloadableItem
            {
                Owner = owner,
                DCId = dcId,
                InputVideoLocation = location
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
                var partName = string.Format("video{0}_{1}_{2}.dat", item.InputVideoLocation.Id, item.InputVideoLocation.AccessHash, part.Number);

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
            Helpers.Execute.BeginOnThreadPool(() =>
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
