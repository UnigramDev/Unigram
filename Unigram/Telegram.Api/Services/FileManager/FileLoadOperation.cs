using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Native.TL;
using Telegram.Api.TL;
using Telegram.Api.TL.Upload;
using Telegram.Api.TL.Upload.Methods;

namespace Telegram.Api.Services.FileManager
{
    public class FileLoadOperation
    {
        private class RequestInfo
        {
            public int requestToken;
            public int offset;
            public TLUploadFile response;
            public TLUploadCdnFile responseCdn;
            public TLUploadWebFile responseWeb;
        }

        private const int stateIdle = 0;
        private const int stateDownloading = 1;
        private const int stateFailed = 2;
        private const int stateFinished = 3;

        private const int downloadChunkSize = 1024 * 32;
        private const int downloadChunkSizeBig = 1024 * 128;
        private const int maxDownloadRequests = 4;
        private const int maxDownloadRequestsBig = 2;
        private const int bigFileSizeFrom = 1024 * 1024;

        private bool started;
        private int datacenter_id;
        private TLInputFileLocationBase location;
        private TLInputWebFileLocation webLocation;
        private volatile int state = stateIdle;
        private int downloadedBytes;
        private int totalBytesCount;
        private int bytesCountPadding;
        private byte[] key;
        private byte[] iv;
        private int currentDownloadChunkSize;
        private int currentMaxDownloadRequests;
        private int requestsCount;
        private int renameRetryCount;

        private int cdnDatacenterId;
        private byte[] cdnIv;
        private byte[] cdnKey;
        private byte[] cdnToken;
        private bool reuploadingCdn;

        private int nextDownloadOffset;
        private List<RequestInfo> requestInfos;
        private List<RequestInfo> delayedRequestInfos;

        private FileInfo cacheFileTemp;
        private FileInfo cacheFileFinal;
        private FileInfo cacheIvTemp;

        private String ext;
        private FileStream fileOutputStream;
        private FileStream fiv;
        private string storePath;
        private string tempPath;
        private bool isForceRequest;
        private bool isCdn;

        private FileType currentType;
        public FileType CurrentType
        {
            get
            {
                return currentType;
            }
        }

        public FileDidFinishLoadingFile DidFinishLoadingFile { get; set; }
        public FileDidFailedLoadingFile DidFailedLoadingFile { get; set; }
        public FileDidChangedLoadProgress DidChangedLoadProgress { get; set; }

        public FileLoadOperation(TLFileLocationBase photoLocation, String extension, int size)
        {
            this.state = stateIdle;
            /*if (photoLocation instanceof TL_fileEncryptedLocation) {
                this.location = new TL_inputEncryptedFileLocation();
                this.location.id = photoLocation.volume_id;
                this.location.volume_id = photoLocation.volume_id;
                this.location.access_hash = photoLocation.secret;
                this.location.local_id = photoLocation.local_id;
                this.iv = new byte[32];
                System.arraycopy(photoLocation.iv, 0, this.iv, 0, this.iv.length);
                this.key = photoLocation.key;
                this.datacenter_id = photoLocation.dc_id;
            } else*/
            if (photoLocation is TLFileLocation fileLocation)
            {
                this.location = new TLInputFileLocation()
                {
                    VolumeId = fileLocation.VolumeId,
                    Secret = fileLocation.Secret,
                    LocalId = fileLocation.LocalId
                };
                this.datacenter_id = fileLocation.DCId;
            }
            this.currentType = FileType.Photo;
            this.totalBytesCount = size;
            if (extension == null)
            {
                extension = "jpg";
            }
            this.ext = extension;
        }

        public FileLoadOperation(TLWebDocument webDocument)
        {
            this.state = 0;
            this.webLocation = new TLInputWebFileLocation();
            this.webLocation.Url = webDocument.Url;
            this.webLocation.AccessHash = webDocument.AccessHash;
            this.totalBytesCount = webDocument.Size;
            this.datacenter_id = webDocument.DCId;
            String defaultExt = FileLoader.getExtensionByMime(webDocument.MimeType);
            if (webDocument.MimeType.StartsWith("image/"))
            {
                this.currentType = FileType.Photo;
            }
            else if (webDocument.MimeType.Equals("audio/ogg"))
            {
                this.currentType = FileType.Audio;
            }
            else if (webDocument.MimeType.StartsWith("video/"))
            {
                this.currentType = FileType.Video;
            }
            else
            {
                this.currentType = FileType.File;
            }
            this.ext = ImageLoader.getHttpUrlExtension(webDocument.Url, defaultExt);
        }

