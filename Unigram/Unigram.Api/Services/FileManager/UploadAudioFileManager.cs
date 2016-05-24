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
    public class UploadAudioFileManager : IUploadAudioFileManager
    {
        private readonly object _itemsSyncRoot = new object();

        private readonly List<UploadableItem> _items = new List<UploadableItem>();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public UploadAudioFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
        {
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;


            var timer = Stopwatch.StartNew();
            for (int i = 0; i < Constants.VideoUploadersCount; i++)
            {
                var worker = new Worker(OnUploading, "audioUploader" + i);
                _workers.Add(worker);
            }

            TLUtils.WritePerformance("Start workers timer: " + timer.Elapsed);

        }

        private void OnUploading(object state)
        {
            UploadablePart part = null;
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
                            _eventAggregator.Publish(new UploadingCanceledEventArgs(item));
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

            if (part != null)
            {
                var bytes = FileUtils.ReadBytes(part.ParentItem.IsoFileName, part.Position, part.Count);
                part.SetBuffer(bytes);

                bool result = PutFile(part.ParentItem.FileId, part.FilePart, part.Bytes);
                while (!result)
                {
                    if (part.ParentItem.Canceled)
                    {
                        return;
                    }
                    result = PutFile(part.ParentItem.FileId, part.FilePart, part.Bytes);
                }

                part.ClearBuffer();


                // indicate progress
                // indicate complete
                bool isComplete = false;
                bool isCanceled;
                var progress = 0.0;
                lock (_itemsSyncRoot)
                {
                    part.Status = PartStatus.Processed;
                    isCanceled = part.ParentItem.Canceled;
                    if (!isCanceled)
                    {
                        isComplete = part.ParentItem.Parts.All(x => x.Status == PartStatus.Processed);
                        if (!isComplete)
                        {
                            double uploadedCount = part.ParentItem.Parts.Count(x => x.Status == PartStatus.Processed);
                            double totalCount = part.ParentItem.Parts.Count;
                            progress = uploadedCount / totalCount;
                        }
                        else
                        {
                            _items.Remove(part.ParentItem);
                        }
                    }
                }

                if (!isCanceled)
                {
                    if (isComplete)
                    {
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(part.ParentItem));
                    }
                    else
                    {
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new UploadProgressChangedEventArgs(part.ParentItem, progress)));
                    }
                }
            }
            else
            {
                var currentWorker = (Worker)state;
                currentWorker.Stop();
            }
        }

        private bool PutFile(TLLong fileId, TLInt filePart, byte[] bytes)
        {
            var manualResetEvent = new ManualResetEvent(false);
            var result = false;

            _mtProtoService.SaveFilePartAsync(fileId, filePart, TLString.FromBigEndianData(bytes),
                savingResult =>
                {
                    result = true;
                    manualResetEvent.Set();
                },
                error =>
                {

                    Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(1000), () => manualResetEvent.Set());
                });

            manualResetEvent.WaitOne();
            return result;
        }

        public void UploadFile(TLLong fileId, TLObject owner, string fileName)
        {
            long fileLength = FileUtils.GetLocalFileLength(fileName);
            if (fileLength <= 0) return;

            var item = GetUploadableItem(fileId, owner, fileName, fileLength);

            var uploadedCount = item.Parts.Count(x => x.Status == PartStatus.Processed);
            var count = item.Parts.Count;
            var isComplete = uploadedCount == count;

            if (isComplete)
            {
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(item));
            }
            else
            {
                lock (_itemsSyncRoot)
                {
                    _items.Add(item);
                }

                StartAwaitingWorkers();
            }
        }

        public void UploadFile(TLLong fileId, TLObject owner, string fileName, IList<UploadablePart> parts)
        {
            long fileLength = FileUtils.GetLocalFileLength(fileName);
            if (fileLength <= 0) return;

            var item = GetUploadableItem(fileId, owner, fileName, fileLength, parts);

            var uploadedCount = item.Parts.Count(x => x.Status == PartStatus.Processed);
            var count = item.Parts.Count;
            var isComplete = uploadedCount == count;

            if (isComplete)
            {
                Execute.BeginOnThreadPool(() => _eventAggregator.Publish(item));
            }
            else
            {
                lock (_itemsSyncRoot)
                {
                    _items.Add(item);
                }

                StartAwaitingWorkers();
            }
        }

        private UploadableItem GetUploadableItem(TLLong fileId, TLObject owner, string isoFileName, long isoFileLength, IList<UploadablePart> parts)
        {
            var item = new UploadableItem(fileId, owner, isoFileName, isoFileLength);
            item.Parts = GetItemParts(item, isoFileLength, parts);
            return item;
        }

        private UploadableItem GetUploadableItem(TLLong fileId, TLObject owner, string isoFileName, long isoFileLength)
        {
            var item = new UploadableItem(fileId, owner, isoFileName, isoFileLength);
            item.Parts = GetItemParts(item, isoFileLength);
            return item;
        }

        private List<UploadablePart> GetItemParts(UploadableItem item, long isoFileLength)
        {
            const int partSize = 32 * 1024; // 32 Kb: кратно 1 Kb и нацело делит 1024 Kb
            var parts = new List<UploadablePart>();
            var partsCount = item.IsoFileLength / partSize + (item.IsoFileLength % partSize > 0 ? 1 : 0);
            for (var i = 0; i < partsCount; i++)
            {
                var part = new UploadablePart(item, new TLInt(i), i * partSize, Math.Min(partSize, isoFileLength - i * partSize));
                parts.Add(part);
            }

            return parts;
        }

        private List<UploadablePart> GetItemParts(UploadableItem item, long isoFileLength, IList<UploadablePart> uploadedParts)
        {
            const int partSize = 32 * 1024;
            foreach (var uploadedPart in uploadedParts)
            {
                uploadedPart.SetParentItem(item);
            }
            var parts = new List<UploadablePart>(uploadedParts);

            var uploadedLength = uploadedParts.Sum(x => x.Count);

            var uploadingLength = item.IsoFileLength - uploadedLength;
            if (uploadingLength == 0)
            {
                return parts;
            }
            var partsCount = uploadingLength / partSize + 1;
            for (var i = 0; i < partsCount; i++)
            {
                var partId = i + uploadedParts.Count;
                var part = new UploadablePart(item, new TLInt(partId), uploadedLength + i * partSize, Math.Min(partSize, isoFileLength - (uploadedLength + i * partSize)));
                parts.Add(part);
            }

            return parts;
        }

        private void StartAwaitingWorkers()
        {
            var awaitingWorkers = _workers.Where(x => x.IsWaiting);

            foreach (var awaitingWorker in awaitingWorkers)
            {
                awaitingWorker.Start();
            }
        }

        public void CancelUploadFile(TLLong fileId)
        {
            lock (_itemsSyncRoot)
            {
                var item = _items.FirstOrDefault(x => x.FileId.Value == fileId.Value);

                if (item != null)
                {
                    item.Canceled = true;
                    //_items.Remove(item);
                }
            }
        }
    }
}