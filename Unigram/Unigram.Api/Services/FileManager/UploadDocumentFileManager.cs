using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;

namespace Telegram.Api.Services.FileManager
{
    public interface IUploadDocumentFileManager
    {
        void UploadFile(long fileId, TLObject owner, byte[] bytes);
#if WP8
        void UploadFile(long fileId, TLObject owner, StorageFile file);
        void UploadFile(long fileId, TLObject owner, StorageFile file, string key, string iv);
#endif
        void CancelUploadFile(long fileId);
    }

    public class UploadDocumentFileManager : IUploadDocumentFileManager
    {
        private readonly object _itemsSyncRoot = new object();

        private readonly List<UploadableItem> _items = new List<UploadableItem>();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public UploadDocumentFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
        {
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;


            var timer = Stopwatch.StartNew();
            for (var i = 0; i < Constants.DocumentUploadersCount; i++)
            {
                var worker = new Worker(OnUploading, "documentUploader" + i);
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

                    if (result.Item1 && result.Item2 == null)
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

                    if (bytes == null)
                    {
                        part.Status = PartStatus.Ready;
                        return;
                    }
                }
#endif
                if (part.ParentItem.IsSmallFile)
                {
                    bool result = PutFile(part.ParentItem.FileId, part.FilePart, bytes);
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
                    bool result = PutBigFile(part.ParentItem.FileId, part.FilePart, part.ParentItem.Parts.Count, bytes);
                    while (!result)
                    {
                        if (part.ParentItem.Canceled)
                        {
                            return;
                        }
                        result = PutBigFile(part.ParentItem.FileId, part.FilePart, part.ParentItem.Parts.Count, bytes);
                    }
                }

                FileUtils.NotifyProgress(_itemsSyncRoot, _items, part, _eventAggregator);
            }
            else
            {

                var currentWorker = (Worker)state;
                currentWorker.Stop();
            }
        }


        private bool PutBigFile(long fileId, int filePart, int fileTotalPars, byte[] bytes)
        {
            var manualResetEvent = new ManualResetEvent(false);
            var result = false;

            _mtProtoService.SaveBigFilePartCallback(fileId, filePart, fileTotalPars, bytes,
                savingResult =>
                {
                    result = true;
                    manualResetEvent.Set();
                },
                error => Execute.BeginOnThreadPool(TimeSpan.FromSeconds(1.0), () =>
                {
                    Execute.ShowDebugMessage(string.Format("upload.saveBigFilePart part={0}, count={1} error\n", filePart, bytes.Length) + error);

                    manualResetEvent.Set();
                }));

            manualResetEvent.WaitOne();
            return result;
        }

        private bool PutFile(long fileId, int filePart, byte[] bytes)
        {
            var manualResetEvent = new ManualResetEvent(false);
            var result = false;

            _mtProtoService.SaveFilePartCallback(fileId, filePart, bytes,
                savingResult =>
                {
                    result = true;
                    manualResetEvent.Set();
                },
                error => Execute.BeginOnThreadPool(TimeSpan.FromSeconds(1.0), () =>
                {
                    Execute.ShowDebugMessage(string.Format("upload.saveBigFilePart part={0}, count={1} error\n", filePart, bytes.Length) + error);

                    manualResetEvent.Set();
                }));

            manualResetEvent.WaitOne();
            return result;
        }

        public void UploadFile(long fileId, TLObject owner, byte[] bytes)
        {
            FileUtils.SwitchIdleDetectionMode(false);
            var item = GetUploadableItem(fileId, owner, bytes);
            lock (_itemsSyncRoot)
            {
                _items.Add(item);
            }

            StartAwaitingWorkers();
        }

#if WP8
        public void UploadFile(TLLong fileId, TLObject owner, StorageFile file)
        {
            UploadFile(fileId, owner, file, null, null);
        }

        public void UploadFile(TLLong fileId, TLObject owner, StorageFile file, TLString key, TLString iv)
        {
            FileUtils.SwitchIdleDetectionMode(false);
            var item = FileUtils.GetUploadableItem(fileId, owner, file, key, iv);
            //if (item)
            //{
            //    item.IsSmallFile = false;   // to void auto convert small video documents to videos on server side
            //}
            lock (_itemsSyncRoot)
            {
                _items.Add(item);
            }

            StartAwaitingWorkers();
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

            item.IsSmallFile = size < Constants.SmallFileMaxSize;// size < chunkSize;

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
}