        public FileLoadOperation(TLDocument documentLocation)
        {
            try
            {
                /*if (documentLocation instanceof TLRPC.TL_documentEncrypted) {
                    location = new TLRPC.TL_inputEncryptedFileLocation();
                    location.id = documentLocation.id;
                    location.access_hash = documentLocation.access_hash;
                    datacenter_id = documentLocation.dc_id;
                    iv = new byte[32];
                    System.arraycopy(documentLocation.iv, 0, iv, 0, iv.length);
                    key = documentLocation.key;
                } else*/
                if (documentLocation is TLDocument document)
                {
                    location = new TLInputDocumentFileLocation
                    {
                        Id = document.Id,
                        AccessHash = document.AccessHash
                    };
                    datacenter_id = document.DCId;
                }
                totalBytesCount = documentLocation.Size;
                if (key != null)
                {
                    int toAdd = 0;
                    if (totalBytesCount % 16 != 0)
                    {
                        bytesCountPadding = 16 - totalBytesCount % 16;
                        totalBytesCount += bytesCountPadding;
                    }
                }
                ext = FileLoader.getDocumentFileName(documentLocation);
                int idx;
                if (ext == null || (idx = ext.LastIndexOf('.')) == -1)
                {
                    ext = "";
                }
                else
                {
                    ext = ext.Substring(idx);
                }
                if ("audio/ogg".Equals(documentLocation.MimeType))
                {
                    currentType = FileType.Audio;
                }
                else if ("video/mp4".Equals(documentLocation.MimeType))
                {
                    currentType = FileType.Video;
                }
                else
                {
                    currentType = FileType.File;
                }
                if (ext.Length <= 1)
                {
                    if (documentLocation.MimeType != null)
                    {
                        switch (documentLocation.MimeType)
                        {
                            case "video/mp4":
                                ext = ".mp4";
                                break;
                            case "audio/ogg":
                                ext = ".ogg";
                                break;
                            default:
                                ext = "";
                                break;
                        }
                    }
                    else
                    {
                        ext = "";
                    }
                }
            }
            catch (Exception e)
            {
                //FileLog.e(e);
                onFail(true, 0);
            }
        }

        public void setForceRequest(bool forceRequest)
        {
            this.isForceRequest = forceRequest;
        }

        public bool IsForceRequest()
        {
            return this.isForceRequest;
        }

        public void SetPaths(string store, string temp)
        {
            this.storePath = store;
            this.tempPath = temp;
        }

        public bool WasStarted()
        {
            return this.started;
        }

        //public String getFileName()
        //{
        //    if (this.location != null)
        //    {
        //        return this.location.volume_id + "_" + this.location.local_id + "." + this.ext;
        //    }

        //    return Utils.ComputeMD5(this.webLocation.Url) + "." + this.ext;
        //}

