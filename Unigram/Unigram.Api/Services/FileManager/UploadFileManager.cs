using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
#if WP8
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
#endif
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Windows.Foundation;
using Execute = Telegram.Api.Helpers.Execute;


namespace Telegram.Api.Services.FileManager
{
    public class UploadProgressChangedEventArgs
    {
        public double Progress { get; protected set; }

        public UploadableItem Item { get; protected set; }

        public UploadProgressChangedEventArgs(UploadableItem item, double progress)
        {
            Item = item;
            Progress = progress;
        }
    }

    public class UploadablePart
    {
        public UploadableItem ParentItem { get; protected set; }

        public int FilePart { get; protected set; }

        public PartStatus Status { get; set; }

        public byte[] Bytes { get; protected set; }

        public long Position { get; protected set; }

        public long Count { get; protected set; }

        public string IV { get; set; }

        public void ClearBuffer()
        {
            Bytes = null;
        }

        public UploadablePart(UploadableItem item, int filePart, byte[] bytes)
        {
            ParentItem = item;
            FilePart = filePart;
            Bytes = bytes;
        }

        public UploadablePart(UploadableItem item, int filePart, long position, long count)
        {
            ParentItem = item;
            FilePart = filePart;
            Position = position;
            Count = count;
        }

        public UploadablePart(UploadableItem item, int filePart, byte[] bytes, long position, long count)
        {
            ParentItem = item;
            FilePart = filePart;
            Bytes = bytes;
            Position = position;
            Count = count;
        }

        public override string ToString()
        {
            return string.Format("Part={0}, Status={1}, Position={2}, Count={3}", FilePart, Status, Position, Count);
        }

        public void SetBuffer(byte[] bytes)
        {
            Bytes = bytes;
        }

        public void SetParentItem(UploadableItem item)
        {
            ParentItem = item;
        }
    }

    public class UploadableItem
    {
        public bool FileNotFound { get; set; }

        public bool IsSmallFile { get; set; }

        public long FileId { get; protected set; }

        public string IsoFileName { get; protected set; }

        public long IsoFileLength { get; protected set; }

        public TLObject Owner { get; protected set; }

#if WP8
        public StorageFile File { get; protected set; }

        public TLString Key { get; protected set; }

        public TLString IV { get; protected set; }
#endif

        public byte[] Bytes { get; protected set; }

        public List<UploadablePart> Parts { get; set; }

        public bool Canceled { get; set; }

        internal TaskCompletionSource<UploadableItem> Callback { get; set; }

        internal IProgress<double> Progress { get; set; }

        public UploadableItem(long fileId, TLObject owner, byte[] bytes)
        {
            FileId = fileId;
            Owner = owner;
            Bytes = bytes;
        }

#if WP8
        public UploadableItem(TLLong fileId, TLObject owner, StorageFile file)
        {
            FileId = fileId;
            Owner = owner;
            File = file;
        }

        public UploadableItem(TLLong fileId, TLObject owner, StorageFile file, TLString key, TLString iv)
        {
            FileId = fileId;
            Owner = owner;
            File = file;

            Key = key;
            IV = iv;
        }
#endif

        public UploadableItem(long fileId, TLObject owner, string isoFileName, long isoFileLength)
        {
            FileId = fileId;
            Owner = owner;
            IsoFileName = isoFileName;
            IsoFileLength = isoFileLength;
        }
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
                var worker = new Worker(OnUploading, "uploader"+i);
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

                            // TODO: verify
                            if (item.Callback != null)
                            {
                                item.Callback.TrySetCanceled();
                            }
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

                var result = PutFile(part.ParentItem.FileId, part.FilePart, part.ParentItem.Parts.Count, bytes);
                while (!result)
                {
                    if (part.ParentItem.Canceled)
                    {
                        return;
                    }
                    result = PutFile(part.ParentItem.FileId, part.FilePart, part.ParentItem.Parts.Count, bytes);
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

                            // TODO: verify
                            if (part.ParentItem.Callback != null)
                            {
                                part.ParentItem.Callback.TrySetResult(part.ParentItem);
                            }
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

                            // TODO: verify
                            if (part.ParentItem.Progress != null)
                            {
                                part.ParentItem.Progress.Report(progress);
                            }
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

        private bool PutFile(long fileId, int filePart, int fileTotalPars, byte[] bytes)
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
                    Execute.ShowDebugMessage(string.Format("upload.saveFilePart part={0}, bytesCount={1} error\n", filePart, bytes.Length) + error);

                    manualResetEvent.Set();
                }));

            manualResetEvent.WaitOne();
            return result;
        }

        public IAsyncOperationWithProgress<UploadableItem, double> UploadFileAsync(long fileId, TLObject owner, byte[] bytes)
        {
            return AsyncInfo.Run<UploadableItem, double>((token, progress) =>
            {
                var tsc = new TaskCompletionSource<UploadableItem>();
                var item = GetUploadableItem(fileId, owner, bytes);
                item.Callback = tsc;
                item.Progress = progress;

                lock (_itemsSyncRoot)
                {
                    _items.Add(item);
                }

                StartAwaitingWorkers();
                return tsc.Task;
            });
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
        public void UploadFile(TLLong fileId, TLObject owner, StorageFile file)
        {
            var item = GetUploadableItem(fileId, owner, file);

            lock (_itemsSyncRoot)
            {
                _items.Add(item);
            }

            StartAwaitingWorkers();
        }

        private UploadableItem GetUploadableItem(TLLong fileId, TLObject owner, StorageFile file)
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
                var part = new UploadablePart(item, new TLInt(i), i * chunkSize, Math.Min(chunkSize, (long)size - i * chunkSize));
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
