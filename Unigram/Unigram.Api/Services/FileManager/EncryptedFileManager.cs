using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.TL;


namespace Telegram.Api.Services.FileManager
{
    public class EncryptedFileManager : IEncryptedFileManager
    {
        private readonly object _randomRoot = new object();

        private readonly Random _random = new Random();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly object _itemsSyncRoot = new object();

        private readonly List<DownloadableItem> _items = new List<DownloadableItem>();

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public EncryptedFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
        {
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;


            var timer = Stopwatch.StartNew();
            for (int i = 0; i < Constants.VideoDownloadersCount; i++)
            {
                var worker = new Worker(OnDownloading, "encryptedDownloader"+i);
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

            var partName = part.ParentItem.InputEncryptedFileLocation.GetPartFileName(part.Number);
            var partLength = FileUtils.GetLocalFileLength(partName);
            var partExists = partLength >= 0;
            var isLastPart = part.Number + 1 == part.ParentItem.Parts.Count;
            var isCorrectPartLength = isLastPart || partLength == part.Limit.Value;

            if (!partExists || !isCorrectPartLength)
            {
                part.File = GetFile(part.ParentItem.DCId, part.ParentItem.InputEncryptedFileLocation, part.Offset, part.Limit);
                while (part.File == null)
                {
                    part.File = GetFile(part.ParentItem.DCId, part.ParentItem.InputEncryptedFileLocation, part.Offset, part.Limit);
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
                    var fileName = part.ParentItem.InputEncryptedFileLocation.GetFileName();
                    var getPartFileName = new Func<DownloadablePart, string>(p => p.ParentItem.InputEncryptedFileLocation.GetPartFileName(p.Number));

                    FileUtils.MergePartsToFile(getPartFileName, part.ParentItem.Parts, fileName);

                    part.ParentItem.IsoFileName = fileName;
                    _eventAggregator.Publish(part.ParentItem);
                }
                else
                {
                    _eventAggregator.Publish(new ProgressChangedEventArgs(part.ParentItem, progress));
                }
            }
        }

        private TLFile GetFile(TLInt dcId, TLInputFileLocationBase location, TLInt offset, TLInt limit)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLFile result = null;

            _mtProtoService.GetFileAsync(dcId, location, offset, limit,
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

        public void DownloadFile(TLEncryptedFile file, TLObject owner)
        {
            var inputFile = new TLInputEncryptedFileLocation { Id = file.Id, AccessHash = file.AccessHash };
            var downloadableItem = GetDownloadableItem(file.DCId, inputFile, owner, file.Size);

            lock (_itemsSyncRoot)
            {
                bool addFile = true;
                foreach (var item in _items)
                {
                    if (item.InputEncryptedFileLocation.LocationEquals(inputFile))
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

        private void StartAwaitingWorkers()
        {
            var awaitingWorkers = _workers.Where(x => x.IsWaiting);

            foreach (var awaitingWorker in awaitingWorkers)
            {
                awaitingWorker.Start();
            }
        }

        private DownloadableItem GetDownloadableItem(TLInt dcId, TLInputFileLocationBase location, TLObject owner, TLInt fileSize)
        {
            var item = new DownloadableItem
            {
                Owner = owner,
                DCId = dcId,
                InputEncryptedFileLocation = location
            };
            item.Parts = GetItemParts(fileSize, item);

            return item;
        }

        private List<DownloadablePart> GetItemParts(TLInt size, DownloadableItem item)
        {
            var chunkSize = Constants.DownloadedChunkSize;
            var parts = new List<DownloadablePart>();
            var partsCount = size.Value / chunkSize + (size.Value % chunkSize > 0 ? 1 : 0);
            for (var i = 0; i < partsCount; i++)
            {
                var part = new DownloadablePart(item, new TLInt(i * chunkSize), size.Value == 0? new TLInt(0) : new TLInt(chunkSize), i);
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
