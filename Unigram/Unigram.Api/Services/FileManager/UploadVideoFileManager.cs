using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Telegram.Api.Helpers;
#if WP8
using Windows.Storage;
#endif
using Telegram.Api.Aggregator;
using Telegram.Api.TL;


namespace Telegram.Api.Services.FileManager
{
    public class UploadVideoFileManager : IUploadVideoFileManager
    {
        private readonly object _itemsSyncRoot = new object();

        private readonly List<UploadableItem> _items = new List<UploadableItem>();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber); 

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public UploadVideoFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
        {
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;


            var timer = Stopwatch.StartNew();
            for (int i = 0; i < Constants.VideoUploadersCount; i++)
            {
                var worker = new Worker(OnUploading, "videoUploader"+i);
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
                var bytes = part.Bytes;

                var fileName = part.ParentItem.IsoFileName;
                if (!string.IsNullOrEmpty(fileName))
                {
                    bytes = FileUtils.ReadBytes(fileName, part.Position, part.Count);
                }
#if WP8
                if (bytes == null)
                {
                    var file = part.ParentItem.File;
                    Tuple<bool, byte[]> result = null;
                    if (file != null)
                    {
                        var task = FileUtils.FillBuffer(file, part);
                        task.Wait();
                        result = task.Result;
                    }

                    if (result == null)
                    {
                        part.Status = PartStatus.Ready;
                        return;
                    }

                    if (!result.Item1)
                    {
                        part.ParentItem.FileNotFound = true;
                        part.Status = PartStatus.Processed;
                        FileUtils.NotifyProgress(_itemsSyncRoot, _items, part, _eventAggregator);
                        return;
                    }

                    bytes = result.Item2;
                }
#endif
                if (part.ParentItem.IsSmallFile)
                {
                    var result = PutFile(part.ParentItem.FileId, part.FilePart, bytes);
                    while (!result)
                    {
                        if (part.ParentItem.Canceled)
                        {
                            return;
                        }
                        result = PutFile(part.ParentItem.FileId, part.FilePart, bytes);
                    }
                }
                else
                {
                    var result = PutBigFile(part.ParentItem.FileId, part.FilePart, new TLInt(part.ParentItem.Parts.Count), bytes);
                    while (!result)
                    {
                        if (part.ParentItem.Canceled)
                        {
                            return;
                        }
                        result = PutBigFile(part.ParentItem.FileId, part.FilePart, new TLInt(part.ParentItem.Parts.Count), bytes);
                    }
                }
                part.ClearBuffer();

                FileUtils.NotifyProgress(_itemsSyncRoot, _items, part, _eventAggregator);
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

        private bool PutBigFile(TLLong fileId, TLInt filePart, TLInt fileTotalParts, byte[] bytes)
        {
            var manualResetEvent = new ManualResetEvent(false);
            var result = false;

            _mtProtoService.SaveBigFilePartAsync(fileId, filePart, fileTotalParts, TLString.FromBigEndianData(bytes),
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
            var fileLength = FileUtils.GetLocalFileLength(fileName);
            if (fileLength <= 0) return;

            var item = GetUploadableItem(fileId, owner, fileName, fileLength);

            var uploadedCount = item.Parts.Count(x => x.Status == PartStatus.Processed);
            var count = item.Parts.Count;
            var isComplete = uploadedCount == count;

            if (isComplete)
            {
                Helpers.Execute.BeginOnThreadPool(() => _eventAggregator.Publish(item));
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

#if WP8
        public void UploadFile(TLLong fileId, TLObject owner, StorageFile file)
        {
            FileUtils.SwitchIdleDetectionMode(false);
                       
            var item = FileUtils.GetUploadableItem(fileId, owner, file);

            lock (_itemsSyncRoot)
            {
                _items.Add(item);
            }

            StartAwaitingWorkers();
        }
#endif

        public void UploadFile(TLLong fileId, TLObject owner, string fileName, IList<UploadablePart> parts)
        {
            FileUtils.SwitchIdleDetectionMode(false);

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

        private UploadableItem GetUploadableItem(TLLong fileId, TLObject owner, string fileName, long fileLength)
        {
            FileUtils.SwitchIdleDetectionMode(false);

            var item = new UploadableItem(fileId, owner, fileName, fileLength);
            item.Parts = GetItemParts(item, fileLength);
            return item;
        }

        private UploadableItem GetUploadableItem(TLLong fileId, TLObject owner, string fileName, long fileLength, IList<UploadablePart> parts)
        {
            var item = new UploadableItem(fileId, owner, fileName, fileLength);
            item.Parts = GetItemParts(item, fileLength, parts);
            return item;
        }

        private List<UploadablePart> GetItemParts(UploadableItem item, long fileLength)
        {
            var chunkSize = FileUtils.GetChunkSize(fileLength);
            var partsCount = FileUtils.GetPartsCount(fileLength, chunkSize);
            var parts = new List<UploadablePart>();
            for (var i = 0; i < partsCount; i++)
            {
                var part = new UploadablePart(item, new TLInt(i), i * chunkSize, Math.Min(chunkSize, fileLength - i * chunkSize));
                parts.Add(part);
            }

            item.IsSmallFile = fileLength < chunkSize;

            return parts;
        }

        private List<UploadablePart> GetItemParts(UploadableItem item, long fileLength, IList<UploadablePart> uploadedParts)
        {
            var chunkSize = FileUtils.GetChunkSize(fileLength);
            var parts = new List<UploadablePart>(uploadedParts);
            foreach (var uploadedPart in uploadedParts)
            {
                uploadedPart.SetParentItem(item);
            }
            var uploadedLength = uploadedParts.Sum(x => x.Count);

            var partsCount = FileUtils.GetPartsCount(item.IsoFileLength - uploadedLength, chunkSize);
            for (var i = 0; i < partsCount; i++)
            {
                var partId = i + uploadedParts.Count;
                var part = new UploadablePart(item, new TLInt(partId), uploadedLength + i * chunkSize, Math.Min(chunkSize, fileLength - (uploadedLength + i * chunkSize)));
                parts.Add(part);
            }

            item.IsSmallFile = fileLength < chunkSize;

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