        public bool Start()
        {
            if (state != stateIdle)
            {
                return false;
            }
            if (location == null && webLocation == null)
            {
                onFail(true, 0);
                return false;
            }

            String fileNameFinal;
            String fileNameTemp;
            String fileNameIv = null;
            if (webLocation != null)
            {
                String md5 = Utils.MD5(webLocation.Url);
                fileNameTemp = md5 + ".temp";
                fileNameFinal = md5 + ext;
                if (key != null)
                {
                    fileNameIv = md5 + ".iv";
                }
            }
            else
            {
                var volume_id = 0L;
                var local_id = 0;
                var id = 0L;

                if (location is TLInputFileLocation fileLocation)
                {
                    volume_id = fileLocation.VolumeId;
                    local_id = fileLocation.LocalId;
                }
                else if (location is TLInputDocumentFileLocation documentLocation)
                {
                    id = documentLocation.Id;
                }

                if (volume_id != 0 && local_id != 0)
                {
                    if (datacenter_id == int.MinValue || volume_id == int.MinValue || datacenter_id == 0)
                    {
                        onFail(true, 0);
                        return false;
                    }

                    fileNameTemp = volume_id + "_" + local_id + ".temp";
                    fileNameFinal = volume_id + "_" + local_id + ext;
                    if (key != null)
                    {
                        fileNameIv = volume_id + "_" + local_id + ".iv";
                    }
                }
                else
                {
                    if (datacenter_id == 0 || id == 0)
                    {
                        onFail(true, 0);
                        return false;
                    }

                    fileNameTemp = datacenter_id + "_" + id + ".temp";
                    fileNameFinal = datacenter_id + "_" + id + ext;
                    if (key != null)
                    {
                        fileNameIv = datacenter_id + "_" + id + ".iv";
                    }
                }
            }
            currentDownloadChunkSize = totalBytesCount >= bigFileSizeFrom ? downloadChunkSizeBig : downloadChunkSize;
            currentMaxDownloadRequests = totalBytesCount >= bigFileSizeFrom ? maxDownloadRequestsBig : maxDownloadRequests;
            requestInfos = new List<RequestInfo>(currentMaxDownloadRequests);
            delayedRequestInfos = new List<RequestInfo>(currentMaxDownloadRequests - 1);
            state = stateDownloading;

            cacheFileFinal = new FileInfo(Path.Combine(storePath, fileNameFinal));
            bool exist = cacheFileFinal.Exists;
            if (exist && totalBytesCount != 0 && totalBytesCount != cacheFileFinal.Length)
            {
                cacheFileFinal.Delete();
            }

            if (!cacheFileFinal.Exists)
            {
                cacheFileTemp = new FileInfo(Path.Combine(tempPath, fileNameTemp));
                if (cacheFileTemp.Exists)
                {
                    downloadedBytes = (int)cacheFileTemp.Length;
                    nextDownloadOffset = downloadedBytes = downloadedBytes / currentDownloadChunkSize * currentDownloadChunkSize;
                }

                //if (BuildVars.DEBUG_VERSION)
                //{
                //    FileLog.d("start loading file to temp = " + cacheFileTemp + " final = " + cacheFileFinal);
                //}

                if (fileNameIv != null)
                {
                    cacheIvTemp = new FileInfo(Path.Combine(tempPath, fileNameIv));
                    try
                    {
                        //fiv = new RandomAccessFile(cacheIvTemp, "rws");
                        fiv = new FileStream(cacheIvTemp.FullName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                        long len = cacheIvTemp.Length;
                        if (len > 0 && len % 32 == 0)
                        {
                            fiv.Read(iv, 0, 32);
                        }
                        else
                        {
                            downloadedBytes = 0;
                        }
                    }
                    catch (Exception e)
                    {
                        //FileLog.e(e);
                        downloadedBytes = 0;
                    }
                }
                try
                {
                    //fileOutputStream = new RandomAccessFile(cacheFileTemp, "rws");
                    fileOutputStream = new FileStream(cacheFileTemp.FullName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    if (downloadedBytes != 0)
                    {
                        fileOutputStream.Seek(downloadedBytes, SeekOrigin.Begin);
                    }
                }
                catch (Exception e)
                {
                    //FileLog.e(e);
                }
                if (fileOutputStream == null)
                {
                    onFail(true, 0);
                    return false;
                }
                started = true;
                //Utilities.stageQueue.postRunnable(new Runnable() {
                //@Override
                //public void run()
                //{
                Execute.BeginOnThreadPool(() =>
                {
                    if (totalBytesCount != 0 && downloadedBytes == totalBytesCount)
                    {
                        try
                        {
                            OnFinishLoadingFile(false);
                        }
                        catch (Exception e)
                        {
                            onFail(true, 0);
                        }
                    }
                    else
                    {
                        startDownloadRequest();
                    }
                });
                //    }
                //});
            }
            else
            {
                started = true;
                try
                {
                    OnFinishLoadingFile(false);
                }
                catch (Exception e)
                {
                    onFail(true, 0);
                }
            }
            return true;
        }

        private void ClearOperation(RequestInfo currentInfo)
        {
            RequestInfo info;
            int a;
            int minOffset = int.MaxValue;
            for (a = 0; a < this.requestInfos.Count; a += stateDownloading)
            {
                info = (RequestInfo)this.requestInfos[a];
                minOffset = Math.Min(info.offset, minOffset);
                if (!(currentInfo == info || info.requestToken == 0))
                {
                    //ConnectionsManager.getInstance().cancelRequest(info.requestToken, true);
                }
            }
            this.requestInfos.Clear();
            for (a = 0; a < this.delayedRequestInfos.Count; a += stateDownloading)
            {
                info = (RequestInfo)this.delayedRequestInfos[a];
                if (info.response != null)
                {
                    //info.response.disableFree = false;
                    //info.response.freeResources();
                }
                else if (info.responseWeb != null)
                {
                    //info.responseWeb.disableFree = false;
                    //info.responseWeb.freeResources();
                }
                else if (info.responseCdn != null)
                {
                    //info.responseCdn.disableFree = false;
                    //info.responseCdn.freeResources();
                }
                minOffset = Math.Min(info.offset, minOffset);
            }
            this.delayedRequestInfos.Clear();
            this.requestsCount = 0;
            this.nextDownloadOffset = minOffset;
        }

        private void OnFinishLoadingFile(bool increment)
        {
            if (state != stateDownloading)
            {
                return;
            }
            state = stateFinished;
            Cleanup();
            if (cacheIvTemp != null)
            {
                cacheIvTemp.Delete();
                cacheIvTemp = null;
            }
            if (cacheFileTemp != null)
            {
                bool renameResult = cacheFileTemp.RenameTo(cacheFileFinal);
                if (!renameResult)
                {
                    //if (BuildVars.DEBUG_VERSION)
                    //{
                    //    FileLog.e("unable to rename temp = " + cacheFileTemp + " to final = " + cacheFileFinal + " retry = " + renameRetryCount);
                    //}
                    renameRetryCount++;
                    if (renameRetryCount < 3)
                    {
                        state = stateDownloading;
                        //                    Utilities.stageQueue.postRunnable(new Runnable()
                        //{
                        //    @Override
                        //                        public void run()
                        //    {
                        //        try
                        //        {
                        //            onFinishLoadingFile(increment);
                        //        }
                        //        catch (Exception e)
                        //        {
                        //            onFail(false, 0);
                        //        }
                        //    }
                        //}, 200);
                        return;
                    }
                    cacheFileFinal = cacheFileTemp;
                }
            }
            //if (BuildVars.DEBUG_VERSION)
            //{
            //    FileLog.e("finished downloading file to " + cacheFileFinal);
            //}

            DidFinishLoadingFile?.Invoke(this, cacheFileFinal);

            if (increment)
            {
                if (currentType == FileType.Audio)
                {
                    //StatsController.getInstance().incrementReceivedItemsCount(ConnectionsManager.getCurrentNetworkType(), StatsController.TYPE_AUDIOS, 1);
                }
                else if (currentType == FileType.Video)
                {
                    //StatsController.getInstance().incrementReceivedItemsCount(ConnectionsManager.getCurrentNetworkType(), StatsController.TYPE_VIDEOS, 1);
                }
                else if (currentType == FileType.Photo)
                {
                    //StatsController.getInstance().incrementReceivedItemsCount(ConnectionsManager.getCurrentNetworkType(), StatsController.TYPE_PHOTOS, 1);
                }
                else if (currentType == FileType.File)
                {
                    //StatsController.getInstance().incrementReceivedItemsCount(ConnectionsManager.getCurrentNetworkType(), StatsController.TYPE_FILES, 1);
                }
            }
        }

        private void processRequestResult(RequestInfo requestInfo, TLRPCError error)
        {
            requestInfos.Remove(requestInfo);
            if (error == null)
            {
                try
                {
                    if (downloadedBytes != requestInfo.offset)
                    {
                        if (state == stateDownloading)
                        {
                            delayedRequestInfos.Add(requestInfo);
                            if (requestInfo.response != null)
                            {
                                //requestInfo.response.disableFree = true;
                            }
                            else
                            {
                                //requestInfo.responseWeb.disableFree = true;
                            }
                        }

                        return;
                    }

                    byte[] bytes;
                    if (requestInfo.response != null)
                    {
                        bytes = requestInfo.response.Bytes;
                    }
                    else if (requestInfo.responseWeb != null)
                    {
                        bytes = requestInfo.responseWeb.Bytes;
                    }
                    else if (requestInfo.responseCdn != null)
                    {
                        bytes = requestInfo.responseCdn.Bytes;
                    }
                    else
                    {
                        bytes = null;
                    }

                    if (bytes == null || bytes.Length == 0)
                    {
                        OnFinishLoadingFile(true);
                        return;
                    }

                    //if (requestInfo.responseCdn != null)
                    //{
                    //    int offset = requestInfo.offset / 16;
                    //    this.cdnIv[15] = (byte)(offset & 0xFF);
                    //    this.cdnIv[14] = (byte)((offset >> 8) & 0xFF);
                    //    this.cdnIv[13] = (byte)((offset >> 16) & 0xFF);
                    //    this.cdnIv[12] = (byte)((offset >> 24) & 0xFF);
                    //    Utilities.aesCtrDecryption(bytes, this.cdnKey, this.cdnIv, 0, bytes.Length);
                    //}

                    int currentBytesSize = bytes.Length;
                    downloadedBytes += currentBytesSize;
                    bool finishedDownloading = currentBytesSize != currentDownloadChunkSize || (totalBytesCount == downloadedBytes || downloadedBytes % currentDownloadChunkSize != 0) && (totalBytesCount <= 0 || totalBytesCount <= downloadedBytes);

                    //if (key != null)
                    //{
                    //    Utilities.aesIgeEncryption(bytes, key, iv, false, true, 0, bytes.Length);
                    //    if (finishedDownloading && bytesCountPadding != 0)
                    //    {
                    //        bytes.limit(bytes.limit() - bytesCountPadding);
                    //    }
                    //}
                    if (fileOutputStream != null)
                    {
                        //FileChannel channel = fileOutputStream.getChannel();
                        //channel.write(bytes.buffer);
                        fileOutputStream.Write(bytes, 0, bytes.Length);
                    }
                    if (fiv != null)
                    {
                        fiv.Seek(0, SeekOrigin.Begin);
                        fiv.Write(iv, 0, iv.Length);
                    }
                    if (totalBytesCount > 0 && state == stateDownloading)
                    {
                        DidChangedLoadProgress?.Invoke(this, Math.Min(1.0f, (float)downloadedBytes / (float)totalBytesCount));
                    }

                    for (int a = 0; a < this.delayedRequestInfos.Count; a += stateDownloading)
                    {
                        RequestInfo delayedRequestInfo = (RequestInfo)this.delayedRequestInfos[a];
                        if (this.downloadedBytes == delayedRequestInfo.offset)
                        {
                            this.delayedRequestInfos.RemoveAt(a);
                            processRequestResult(delayedRequestInfo, null);
                            if (delayedRequestInfo.response != null)
                            {
                                //delayedRequestInfo.response.disableFree = false;
                                //delayedRequestInfo.response.freeResources();
                            }
                            else if (delayedRequestInfo.responseWeb != null)
                            {
                                //delayedRequestInfo.responseWeb.disableFree = false;
                                //delayedRequestInfo.responseWeb.freeResources();
                            }
                            else if (delayedRequestInfo.responseCdn != null)
                            {
                                //delayedRequestInfo.responseCdn.disableFree = false;
                                //delayedRequestInfo.responseCdn.freeResources();
                            }
                            break;
                        }
                    }
                    if (finishedDownloading)
                    {
                        OnFinishLoadingFile(true);
                    }
                    else
                    {
                        startDownloadRequest();
                    }
                }
                catch (Exception e)
                {
                    onFail(false, 0);
                    //FileLog.e(e);
                }
            }
            else
            {
                if (error.ErrorMessage.Contains("FILE_MIGRATE_"))
                {
                    String errorMsg = error.ErrorMessage.Replace("FILE_MIGRATE_", "");
                    int? val;
                    try
                    {
                        val = int.Parse(errorMsg);
                    }
                    catch (Exception e)
                    {
                        val = null;
                    }
                    if (val == null)
                    {
                        onFail(false, 0);
                    }
                    else
                    {
                        datacenter_id = val.Value;
                        nextDownloadOffset = 0;
                        startDownloadRequest();
                    }
                }
                else if (error.ErrorMessage.Contains("OFFSET_INVALID"))
                {
                    if (downloadedBytes % currentDownloadChunkSize == 0)
                    {
                        try
                        {
                            OnFinishLoadingFile(true);
                        }
                        catch (Exception e)
                        {
                            //FileLog.e(e);
                            onFail(false, 0);
                        }
                    }
                    else
                    {
                        onFail(false, 0);
                    }
                }
                else if (error.ErrorMessage.Contains("RETRY_LIMIT"))
                {
                    onFail(false, 2);
                }
                else if (error.ErrorMessage.Contains("is already authorizing"))
                {
                    nextDownloadOffset = 0;
                    startDownloadRequest();
                }
                else
                {
                    //if (location != null)
                    //{
                    //    FileLog.e("" + location + " id = " + location.id + " local_id = " + location.local_id + " access_hash = " + location.access_hash + " volume_id = " + location.volume_id + " secret = " + location.secret);
                    //}
                    //else if (webLocation != null)
                    //{
                    //    FileLog.e("" + webLocation + " id = " + webLocation.url + " access_hash = " + webLocation.access_hash);
                    //}
                    onFail(false, 0);
                }
            }
        }

        private void Cleanup()
        {
            try
            {
                if (fileOutputStream != null)
                {
                    //try
                    //{
                    //    fileOutputStream.getChannel().close();
                    //}
                    //catch (Exception e)
                    //{
                    //    FileLog.e(e);
                    //}
                    fileOutputStream.Dispose();
                    fileOutputStream = null;
                }
            }
            catch (Exception e)
            {
                //FileLog.e(e);
            }

            try
            {
                if (fiv != null)
                {
                    fiv.Dispose();
                    fiv = null;
                }
            }
            catch (Exception e)
            {
                //FileLog.e(e);
            }

            if (delayedRequestInfos != null)
            {
                for (int a = 0; a < delayedRequestInfos.Count; a++)
                {
                    RequestInfo requestInfo = delayedRequestInfos[a];
                    if (requestInfo.response != null)
                    {
                        //requestInfo.response.disableFree = false;
                        //requestInfo.response.freeResources();
                    }
                    else if (requestInfo.responseWeb != null)
                    {
                        //requestInfo.responseWeb.disableFree = false;
                        //requestInfo.responseWeb.freeResources();
                    }
                }
                delayedRequestInfos.Clear();
            }
        }

        private void onFail(bool thread, int reason)
        {
            Cleanup();
            state = stateFailed;
            if (thread)
            {
                //    Utilities.stageQueue.postRunnable(new Runnable() {
                //    @Override
                //    public void run()
                //    {
                //        delegate.didFailedLoadingFile(FileLoadOperation.this, reason);
                //    }
                //});

                Execute.BeginOnThreadPool(() =>
                {
                    DidFailedLoadingFile?.Invoke(this, reason);
                });
            }
            else
            {
                DidFailedLoadingFile?.Invoke(this, reason);
            }
        }

        private void startDownloadRequest()
        {
            if (state != stateDownloading || totalBytesCount > 0 && nextDownloadOffset >= totalBytesCount || requestInfos.Count + delayedRequestInfos.Count >= currentMaxDownloadRequests)
            {
                return;
            }
            int count = 1;
            if (totalBytesCount > 0)
            {
                count = Math.Max(0, currentMaxDownloadRequests - requestInfos.Count/* - delayedRequestInfos.size()*/);
            }

            for (int a = 0; a < count; a++)
            {
                if (totalBytesCount > 0 && nextDownloadOffset >= totalBytesCount)
                {
                    break;
                }
                bool isLast = totalBytesCount <= 0 || a == count - 1 || totalBytesCount > 0 && nextDownloadOffset + currentDownloadChunkSize >= totalBytesCount;
                TLObject request;
                int offset;
                int flags;
                if (isCdn)
                {
                    TLUploadGetCdnFile req = new TLUploadGetCdnFile();
                    req.FileToken = cdnToken;
                    req.Offset = offset = nextDownloadOffset;
                    req.Limit = currentDownloadChunkSize;
                    request = req;
                    //!!!flags = ConnectionsManager.ConnectionTypeGeneric;
                    //flags = requestsCount % 2 == 0 ? ConnectionsManager.ConnectionTypeDownload : ConnectionsManager.ConnectionTypeDownload2;
                }
                else if (webLocation != null)
                {
                    TLUploadGetWebFile req = new TLUploadGetWebFile();
                    req.Location = webLocation;
                    req.Offset = offset = nextDownloadOffset;
                    req.Limit = currentDownloadChunkSize;
                    request = req;
                    //1!!flags = ConnectionsManager.ConnectionTypeGeneric;
                    //flags = requestsCount % 2 == 0 ? ConnectionsManager.ConnectionTypeDownload : ConnectionsManager.ConnectionTypeDownload2;
                }
                else
                {
                    TLUploadGetFile req = new TLUploadGetFile();
                    req.Location = location;
                    req.Offset = offset = nextDownloadOffset;
                    req.Limit = currentDownloadChunkSize;
                    request = req;
                    //flags = requestsCount % 2 == 0 ? ConnectionsManager.ConnectionTypeDownload : ConnectionsManager.ConnectionTypeDownload2;
                }
                nextDownloadOffset += currentDownloadChunkSize;
                RequestInfo requestInfo = new RequestInfo();
                requestInfos.Add(requestInfo);
                requestInfo.offset = offset;

                int dcId;
                if (isCdn)
                {
                    dcId = cdnDatacenterId;
                }
                else
                {
                    dcId = datacenter_id;
                }

                //var reset = new ManualResetEvent(false);
                MTProtoService.Current.SendRequestAsync<TLObject>("", request, dcId, isCdn, result =>
                {
                    //reset.Set();
                    if (result is TLUploadFileCdnRedirect redirect)
                    {
                        isCdn = true;
                        cdnDatacenterId = redirect.DCId;
                        cdnIv = redirect.EncryptionIV;
                        cdnKey = redirect.EncryptionKey;
                        cdnToken = redirect.FileToken;
                        ClearOperation(requestInfo);
                        startDownloadRequest();
                    }
                    else if (result is TLUploadCdnFileReuploadNeeded reuploadNeeded && !reuploadingCdn)
                    {
                        ClearOperation(requestInfo);
                        reuploadingCdn = true;
                        TLUploadReuploadCdnFile req = new TLUploadReuploadCdnFile();
                        req.FileToken = cdnToken;
                        req.RequestToken = reuploadNeeded.RequestToken;

                        MTProtoService.Current.SendRequestAsync<TLObject>("upload.reuploadCdnFile", req, datacenter_id, isCdn, resultReupload =>
                        {
                            reuploadingCdn = false;
                            startDownloadRequest();
                        }, faultReupload =>
                        {
                            reuploadingCdn = false;
                            if (faultReupload.ErrorMessage.Equals("FILE_TOKEN_INVALID") && faultReupload.ErrorMessage.Equals("REQUEST_TOKEN_INVALID"))
                            {
                                isCdn = false;
                                ClearOperation(requestInfo);
                                startDownloadRequest();
                            }
                            else
                            {
                                onFail(false, 0);
                            }
                        });
                    }
                    else if (!(result is TLUploadCdnFileReuploadNeeded))
                    {
                        if (result is TLUploadFile file)
                        {
                            requestInfo.response = file;
                        }
                        else if (result is TLUploadWebFile webFile)
                        {
                            requestInfo.responseWeb = webFile;
                        }
                        else
                        {
                            requestInfo.responseCdn = result as TLUploadCdnFile;
                        }

                        if (result != null)
                        {
                            if (currentType == FileType.Audio)
                            {
                                //StatsController.getInstance().incrementReceivedBytesCount(response.networkType, StatsController.TYPE_AUDIOS, response.getObjectSize() + 4);
                            }
                            else if (currentType == FileType.Video)
                            {
                                //StatsController.getInstance().incrementReceivedBytesCount(response.networkType, StatsController.TYPE_VIDEOS, response.getObjectSize() + 4);
                            }
                            else if (currentType == FileType.Photo)
                            {
                                //StatsController.getInstance().incrementReceivedBytesCount(response.networkType, StatsController.TYPE_PHOTOS, response.getObjectSize() + 4);
                            }
                            else if (currentType == FileType.File)
                            {
                                //StatsController.getInstance().incrementReceivedBytesCount(response.networkType, StatsController.TYPE_FILES, response.getObjectSize() + 4);
                            }
                        }

                        processRequestResult(requestInfo, null);
                    }
                }, fault =>
                {
                    //reset.Set();
                    if (request is TLUploadGetCdnFile && fault.ErrorMessage.Equals("FILE_TOKEN_INVALID"))
                    {
                        isCdn = false;
                        ClearOperation(requestInfo);
                        startDownloadRequest();
                    }
                    else
                    {
                        processRequestResult(null, fault);
                    }
                });
                requestsCount++;
                //reset.WaitOne();
            }
        }
    }
}
