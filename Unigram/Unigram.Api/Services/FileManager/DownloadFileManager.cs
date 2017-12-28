using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.Native.TL;
using Telegram.Api.Services.FileManager.EventArgs;
using Telegram.Api.TL;
using Telegram.Api.TL.Upload;
using Telegram.Api.TL.Upload.Methods;
using Windows.Foundation;
using Windows.Security.Cryptography;
using Windows.Storage;

namespace Telegram.Api.Services.FileManager
{
    public interface IDownloadFileManager
    {
        IAsyncOperationWithProgress<DownloadableItem, double> DownloadFileAsync(TLFileLocation file, int fileSize, IProgress<double> test = null);

        void DownloadFile(TLFileLocation file, int fileSize, Action<DownloadableItem> callback);

        void CancelDownloadFile(TLObject owner);
        void Cancel(TLFileLocation file);
    }

    public class DownloadFileManager : IDownloadFileManager
    {
        private readonly object _randomRoot = new object();

        private readonly Random _random = new Random();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly object _itemsSyncRoot = new object();

        private readonly List<DownloadableItem> _items = new List<DownloadableItem>();

        private readonly ITelegramEventAggregator _eventAggregator;
        private readonly IMTProtoService _protoService;
        private readonly IStatsService _statsService;
        private readonly DataType _dataType = DataType.Photos;

        public DownloadFileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService, IStatsService statsService)
        {
            var stopwatch = Stopwatch.StartNew();
            _eventAggregator = eventAggregator;
            _protoService = mtProtoService;
            _statsService = statsService;

            for (int i = 0; i < Constants.WorkersNumber; i++)
            {
                var worker = new Worker(OnDownloading, "downloader" + i);
                _workers.Add(worker);
            }

            System.Diagnostics.Debug.WriteLine("FileManager.ctor {0}", stopwatch.Elapsed);
        }

