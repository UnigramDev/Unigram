using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Windows.Storage;

namespace Telegram.Api.Services.FileManager
{
    public class FileLoader
    {
        private static FileLoader _current;
        public static FileLoader Current
        {
            get
            {
                if (_current == null)
                    _current = new FileLoader();

                return _current;
            }
        }

        public const int MEDIA_DIR_IMAGE = 0;
        public const int MEDIA_DIR_AUDIO = 1;
        public const int MEDIA_DIR_VIDEO = 2;
        public const int MEDIA_DIR_DOCUMENT = 3;
        public const int MEDIA_DIR_CACHE = 4;

        public FileLoader()
        {
            CreateDirectory(MEDIA_DIR_IMAGE);
            CreateDirectory(MEDIA_DIR_AUDIO);
            CreateDirectory(MEDIA_DIR_VIDEO);
            CreateDirectory(MEDIA_DIR_DOCUMENT);
            CreateDirectory(MEDIA_DIR_CACHE);
        }

        private void CreateDirectory(int dir)
        {
            var folder = GetDirectory(dir);
            if (Directory.Exists(folder)) { }
            else
            {
                Directory.CreateDirectory(folder);
            }
        }

        private string GetDirectory(int dir)
        {
            string folder;
            switch (dir)
            {
                case MEDIA_DIR_IMAGE:
                    folder = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "Image");
                    break;
                case MEDIA_DIR_AUDIO:
                    folder = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "Audio");
                    break;
                case MEDIA_DIR_VIDEO:
                    folder = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "Video");
                    break;
                case MEDIA_DIR_DOCUMENT:
                    folder = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "Document");
                    break;
                case MEDIA_DIR_CACHE:
                default:
                    return ApplicationData.Current.TemporaryFolder.Path;
            }

            return folder;
        }

        private List<FileLoadOperation> loadOperationQueue = new List<FileLoadOperation>();
        private List<FileLoadOperation> audioLoadOperationQueue = new List<FileLoadOperation>();
        private List<FileLoadOperation> photoLoadOperationQueue = new List<FileLoadOperation>();
        //
        private ConcurrentDictionary<string, FileLoadOperation> loadOperationPaths = new ConcurrentDictionary<string, FileLoadOperation>();

        private int currentLoadOperationsCount = 0;
        private int currentAudioLoadOperationsCount = 0;
        private int currentPhotoLoadOperationsCount = 0;

        public void LoadFile(TLPhotoSize photo, String ext, bool cacheOnly, TaskCompletionSource<string> tsc)
        {
            LoadFile(null, null, photo.Location, ext, photo.Size, false, cacheOnly || (photo != null && photo.Size == 0 /*|| photo.Location.key != null*/), tsc);
        }

        public void LoadFile(TLDocument document, bool force, bool cacheOnly, TaskCompletionSource<string> tsc)
        {
            LoadFile(document, null, null, null, 0, force, cacheOnly /*|| document != null && document.key != null*/, tsc);
        }

        public void LoadFile(TLWebDocument document, bool force, bool cacheOnly, TaskCompletionSource<string> tsc)
        {
            LoadFile(null, document, null, null, 0, force, cacheOnly, tsc);
        }

        public void LoadFile(TLFileLocationBase location, String ext, int size, bool cacheOnly, TaskCompletionSource<string> tsc)
        {
            LoadFile(null, null, location, ext, size, true, cacheOnly || size == 0 /*|| (location != null && location.key != null)*/, tsc);
        }

        private void LoadFile(TLDocument document, TLWebDocument webDocument, TLFileLocationBase location, String locationExt, int locationSize, bool force, bool cacheOnly, TaskCompletionSource<string> tsc)
        {
            //fileLoaderQueue.postRunnable(new Runnable() {
            //@Override
            //public void run()
            Execute.BeginOnThreadPool(() =>
            {
                String fileName = null;
                if (location != null)
                {
                    fileName = getAttachFileName(location, locationExt);
                }
                else if (document != null)
                {
                    fileName = getAttachFileName(document);
                }
                else if (webDocument != null)
                {
                    fileName = getAttachFileName(webDocument);
                }

                if (fileName == null || fileName.Contains("" + int.MinValue))
                {
                    return;
                }

                loadOperationPaths.TryGetValue(fileName, out FileLoadOperation operation);
                if (operation != null)
                {
                    if (force)
                    {
                        operation.setForceRequest(true);
                        List<FileLoadOperation> downloadQueue;
                        if (TLMessage.isVoiceDocument(document) || TLMessage.isVoiceWebDocument(webDocument))
                        {
                            downloadQueue = audioLoadOperationQueue;
                        }
                        else if (location != null || TLMessage.isImageWebDocument(webDocument))
                        {
                            downloadQueue = photoLoadOperationQueue;
                        }
                        else
                        {
                            downloadQueue = loadOperationQueue;
                        }
                        if (downloadQueue != null)
                        {
                            int index = downloadQueue.IndexOf(operation);
                            if (index > 0)
                            {
                                downloadQueue.RemoveAt(index);
                                downloadQueue.Insert(0, operation);
                            }
                        }
                    }
                    return;
                }

                string tempDir = GetDirectory(MEDIA_DIR_CACHE);
                string storeDir = tempDir;
                int type = MEDIA_DIR_CACHE;

                if (location != null)
                {
                    operation = new FileLoadOperation(location, locationExt, locationSize);
                    type = MEDIA_DIR_IMAGE;
                }
                else if (document != null)
                {
                    operation = new FileLoadOperation(document);
                    if (TLMessage.isVoiceDocument(document))
                    {
                        type = MEDIA_DIR_AUDIO;
                    }
                    else if (TLMessage.isVideoDocument(document))
                    {
                        type = MEDIA_DIR_VIDEO;
                    }
                    else
                    {
                        type = MEDIA_DIR_DOCUMENT;
                    }
                }
                else if (webDocument != null)
                {
                    operation = new FileLoadOperation(webDocument);
                    if (TLMessage.isVoiceWebDocument(webDocument))
                    {
                        type = MEDIA_DIR_AUDIO;
                    }
                    else if (TLMessage.isVideoWebDocument(webDocument))
                    {
                        type = MEDIA_DIR_VIDEO;
                    }
                    else if (TLMessage.isImageWebDocument(webDocument))
                    {
                        type = MEDIA_DIR_IMAGE;
                    }
                    else
                    {
                        type = MEDIA_DIR_DOCUMENT;
                    }
                }
                if (!cacheOnly)
                {
                    storeDir = GetDirectory(type);
                }
                operation.SetPaths(storeDir, tempDir);

                String finalFileName = fileName;
                int finalType = type;
                //    FileLoadOperation.FileLoadOperationDelegate fileLoadOperationDelegate = new FileLoadOperation.FileLoadOperationDelegate() {
                //        @Override
                //        public void didFinishLoadingFile(FileLoadOperation operation, File finalFile)
                //    {
                //        if (delegate != null)
                //        {
                //            delegate.fileDidLoaded(finalFileName, finalFile, finalType);
                //        }
                //        checkDownloadQueue(document, webDocument, location, finalFileName);
                //    }

                //    @Override
                //        public void didFailedLoadingFile(FileLoadOperation operation, int reason)
                //    {
                //        checkDownloadQueue(document, webDocument, location, finalFileName);
                //        if (delegate != null)
                //        {
                //            delegate.fileDidFailedLoad(finalFileName, reason);
                //        }
                //    }

                //    @Override
                //        public void didChangedLoadProgress(FileLoadOperation operation, float progress)
                //    {
                //        if (delegate != null)
                //        {
                //            delegate.fileLoadProgressChanged(finalFileName, progress);
                //        }
                //    }
                //};
                //operation.setDelegate(fileLoadOperationDelegate);

                operation.DidChangedLoadProgress = (s, args) => Debug.WriteLine("Download progress: " + args);
                operation.DidFailedLoadingFile = (s, args) => checkDownloadQueue(document, webDocument, location, finalFileName);
                operation.DidFinishLoadingFile = (s, args) =>
                {
                    checkDownloadQueue(document, webDocument, location, finalFileName);
                    tsc.SetResult(args.FullName);
                };

                /*if (location != null) {
                    operation = new FileLoadOperation(location.dc_id, location.volume_id, location.volume_id, location.secret, location.local_id, location.key, location.iv, locationExt != null ? locationExt : "jpg", 0, locationSize, !cacheOnly ? getDirectory(type) : tempDir, tempDir, fileLoadOperationDelegate);
                } else if (document != null) {
                    String ext = FileLoader.getDocumentFileName(document);
                    int idx;
                    if (ext == null || (idx = ext.lastIndexOf('.')) == -1) {
                        ext = "";
                    } else {
                        ext = ext.substring(idx + 1);
                    }
                    if (ext.length() <= 0) {
                        if (document.mime_type != null) {
                            switch (document.mime_type) {
                                case "video/mp4":
                                    ext = "mp4";
                                    break;
                                case "audio/ogg":
                                    ext = "ogg";
                                    break;
                                default:
                                    ext = "";
                                    break;
                            }
                        } else {
                            ext = "";
                        }
                    }
                    operation = new FileLoadOperation(document.dc_id, document.id, 0, document.access_hash, 0, document.key, document.iv, ext, document.version, document.size, !cacheOnly ? getDirectory(type) : tempDir, tempDir, fileLoadOperationDelegate);
                }*/
                loadOperationPaths[fileName] = operation;
                int maxCount = force ? 3 : 1;
                if (type == MEDIA_DIR_AUDIO)
                {
                    if (currentAudioLoadOperationsCount < maxCount)
                    {
                        if (operation.Start())
                        {
                            currentAudioLoadOperationsCount++;
                        }
                    }
                    else
                    {
                        if (force)
                        {
                            audioLoadOperationQueue.Insert(0, operation);
                        }
                        else
                        {
                            audioLoadOperationQueue.Add(operation);
                        }
                    }
                }
                else if (location != null)
                {
                    if (currentPhotoLoadOperationsCount < maxCount)
                    {
                        if (operation.Start())
                        {
                            currentPhotoLoadOperationsCount++;
                        }
                    }
                    else
                    {
                        if (force)
                        {
                            photoLoadOperationQueue.Insert(0, operation);
                        }
                        else
                        {
                            photoLoadOperationQueue.Add(operation);
                        }
                    }
                }
                else
                {
                    if (currentLoadOperationsCount < maxCount)
                    {
                        if (operation.Start())
                        {
                            currentLoadOperationsCount++;
                        }
                    }
                    else
                    {
                        if (force)
                        {
                            loadOperationQueue.Insert(0, operation);
                        }
                        else
                        {
                            loadOperationQueue.Add(operation);
                        }
                    }
                }
            });
        }

        private void checkDownloadQueue(TLDocument document, TLWebDocument webDocument, TLFileLocationBase location, String arg1)
        {
            //fileLoaderQueue.postRunnable(new Runnable() {
            //@Override
            //public void run()
            //{
            Execute.BeginOnThreadPool(() =>
            {
                loadOperationPaths.TryRemove(arg1, out FileLoadOperation operation);
                if (TLMessage.isVoiceDocument(document) || TLMessage.isVoiceWebDocument(webDocument))
                {
                    if (operation != null)
                    {
                        if (operation.WasStarted())
                        {
                            currentAudioLoadOperationsCount--;
                        }
                        else
                        {
                            audioLoadOperationQueue.Remove(operation);
                        }
                    }
                    while (audioLoadOperationQueue.Count > 0)
                    {
                        operation = audioLoadOperationQueue[0];
                        int maxCount = operation.IsForceRequest() ? 3 : 1;
                        if (currentAudioLoadOperationsCount < maxCount)
                        {
                            operation = audioLoadOperationQueue.Poll();
                            if (operation != null && operation.Start())
                            {
                                currentAudioLoadOperationsCount++;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else if (location != null || TLMessage.isImageWebDocument(webDocument))
                {
                    if (operation != null)
                    {
                        if (operation.WasStarted())
                        {
                            currentPhotoLoadOperationsCount--;
                        }
                        else
                        {
                            photoLoadOperationQueue.Remove(operation);
                        }
                    }

                    Debug.WriteLine(arg1 + " Completed");

                    while (photoLoadOperationQueue.Count > 0)
                    {
                        operation = photoLoadOperationQueue[0];
                        int maxCount = operation.IsForceRequest() ? 3 : 1;
                        if (currentPhotoLoadOperationsCount < maxCount)
                        {
                            operation = photoLoadOperationQueue.Poll();
                            if (operation != null && operation.Start())
                            {
                                currentPhotoLoadOperationsCount++;
                                Debug.WriteLine(arg1 + " New download");
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    if (operation != null)
                    {
                        if (operation.WasStarted())
                        {
                            currentLoadOperationsCount--;
                        }
                        else
                        {
                            loadOperationQueue.Remove(operation);
                        }
                    }
                    while (loadOperationQueue.Count > 0)
                    {
                        operation = loadOperationQueue[0];
                        int maxCount = operation.IsForceRequest() ? 3 : 1;
                        if (currentLoadOperationsCount < maxCount)
                        {
                            operation = loadOperationQueue.Poll();
                            if (operation != null && operation.Start())
                            {
                                currentLoadOperationsCount++;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            });
        }

        public static String getAttachFileName(TLObject attach)
        {
            return getAttachFileName(attach, null);
        }

        public static String getAttachFileName(TLObject attach, String ext)
        {
            if (attach is TLDocument document)
            {
                String docExt = null;
                if (docExt == null)
                {
                    docExt = getDocumentFileName(document);
                    int idx;
                    if (docExt == null || (idx = docExt.LastIndexOf('.')) == -1)
                    {
                        docExt = "";
                    }
                    else
                    {
                        docExt = docExt.Substring(idx);
                    }
                }
                if (docExt.Length <= 1)
                {
                    if (document.MimeType != null)
                    {
                        switch (document.MimeType)
                        {
                            case "video/mp4":
                                docExt = ".mp4";
                                break;
                            case "audio/ogg":
                                docExt = ".ogg";
                                break;
                            default:
                                docExt = "";
                                break;
                        }
                    }
                    else
                    {
                        docExt = "";
                    }
                }
                if (document.Version == 0)
                {
                    if (docExt.Length > 1)
                    {
                        return document.DCId + "_" + document.Id + docExt;
                    }
                    else
                    {
                        return document.DCId + "_" + document.Id;
                    }
                }
                else
                {
                    if (docExt.Length > 1)
                    {
                        return document.DCId + "_" + document.Id + "_" + document.Version + docExt;
                    }
                    else
                    {
                        return document.DCId + "_" + document.Id + "_" + document.Version;
                    }
                }
            }
            else if (attach is TLWebDocument webDocument)
            {
                return Utils.MD5(webDocument.Url) + ImageLoader.getHttpUrlExtension(webDocument.Url, getExtensionByMime(webDocument.MimeType));
            }
            else if (attach is TLPhotoSize photo)
            {
                if (photo.Location == null || photo.Location is TLFileLocationUnavailable)
                {
                    return "";
                }
                TLFileLocation location = (TLFileLocation)photo.Location;
                return location.VolumeId + "_" + location.LocalId + (ext != null ? ext : ".jpg");
            }
            else if (attach is TLFileLocationBase)
            {
                if (attach is TLFileLocationUnavailable)
                {
                    return "";
                }
                TLFileLocation location = (TLFileLocation)attach;
                return location.VolumeId + "_" + location.LocalId + (ext != null ? ext : ".jpg");
            }
            return "";
        }

        public static String getDocumentFileName(TLDocument document)
        {
            if (document != null)
            {
                //if (document.FileName != null)
                //{
                //    return document.file_name;
                //}
                for (int a = 0; a < document.Attributes.Count; a++)
                {
                    var documentAttribute = document.Attributes[a];
                    if (documentAttribute is TLDocumentAttributeFilename filenameAttribute)
                    {
                        return filenameAttribute.FileName;
                    }
                }
            }
            return "";
        }

        public static String getExtensionByMime(String mime)
        {
            int index;
            if ((index = mime.IndexOf('/')) != -1)
            {
                return mime.Substring(index + 1);
            }
            return "";
        }
    }
}
