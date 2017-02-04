using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;
using Windows.Foundation;

namespace Telegram.Api.Services.FileManager
{
    public interface IUploadManager
    {
        IAsyncOperationWithProgress<UploadableItem, double> UploadFileAsync(long fileId, string fileName, bool forceBigFile);
        IAsyncOperationWithProgress<UploadableItem, double> UploadFileAsync(long fileId, string fileName);

        void UploadFile(long fileId, TLObject owner, string fileName, bool forceBigFile);
        void UploadFile(long fileId, TLObject owner, string fileName);
        void CancelUploadFile(long fileId);
    }

    public interface IUploadFileManager : IUploadManager { }
    public interface IUploadAudioManager : IUploadManager { }
    public interface IUploadDocumentManager : IUploadManager { }
    public interface IUploadVideoManager : IUploadManager { }

    public class UploadManager : IUploadFileManager, IUploadAudioManager, IUploadDocumentManager, IUploadVideoManager
    {
        private readonly object _itemsSyncRoot = new object();
        private readonly List<UploadableItem> _items = new List<UploadableItem>();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly ITelegramEventAggregator _eventAggregator;
        private readonly IMTProtoService _mtProtoService;

        public UploadManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
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
                    if (item.IsCancelled)
                    {
                        _items.RemoveAt(i--);
                        try
                        {
                            // TODO: verify
                            if (item.Callback != null)
                            {
                                item.Callback.TrySetResult(null);
                            }
                            else
                            {
                                _eventAggregator.Publish(new UploadingCanceledEventArgs(item));
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
                var bytes = FileUtils.ReadTemporaryBytes(part.ParentItem.FileName, part.Position, part.Count);
                part.SetBuffer(bytes);

                //bool result = PutFile(part.ParentItem.FileId, part.FilePart, part.Bytes);
                //while (!result)
                //{
                //    if (part.ParentItem.Canceled)
                //    {
                //        return;
                //    }
                //    result = PutFile(part.ParentItem.FileId, part.FilePart, part.Bytes);
                //}

                if (part.ParentItem.IsSmallFile)
                {
                    bool result = PutFile(part.ParentItem.FileId, part.FilePart, bytes);
                    while (!result)
                    {
                        if (part.ParentItem.IsCancelled)
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
                        if (part.ParentItem.IsCancelled)
                        {
                            return;
                        }
                        result = PutBigFile(part.ParentItem.FileId, part.FilePart, part.ParentItem.Parts.Count, bytes);
                    }
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
                    isCanceled = part.ParentItem.IsCancelled;
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
                        if (part.ParentItem.Callback != null)
                        {
                            part.ParentItem.Progress.Report(1.0);
                            part.ParentItem.Callback.TrySetResult(part.ParentItem);
                        }
                        else
                        {
                            Execute.BeginOnThreadPool(() => _eventAggregator.Publish(part.ParentItem));
                        }
                    }
                    else
                    {
                        if (part.ParentItem.Callback != null)
                        {
                            part.ParentItem.Progress.Report(progress);
                        }
                        else
                        {
                            Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new UploadProgressChangedEventArgs(part.ParentItem, progress)));
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

        public IAsyncOperationWithProgress<UploadableItem, double> UploadFileAsync(long fileId, string fileName)
        {
            return UploadFileAsync(fileId, fileName, false);
        }

        public IAsyncOperationWithProgress<UploadableItem, double> UploadFileAsync(long fileId, string fileName, bool forceBigFile)
        {
            return AsyncInfo.Run<UploadableItem, double>((token, progress) =>
            {
                var tsc = new TaskCompletionSource<UploadableItem>();

                long fileLength = FileUtils.GetLocalFileLength(fileName);
                if (fileLength <= 0) return Task.FromResult<UploadableItem>(null);

                var item = GetUploadableItem(fileId, null, fileName, fileLength);
                item.Callback = tsc;
                item.Progress = progress;

                if (forceBigFile)
                {
                    item.IsSmallFile = false;
                }

                var uploadedCount = item.Parts.Count(x => x.Status == PartStatus.Processed);
                var count = item.Parts.Count;
                var isComplete = uploadedCount == count;

                if (isComplete)
                {
                    if (item.Callback != null)
                    {
                        item.Progress.Report(1.0);
                        item.Callback.TrySetResult(item);
                    }
                    else
                    {
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(item));
                    }
                }
                else
                {
                    lock (_itemsSyncRoot)
                    {
                        _items.Add(item);
                    }

                    StartAwaitingWorkers();
                }

                return tsc.Task;
            });
        }

        public void UploadFile(long fileId, TLObject owner, string fileName)
        {
            UploadFile(fileId, owner, fileName, false);
        }

        public void UploadFile(long fileId, TLObject owner, string fileName, bool forceBigFile)
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

        private UploadableItem GetUploadableItem(long fileId, TLObject owner, string isoFileName, long isoFileLength)
        {
            var item = new UploadableItem(fileId, owner, isoFileName, isoFileLength);
            item.Parts = GetItemParts(item, isoFileLength);
            return item;
        }

        private List<UploadablePart> GetItemParts(UploadableItem item, long isoFileLength)
        {
            const int partSize = 32 * 1024; // 32 Kb: êðàòíî 1 Kb è íàöåëî äåëèò 1024 Kb
            var parts = new List<UploadablePart>();
            var partsCount = item.FileLength / partSize + (item.FileLength % partSize > 0 ? 1 : 0);

            for (var i = 0; i < partsCount; i++)
            {
                var part = new UploadablePart(item, i, i * partSize, Math.Min(partSize, isoFileLength - i * partSize));
                parts.Add(part);
            }

            item.IsSmallFile = item.FileLength < Constants.SmallFileMaxSize;// size < chunkSize;

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
                    item.IsCancelled = true;
                    //_items.Remove(item);
                }
            }
        }
    }
}
