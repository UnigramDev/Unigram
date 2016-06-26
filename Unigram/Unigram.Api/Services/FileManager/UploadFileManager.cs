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
    public interface IUploadFileManager
    {
        void UploadFile(long fileId, TLObject owner, byte[] bytes);
#if WP8
        void UploadFile(long fileId, TLObject owner, StorageFile file);
#endif
        void CancelUploadFile(long fileId);
    }

    public class UploadFileManager : IUploadFileManager
    {
        private readonly object _itemsSyncRoot = new object();

        private readonly List<UploadableItem> _items = new List<UploadableItem>();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public UploadFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
        {
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;


            var timer = Stopwatch.StartNew();
            for (int i = 0; i < Constants.WorkersNumber; i++)
            {
                var worker = new Worker(OnUploading, "uploader" + i);
                _workers.Add(worker);
            }

            TLUtils.WritePerformance("Start workers timer: " + timer.Elapsed);

        }

        private async void OnUploading(object state)
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
#if WP8
                if (bytes == null)
                {
                    var file = part.ParentItem.File;
                    if (file != null)
                    {
                        var task = FileUtils.FillBuffer(file, part);
                        task.Wait();
                        

                        bytes = task.Result.Item2;
                    }

                    if (bytes == null)
                    {
                        part.Status = PartStatus.Ready;
                        return;
                    }
                }
#endif

                var result = await PutFile(part.ParentItem.FileId, part.FilePart, part.ParentItem.Parts.Count, bytes);
                while (!result)
                {
                    if (part.ParentItem.Canceled)
                    {
                        return;
                    }
                    result = await PutFile(part.ParentItem.FileId, part.FilePart, part.ParentItem.Parts.Count, bytes);
                }

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
                        try
                        {
                            Execute.BeginOnThreadPool(() => _eventAggregator.Publish(part.ParentItem));
                        }
                        catch (Exception e)
                        {
                            TLUtils.WriteLine(e.ToString(), LogSeverity.Error);
                        }
                    }
                    else
                    {
                        try
                        {
                            Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new UploadProgressChangedEventArgs(part.ParentItem, progress)));
                        }
                        catch (Exception e)
                        {
                            TLUtils.WriteLine(e.ToString(), LogSeverity.Error);
                        }
                    }
                }
            }
            else
            {

                var currentWorker = (Worker)state;
                currentWorker.Stop();
            }
        }

        private async Task<bool> PutFile(long fileId, int filePart, int fileTotalPars, byte[] bytes)
        {
            var manualResetEvent = new ManualResetEvent(false);
            var result = false;

            var request = await _mtProtoService.SaveFilePartAsync(fileId, filePart, bytes);
            if (request.Error == null)
            {
                result = true;
                manualResetEvent.Set();
            }
            else
            {
                Execute.ShowDebugMessage(string.Format("upload.saveFilePart part={0}, bytesCount={1} error\n", filePart, bytes.Length) + request.Error);
                manualResetEvent.Set();
            }

            manualResetEvent.WaitOne();
            return result;
        }

        public void UploadFile(long fileId, TLObject owner, byte[] bytes)
        {
            var item = GetUploadableItem(fileId, owner, bytes);

            lock (_itemsSyncRoot)
            {
                _items.Add(item);
            }

            StartAwaitingWorkers();
        }

#if WP8
        public void UploadFile(long fileId, TLObject owner, StorageFile file)
        {
            var item = GetUploadableItem(fileId, owner, file);

            lock (_itemsSyncRoot)
            {
                _items.Add(item);
            }

            StartAwaitingWorkers();
        }

        private UploadableItem GetUploadableItem(long fileId, TLObject owner, StorageFile file)
        {
            var item = new UploadableItem(fileId, owner, file);

            var task = file.GetBasicPropertiesAsync().AsTask();
            task.Wait();
            var propertie = task.Result;
            var size = propertie.Size;
            item.Parts = GetItemParts(item, (int)size);
            return item;
        }

        private static List<UploadablePart> GetItemParts(UploadableItem item, int size)
        {
            var chunkSize = FileUtils.GetChunkSize(size);
            var partsCount = FileUtils.GetPartsCount(size, chunkSize);
            var parts = new List<UploadablePart>(partsCount);

            for (var i = 0; i < partsCount; i++)
            {
                var part = new UploadablePart(item, new int?(i), i * chunkSize, Math.Min(chunkSize, (long)size - i * chunkSize));
                parts.Add(part);
            }

            return parts;
        }
#endif

        private UploadableItem GetUploadableItem(long fileId, TLObject owner, byte[] bytes)
        {
            var item = new UploadableItem(fileId, owner, bytes);
            item.Parts = GetItemParts(item);
            return item;
        }

        private static List<UploadablePart> GetItemParts(UploadableItem item)
        {
            var size = item.Bytes.Length;
            var chunkSize = FileUtils.GetChunkSize(size);
            var partsCount = FileUtils.GetPartsCount(size, chunkSize);
            var parts = new List<UploadablePart>(partsCount);

            for (var i = 0; i < partsCount; i++)
            {
                var part = new UploadablePart(item, i, item.Bytes.SubArray(i * chunkSize, Math.Min(chunkSize, item.Bytes.Length - i * chunkSize)));
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

        public void CancelUploadFile(long fileId)
        {
            lock (_itemsSyncRoot)
            {
                var item = _items.FirstOrDefault(x => x.FileId == fileId);

                if (item != null)
                {
                    item.Canceled = true;
                    //_items.Remove(item);
                }
            }
        }
    }

    public class UploadingCanceledEventArgs
    {
        public UploadableItem Item { get; protected set; }

        public UploadingCanceledEventArgs(UploadableItem item)
        {
            Item = item;
        }
    }
}
