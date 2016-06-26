﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IAudioFileManager
    {
        void DownloadFile(int dcId, TLInputAudioFileLocation file, TLObject owner, int fileSize);
        void CancelDownloadFile(TLObject owner);
    }

    public class AudioFileManager : IAudioFileManager
    {
        private readonly object _randomRoot = new object();

        private readonly Random _random = new Random();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly object _itemsSyncRoot = new object();

        private readonly List<DownloadableItem> _items = new List<DownloadableItem>();

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public AudioFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
        {
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;


            var timer = Stopwatch.StartNew();
            for (int i = 0; i < Constants.AudioDownloadersCount; i++)
            {
                var worker = new Worker(OnDownloading, "audioDownloader" + i);
                _workers.Add(worker);
            }

            TLUtils.WritePerformance("Start workers timer: " + timer.Elapsed);

        }

        private async void OnDownloading(object state)
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

            var partName = string.Format("audio{0}_{1}_{2}.dat", part.ParentItem.InputAudioLocation.Id, part.ParentItem.InputAudioLocation.AccessHash, part.Number);
            var isLastPart = part.Number + 1 == part.ParentItem.Parts.Count;
            var partLength = FileUtils.GetLocalFileLength(partName);
            var partExists = partLength >= 0;
            var isCorrectPartLength = isLastPart || partLength == part.Limit;

            if (!partExists || !isCorrectPartLength)
            {
                part.File = await GetFile(part.ParentItem.DCId, part.ParentItem.InputAudioLocation, part.Offset, part.Limit);
                while (part.File == null)
                {
                    part.File = await GetFile(part.ParentItem.DCId, part.ParentItem.InputAudioLocation, part.Offset, part.Limit);
                }

                part.Status = PartStatus.Processed;

                FileUtils.CheckMissingPart(_itemsSyncRoot, part, partName);
            }

            // indicate progress
            // indicate complete
            bool isComplete;
            bool isCanceled;
            var progress = 0.0;
            lock (_itemsSyncRoot)
            {
                part.Status = PartStatus.Processed;
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
                    var id = part.ParentItem.InputAudioLocation.Id;
                    var accessHash = part.ParentItem.InputAudioLocation.AccessHash;
                    var fileName = string.Format("audio{0}_{1}.mp3", id, accessHash);
                    var getPartName = new Func<DownloadablePart, string>(x => string.Format("audio{0}_{1}_{2}.dat", id, accessHash, x.Number));

                    FileUtils.MergePartsToFile(getPartName, part.ParentItem.Parts, fileName);

                    part.ParentItem.IsoFileName = fileName;
                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(part.ParentItem));
                }
                else
                {
                    Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new ProgressChangedEventArgs(part.ParentItem, progress)));
                }
            }
        }

        private async Task<TLUploadFile> GetFile(int dcId, TLInputFileLocationBase location, int offset, int limit)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLUploadFile result = null;

            var request = await _mtProtoService.GetFileAsync(dcId, location, offset, limit);
            if (request.Error == null)
            {
                result = request.Value;
                manualResetEvent.Set();
            }
            else
            {
                int delay;
                lock (_randomRoot)
                {
                    delay = _random.Next(1000, 3000);
                }

                Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
            }

            manualResetEvent.WaitOne();
            return result;
        }

        public void DownloadFile(int dcId, TLInputAudioFileLocation fileLocation, TLObject owner, int fileSize)
        {
            var downloadableItem = GetDownloadableItem(dcId, fileLocation, owner, fileSize);

            var downloadedCount = downloadableItem.Parts.Count(x => x.Status == PartStatus.Processed);
            var count = downloadableItem.Parts.Count;
            var isComplete = downloadedCount == count;

            if (isComplete)
            {
                var id = downloadableItem.InputAudioLocation.Id;
                var accessHash = downloadableItem.InputAudioLocation.AccessHash;
                var fileName = string.Format("audio{0}_{1}.mp3", id, accessHash);
                var getPartName = new Func<DownloadablePart, string>(x => string.Format("audio{0}_{1}_{2}.dat", id, accessHash, x.Number));

                FileUtils.MergePartsToFile(getPartName, downloadableItem.Parts, fileName);

                downloadableItem.IsoFileName = fileName;
                _eventAggregator.Publish(downloadableItem);
            }
            else
            {
                lock (_itemsSyncRoot)
                {
                    bool addFile = true;
                    foreach (var item in _items)
                    {
                        if (item.InputAudioLocation.AccessHash.Value == fileLocation.AccessHash.Value
                            && item.InputAudioLocation.Id.Value == fileLocation.Id.Value
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
        }

        private void StartAwaitingWorkers()
        {
            var awaitingWorkers = _workers.Where(x => x.IsWaiting);

            foreach (var awaitingWorker in awaitingWorkers)
            {
                awaitingWorker.Start();
            }
        }

        private DownloadableItem GetDownloadableItem(int dcId, TLInputAudioFileLocation location, TLObject owner, int fileSize)
        {
            var item = new DownloadableItem
            {
                Owner = owner,
                DCId = dcId,
                InputAudioLocation = location
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
                parts.Add(part);
            }

            return parts;
        }

        public void CancelDownloadFile(TLObject owner)
        {
            lock (_itemsSyncRoot)
            {
                var items = _items.Where(x => x.Owner == owner);

                foreach (var item in items)
                {
                    item.Canceled = true;
                }
            }
        }
    }
}