        private void OnDownloading(object state)
        {
            DownloadablePart part = null;
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
                            _eventAggregator.Publish(new DownloadingCanceledEventArgs(item));
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

            TLRPCError error;
            bool canceled;
            do
            {
                TLUploadFileBase result;
                if (part.ParentItem.CdnRedirect != null)
                    result = GetCdnFile(part.ParentItem.CdnRedirect, part.ParentItem.Location, part.Offset, part.Limit, out error, out canceled);
                else
                    result = GetFile(part.ParentItem.Location, part.Offset, part.Limit, out error, out canceled);

                if (result is TLUploadFileCdnRedirect redirect)
                {
                    part.ParentItem.CdnRedirect = redirect;
                    part.ParentItem.CdnHashes = redirect.CdnFileHashes.ToDictionary(x => x.Offset, x => x);
                    continue;
                }
                else
                {
                    part.File = result as TLUploadFile;
                }

                if (canceled)
                {
                    lock (_itemsSyncRoot)
                    {
                        part.ParentItem.IsCancelled = true;
                        part.Status = PartStatus.Processed;
                        _items.Remove(part.ParentItem);
                    }

                    return;
                }
            } while (part.File == null);

            // indicate progress
            // indicate complete
            bool isComplete;
            bool isCanceled;
            var progress = 0.0;
            lock (_itemsSyncRoot)
            {
                part.Status = PartStatus.Processed;

                var data = part.File.Bytes;
                if (data.Length < part.Limit && (part.Number + 1) != part.ParentItem.Parts.Count)
                {
                    var complete = part.ParentItem.Parts.All(x => x.Status == PartStatus.Processed);
                    if (!complete)
                    {
                        var emptyBufferSize = part.Limit - data.Length;
                        var position = data.Length;

                        var missingPart = new DownloadablePart(part.ParentItem, position, emptyBufferSize, -part.Number);

                        var currentItemIndex = part.ParentItem.Parts.IndexOf(part);
                        part.ParentItem.Parts.Insert(currentItemIndex + 1, missingPart);
                    }
                }
                else if (data.Length == part.Limit && (part.Number + 1) == part.ParentItem.Parts.Count)
                {
                    var currentItemIndex = part.ParentItem.Parts.IndexOf(part);
                    var missingPart = new DownloadablePart(part.ParentItem, part.Offset + part.Limit, part.Limit, currentItemIndex + 1);

                    part.ParentItem.Parts.Insert(currentItemIndex + 1, missingPart);
                }

                isCanceled = part.ParentItem.IsCancelled;

                isComplete = part.ParentItem.Parts.All(x => x.Status == PartStatus.Processed);
                if (!isComplete)
                {
                    var downloadedCount = part.ParentItem.Parts.Count(x => x.Status == PartStatus.Processed);
                    var count = part.ParentItem.Parts.Count;
                    progress = (double)downloadedCount / count;
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
                    byte[] bytes = { };
                    foreach (var p in part.ParentItem.Parts)
                    {
                        var partBytes = p.File.Bytes;

                        var redirect = part.ParentItem.CdnRedirect;
                        if (redirect != null)
                        {
                            var iv = redirect.EncryptionIV;
                            var counter = p.Offset / 16;
                            iv[15] = (byte)(counter & 0xFF);
                            iv[14] = (byte)((counter >> 8) & 0xFF);
                            iv[13] = (byte)((counter >> 16) & 0xFF);
                            iv[12] = (byte)((counter >> 24) & 0xFF);

                            var key = CryptographicBuffer.CreateFromByteArray(redirect.EncryptionKey);

                            var ecount_buf = new byte[0];
                            var num = 0u;
                            partBytes = Utils.AES_ctr128_encrypt(partBytes, key, ref iv, ref ecount_buf, ref num);
                            redirect.EncryptionIV = iv;

                            //TLCdnFileHash hash;
                            //if (!part.ParentItem.CdnHashes.TryGetValue(p.Offset, out hash))
                            //{
                            //    var hashes = GetCdnFileHashes(redirect, part.ParentItem.Location, p.Offset, out TLRPCError er, out bool yolo);
                            //    if (hashes != null)
                            //    {
                            //        foreach (var item in hashes)
                            //        {
                            //            part.ParentItem.CdnHashes[item.Offset] = item;
                            //        }

                            //        part.ParentItem.CdnHashes.TryGetValue(p.Offset, out hash);
                            //    }
                            //}

                            //if (hash != null)
                            //{
                            //    var sha256 = Utils.ComputeSHA256(partBytes);
                            //    if (!sha256.SequenceEqual(hash.Hash))
                            //    {
                            //        lock (_itemsSyncRoot)
                            //        {
                            //            part.ParentItem.IsCancelled = true;
                            //            part.Status = PartStatus.Processed;
                            //            _items.Remove(part.ParentItem);
                            //        }

                            //        Debug.WriteLine("HASH DOESN'T MATCH");
                            //        return;
                            //    }
                            //}
                        }

                        bytes = TLUtils.Combine(bytes, partBytes);
                    }
                    //part.ParentItem.Location.Buffer = bytes;
                    var fileName = String.Format("{0}_{1}_{2}.jpg",
                        part.ParentItem.Location.VolumeId,
                        part.ParentItem.Location.LocalId,
                        part.ParentItem.Location.Secret);

                    FileUtils.WriteTemporaryBites(fileName, bytes);

                    if (part.ParentItem.Callback != null)
                    {
                        part.ParentItem.Progress.Report(1.0);
                        part.ParentItem.Callback.TrySetResult(part.ParentItem);
                    }
                    else
                    {
                        part.ParentItem.Action?.Invoke(part.ParentItem);
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(part.ParentItem));
                    }

                    _statsService.IncrementReceivedItemsCount(_protoService.NetworkType, _dataType, 1);
                }
                else
                {
                    if (part.ParentItem.Callback != null)
                    {
                        part.ParentItem.Progress.Report(progress);
                    }
                    else
                    {
                        Execute.BeginOnThreadPool(() => _eventAggregator.Publish(new DownloadProgressChangedEventArgs(part.ParentItem, progress)));
                    }
                }
            }
        }



        private TLUploadFileBase GetFile(TLFileLocation location, int offset, int limit, out TLRPCError er, out bool isCanceled)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLUploadFileBase result = null;
            TLRPCError outError = null;
            var outIsCanceled = false;
            _protoService.GetFileAsync(location.DCId, location.ToInputFileLocation(), offset, limit,
                callback =>
                {
                    result = callback;
                    manualResetEvent.Set();

                    if (callback is TLUploadFile file)
                    {
                        _statsService.IncrementReceivedBytesCount(_protoService.NetworkType, _dataType, 4 + 4 + file.Bytes.Length + 4);
                    }
                },
                error =>
                {
                    outError = error;

                    if (error.CodeEquals(TLErrorCode.INTERNAL)
                        || (error.CodeEquals(TLErrorCode.BAD_REQUEST) && (error.TypeEquals(TLErrorType.LOCATION_INVALID) || error.TypeEquals(TLErrorType.VOLUME_LOC_NOT_FOUND)))
                        || (error.CodeEquals(TLErrorCode.NOT_FOUND) && error.ErrorMessage != null && error.ErrorMessage.StartsWith("Incorrect dhGen")))
                    {
                        outIsCanceled = true;

                        manualResetEvent.Set();
                        return;
                    }

                    int delay;
                    lock (_randomRoot)
                    {
                        delay = _random.Next(1000, 3000);
                    }

                    Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
                });

            manualResetEvent.WaitOne(20 * 1000);
            er = outError;
            isCanceled = outIsCanceled;

            return result;
        }

        private TLUploadFileBase GetCdnFile(TLUploadFileCdnRedirect redirect, TLFileLocation location, int offset, int limit, out TLRPCError er, out bool isCanceled)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLUploadFileBase result = null;
            TLRPCError outError = null;
            var outIsCanceled = false;

            _protoService.GetCdnFileAsync(redirect.DCId, redirect.FileToken, offset, limit, callback =>
            {
                if (callback is TLUploadCdnFile file)
                {
                    result = new TLUploadFile { Bytes = file.Bytes };
                    manualResetEvent.Set();

                    _statsService.IncrementReceivedBytesCount(_protoService.NetworkType, _dataType, file.Bytes.Length + 4);
                }
                else if (callback is TLUploadCdnFileReuploadNeeded reupload)
                {
                    result = ReuploadFile(redirect, reupload.RequestToken, location, offset, limit, out outError, out outIsCanceled);
                    while (result == null)
                    {
                        result = ReuploadFile(redirect, reupload.RequestToken, location, offset, limit, out outError, out outIsCanceled);
                        if (outIsCanceled)
                        {
                            break;
                        }
                    }

                    manualResetEvent.Set();
                }
            },
            error =>
            {
                outError = error;

                if (error.CodeEquals(TLErrorCode.INTERNAL)
                    || (error.CodeEquals(TLErrorCode.BAD_REQUEST) && (error.TypeEquals(TLErrorType.LOCATION_INVALID) || error.TypeEquals(TLErrorType.VOLUME_LOC_NOT_FOUND)))
                    || (error.CodeEquals(TLErrorCode.NOT_FOUND) && error.ErrorMessage != null && error.ErrorMessage.StartsWith("Incorrect dhGen")))
                {
                    outIsCanceled = true;

                    manualResetEvent.Set();
                    return;
                }

                int delay;
                lock (_randomRoot)
                {
                    delay = _random.Next(1000, 3000);
                }

                Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
            });

            manualResetEvent.WaitOne(20 * 1000);
            er = outError;
            isCanceled = outIsCanceled;

            return result;
        }

        private TLUploadFileBase ReuploadFile(TLUploadFileCdnRedirect redirect, byte[] requestToken, TLFileLocation location, int offset, int limit, out TLRPCError er, out bool isCanceled)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLUploadFileBase result = null;
            TLRPCError outError = null;
            var outIsCanceled = false;

            _protoService.ReuploadCdnFileAsync(redirect.DCId, redirect.FileToken, requestToken, callback =>
            {
                if (callback != null)
                {
                    result = GetCdnFile(redirect, location, offset, limit, out outError, out outIsCanceled);
                    while (result == null)
                    {
                        result = GetCdnFile(redirect, location, offset, limit, out outError, out outIsCanceled);
                        if (outIsCanceled)
                        {
                            break;
                        }
                    }

                    manualResetEvent.Set();
                }
            },
            error =>
            {
                outError = error;

                if (error.CodeEquals(TLErrorCode.INTERNAL)
                    || (error.CodeEquals(TLErrorCode.BAD_REQUEST) && (error.TypeEquals(TLErrorType.LOCATION_INVALID) || error.TypeEquals(TLErrorType.VOLUME_LOC_NOT_FOUND)))
                    || (error.CodeEquals(TLErrorCode.NOT_FOUND) && error.ErrorMessage != null && error.ErrorMessage.StartsWith("Incorrect dhGen")))
                {
                    outIsCanceled = true;

                    manualResetEvent.Set();
                    return;
                }

                int delay;
                lock (_randomRoot)
                {
                    delay = _random.Next(1000, 3000);
                }

                Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
            });

            manualResetEvent.WaitOne(20 * 1000);
            er = outError;
            isCanceled = outIsCanceled;

            return result;
        }

        private TLVector<TLCdnFileHash> GetCdnFileHashes(TLUploadFileCdnRedirect redirect, TLFileLocation location, int offset, out TLRPCError er, out bool isCanceled)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLVector<TLCdnFileHash> result = null;
            TLRPCError outError = null;
            var outIsCanceled = false;

            var req = new TLUploadGetCdnFileHashes();
            req.FileToken = redirect.FileToken;
            req.Offset = offset;

            _protoService.SendRequestAsync<TLVector<TLCdnFileHash>>("upload.getCdnFileHashes", req, location.DCId, true, callback =>
            {
                result = callback;
                manualResetEvent.Set();
            },
            error =>
            {
                outError = error;

                if (error.CodeEquals(TLErrorCode.INTERNAL)
                    || (error.CodeEquals(TLErrorCode.BAD_REQUEST) && (error.TypeEquals(TLErrorType.LOCATION_INVALID) || error.TypeEquals(TLErrorType.VOLUME_LOC_NOT_FOUND)))
                    || (error.CodeEquals(TLErrorCode.NOT_FOUND) && error.ErrorMessage != null && error.ErrorMessage.ToString().StartsWith("Incorrect dhGen")))
                {
                    outIsCanceled = true;

                    manualResetEvent.Set();
                    return;
                }

                int delay;
                lock (_randomRoot)
                {
                    delay = _random.Next(1000, 3000);
                }

                Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
            });

            manualResetEvent.WaitOne(20 * 1000);
            er = outError;
            isCanceled = outIsCanceled;

            return result;
        }

        public IAsyncOperationWithProgress<DownloadableItem, double> DownloadFileAsync(TLFileLocation file, int fileSize, IProgress<double> test = null)
        {
            return AsyncInfo.Run<DownloadableItem, double>((token, progress) =>
            {
                var tsc = new TaskCompletionSource<DownloadableItem>();
                //var boh = new TaskCompletionSource<string>();

                //FileLoader.Current.LoadFile(file, ".jpg", fileSize, false, boh);
                //var name = await boh.Task;

                //return new DownloadableItem { DestFileName = name };







                var downloadableItem = GetDownloadableItem(file, null, fileSize);
                downloadableItem.Callback = tsc;
                downloadableItem.Progress = test ?? progress;

                lock (_itemsSyncRoot)
                {
                    bool addFile = true;
                    foreach (var item in _items)
                    {
                        if (item.Location.VolumeId == file.VolumeId &&
                            item.Location.LocalId == file.LocalId)
                        {
                            downloadableItem.Callback = item.Callback;
                            downloadableItem.Progress = item.Progress;
                            addFile = false;

                            Debug.WriteLine("Already downloading document");
                            break;
                        }
                    }

                    if (addFile)
                    {
                        _items.Add(downloadableItem);
                    }
                }

                StartAwaitingWorkers();

                return tsc.Task;
            });
        }

        public void DownloadFile(TLFileLocation file, int fileSize, Action<DownloadableItem> callback)
        {
            //var tsc = new TaskCompletionSource<string>();

            //FileLoader.Current.LoadFile(file, ".jpg", fileSize, false, tsc);
            //var name = await tsc.Task;
            //callback?.Invoke(new DownloadableItem { DestFileName = name });

            //return;

            var downloadableItem = GetDownloadableItem(file, null, fileSize);
            downloadableItem.Action = callback;

            lock (_itemsSyncRoot)
            {
                bool addFile = true;
                foreach (var item in _items)
                {
                    if (item.Location.VolumeId == file.VolumeId &&
                        item.Location.LocalId == file.LocalId)
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

        private DownloadableItem GetDownloadableItem(TLFileLocation location, TLObject owner, int fileSize)
        {
            var item = new DownloadableItem
            {
                Owner = owner,
                Location = location,
                FileSize = fileSize
            };
            item.Parts = GetItemParts(fileSize, item);

            return item;
        }

        private List<DownloadablePart> GetItemParts(int size, DownloadableItem item)
        {
            //var chunkSize = Constants.DownloadChunkSize;

            //var chunkSize = size > 1024 * 1024 ? 1024 * 128 : 1024 * 32;
            var chunkSize = 1024 * 128;
            var parts = new List<DownloadablePart>();
            var partsCount = size / chunkSize + 1;
            for (var i = 0; i < partsCount; i++)
            {
                //var part = new DownloadablePart(item, i * chunkSize, size == 0 ? 0 : chunkSize);
                var part = new DownloadablePart(item, i * chunkSize, chunkSize, i);
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
                    item.IsCancelled = true;
                }
            }
        }

        public void Cancel(TLFileLocation file)
        {
            Execute.BeginOnThreadPool(() =>
            {
                lock (_itemsSyncRoot)
                {
                    var items = _items.Where(x => x.Location.VolumeId == file.VolumeId && x.Location.LocalId == file.LocalId);

                    foreach (var item in items)
                    {
                        item.IsCancelled = true;
                    }
                }
            });
        }
    }
}
