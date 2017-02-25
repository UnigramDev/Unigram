using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Telegram.Api.Aggregator;
using Telegram.Api.Services.FileManager;
using Windows.Foundation;
using Telegram.Api.TL;
using Telegram.Api.Services.FileManager.EventArgs;
using Windows.Storage.FileProperties;
using Windows.Graphics.Imaging;

namespace Telegram.Api.Helpers
{
    public static class FileUtils
    {
        public static string GetFileName(string fileName)
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName);
        }

        public static string GetTempFileName(string fileName)
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp", fileName);
        }

        public static Uri GetTempFileUri(string fileName)
        {
            return new Uri($"ms-appdata:///local/temp/{fileName}");
        }
        
        public static void CreateTemporaryFolder()
        {
            if (Directory.Exists(Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp\\parts")) == false)
            {
                Directory.CreateDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp"));
                Directory.CreateDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp\\parts"));
                Directory.CreateDirectory(Path.Combine(ApplicationData.Current.LocalFolder.Path, "temp\\placeholders"));
            }
        }

        public static async Task<TLPhotoSizeBase> GetFileThumbnailAsync(StorageFile file)
        {
            var imageProps = await file.Properties.GetImagePropertiesAsync();
            var videoProps = await file.Properties.GetVideoPropertiesAsync();

            if (imageProps.Width > 0 || videoProps.Width > 0)
            {
                using (var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 96, ThumbnailOptions.ResizeThumbnail))
                {
                    if (thumb != null)
                    {
                        var randomStream = thumb as IRandomAccessStream;

                        var originalWidth = (int)thumb.OriginalWidth;
                        var originalHeight = (int)thumb.OriginalHeight;

                        if (thumb.ContentType != "image/jpeg")
                        {
                            var memoryStream = new InMemoryRandomAccessStream();
                            var bitmapDecoder = await BitmapDecoder.CreateAsync(thumb);
                            var pixelDataProvider = await bitmapDecoder.GetPixelDataAsync();
                            var bitmapEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, memoryStream);
                            bitmapEncoder.SetPixelData(bitmapDecoder.BitmapPixelFormat, BitmapAlphaMode.Ignore, bitmapDecoder.PixelWidth, bitmapDecoder.PixelHeight, bitmapDecoder.DpiX, bitmapDecoder.DpiY, pixelDataProvider.DetachPixelData());
                            await bitmapEncoder.FlushAsync();
                            randomStream = memoryStream;
                        }

                        var fileLocation = new TLFileLocation
                        {
                            VolumeId = TLLong.Random(),
                            LocalId = TLInt.Random(),
                            Secret = TLLong.Random(),
                            DCId = 0
                        };

                        var desiredName = string.Format("{0}_{1}_{2}.jpg", fileLocation.VolumeId, fileLocation.LocalId, fileLocation.Secret);
                        var desiredFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("temp\\" + desiredName);

                        var buffer = new Windows.Storage.Streams.Buffer(Convert.ToUInt32(randomStream.Size));
                        var buffer2 = await randomStream.ReadAsync(buffer, buffer.Capacity, InputStreamOptions.None);
                        using (var stream = await desiredFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await stream.WriteAsync(buffer2);
                        }

                        var result = new TLPhotoSize
                        {
                            W = originalWidth,
                            H = originalHeight,
                            Size = (int)randomStream.Size,
                            Type = string.Empty,
                            Location = fileLocation
                        };

                        randomStream.Dispose();
                        return result;
                    }
                }
            }

            return null;
        }

        public static void MergePartsToFile(Func<DownloadablePart, string> getPartName, IEnumerable<DownloadablePart> parts, string fileName)
        {
            using (var part1 = File.Open(GetTempFileName(fileName), FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (IEnumerator<DownloadablePart> enumerator = parts.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        DownloadablePart current = enumerator.Current;
                        string text = getPartName.Invoke(current);
                        using (var part2 = File.Open(GetTempFileName("parts\\" + text), FileMode.OpenOrCreate, FileAccess.Read))
                        {
                            byte[] array = new byte[part2.Length];
                            part2.Read(array, 0, array.Length);
                            part1.Position = part1.Length;
                            part1.Write(array, 0, array.Length);
                        }

                        File.Delete(GetTempFileName("parts\\" + text));
                    }
                }
            }
        }

        public static bool Delete(object syncRoot, string fileName)
        {
            try
            {
                lock (syncRoot)
                {
                    if (File.Exists(GetFileName(fileName)))
                    {
                        File.Delete(GetFileName(fileName));
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                TLUtils.WriteLine("FILE ERROR: cannot delete " + fileName, LogSeverity.Error);
                TLUtils.WriteException(e);
            }
            return false;
        }

        public static void Copy(object syncRoot, string fileName, string destinationFileName)
        {
            try
            {
                lock (syncRoot)
                {
                    File.Copy(GetFileName(fileName), GetFileName(destinationFileName), true);
                }
            }
            catch (Exception e)
            {
                TLUtils.WriteLine("DB ERROR: cannot copy " + fileName, LogSeverity.Error);
                TLUtils.WriteException(e);
            }
        }

        public static void NotifyProgress(object itemsSyncRoot, IList<UploadableItem> items, UploadablePart part, ITelegramEventAggregator eventAggregator)
        {
            bool flag = false;
            double progress = 0.0;
            bool canceled;
            lock (itemsSyncRoot)
            {
                part.Status = PartStatus.Processed;
                canceled = part.ParentItem.IsCancelled;
                if (!canceled)
                {
                    flag = Enumerable.All(part.ParentItem.Parts, (UploadablePart x) => x.Status == PartStatus.Processed);
                    if (!flag)
                    {
                        double num = (double)Enumerable.Count<UploadablePart>(part.ParentItem.Parts, (UploadablePart x) => x.Status == PartStatus.Processed);
                        double num2 = (double)part.ParentItem.Parts.Count;
                        progress = num / num2;
                    }
                    else
                    {
                        items.Remove(part.ParentItem);
                    }
                }
            }
            if (!canceled)
            {
                if (flag)
                {
                    FileUtils.SwitchIdleDetectionMode(true);
                    Execute.BeginOnThreadPool(() => eventAggregator.Publish(part.ParentItem));

                    // TODO: verify
                    if (part.ParentItem.Callback != null)
                    {
                        part.ParentItem.Callback.TrySetResult(part.ParentItem);
                    }

                    return;
                }
                if (part.ParentItem.FileNotFound)
                {
                    return;
                }
                UploadProgressChangedEventArgs args = new UploadProgressChangedEventArgs(part.ParentItem, progress);
                Execute.BeginOnThreadPool(delegate
                {
                    eventAggregator.Publish(args);
                });

                if (part.ParentItem.Progress != null)
                {
                    part.ParentItem.Progress.Report(progress);
                }
            }
        }

        public static void SwitchIdleDetectionMode(bool enabled)
        {
        }

        public static int GetChunkSize(long totalSize)
        {
            int result = 32768;
            if (totalSize > (long)(262144 * Constants.MaximumChunksCount))
            {
                result = 524288;
            }
            else if (totalSize > (long)(131072 * Constants.MaximumChunksCount))
            {
                result = 262144;
            }
            else if (totalSize > (long)(65536 * Constants.MaximumChunksCount))
            {
                result = 131072;
            }
            else if (totalSize > (long)(32768 * Constants.MaximumChunksCount))
            {
                result = 65536;
            }
            return result;
        }

        public static int GetPartsCount(long totalSize, int chunkSize)
        {
            return (int)(totalSize / (long)chunkSize + ((totalSize % (long)chunkSize > 0L) ? 1L : 0L));
        }

        public static int GetLocalFileLength(string fileName)
        {
            if (File.Exists(GetTempFileName(fileName)))
            {
                using (var file = File.Open(GetTempFileName(fileName), FileMode.Open, FileAccess.Read))
                {
                    return (int)file.Length;
                }
            }

            return -1;
        }

        private static StorageFile GetLocalFile(string fileName)
        {
            StorageFile result = null;
            try
            {
                IAsyncOperation<StorageFile> fileAsync = ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                fileAsync.AsTask<StorageFile>().Wait();
                result = fileAsync.GetResults();
            }
            catch (Exception)
            {
            }
            return result;
        }

        private static StorageFile CreateLocalFile(string fileName)
        {
            IAsyncOperation<StorageFile> asyncOperation = ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            asyncOperation.AsTask<StorageFile>().Wait();
            return asyncOperation.GetResults();
        }

        private static void Delete(StorageFile file)
        {
            if (file != null)
            {
                IAsyncAction asyncAction = file.DeleteAsync();
                asyncAction.AsTask().Wait();
                asyncAction.GetResults();
            }
        }

        public static void CheckMissingPart(object syncRoot, DownloadablePart part, string partName)
        {
            if (part.Offset == 0)
            {
                if (File.Exists(GetTempFileName("parts\\" + partName)))
                {
                    File.Delete(GetTempFileName("parts\\" + partName));
                }
            }

            if (File.Exists(GetTempFileName("parts\\" + partName)))
            {
                File.Delete(GetTempFileName("parts\\" + partName));
            }

            using (var file = File.Open(GetTempFileName("parts\\" + partName), FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                byte[] data = part.File.Bytes;
                part.File.Bytes = new byte[0];
                file.Position = file.Length;
                file.Write(data, 0, data.Length);
                if (data.Length < part.Limit && part.Number + 1 != part.ParentItem.Parts.Count)
                {
                    lock (syncRoot)
                    {
                        if (!Enumerable.All<DownloadablePart>(part.ParentItem.Parts, (DownloadablePart x) => x.Status == PartStatus.Processed))
                        {
                            int value = part.Limit - data.Length;
                            long position = file.Position;
                            DownloadablePart downloadablePart = new DownloadablePart(part.ParentItem, (int)position, value, -part.Number);
                            int num = part.ParentItem.Parts.IndexOf(part);
                            part.ParentItem.Parts.Insert(num + 1, downloadablePart);
                        }
                    }
                }
            }
        }

        public static void WriteBytes(string fileName, byte[] bytes)
        {
            using (var file = File.Open(GetFileName(fileName), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                file.Write(bytes, 0, bytes.Length);
            }
        }

        public static void WriteTemporaryBites(string fileName, byte[] bytes)
        {
            using (var file = File.Open(GetTempFileName(fileName), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                file.Write(bytes, 0, bytes.Length);
            }
        }

        public static byte[] ReadBytes(string fileName, long position, long length)
        {
            byte[] array = null;
            using (var file = File.Open(GetFileName(fileName), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file.Position = position;
                array = new byte[length];
                file.Read(array, 0, (int)length);
            }
            return array;
        }

        public static byte[] ReadTemporaryBytes(string fileName, long position, long length)
        {
            byte[] array = null;
            using (var file = File.Open(GetTempFileName(fileName), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                file.Position = position;
                array = new byte[length];
                file.Read(array, 0, (int)length);
            }
            return array;
        }

        //public static async Task<Tuple<bool, byte[]>> FillBuffer(IStorageFile file, UploadablePart part)
        //{
        //    Tuple<bool, byte[]> result;
        //    try
        //    {
        //        if (part.ParentItem.FileNotFound)
        //        {
        //            result = new Tuple<bool, byte[]>(false, null);
        //        }
        //        else
        //        {
        //            using (IRandomAccessStream randomAccessStream = await file.OpenAsync(0))
        //            {
        //                using (IInputStream inputStreamAt = randomAccessStream.GetInputStreamAt((ulong)part.Position))
        //                {
        //                    byte[] array = new byte[part.Count];
        //                    using (DataReader dataReader = new DataReader(inputStreamAt))
        //                    {
        //                        await dataReader.LoadAsync((uint)array.Length);
        //                        dataReader.ReadBytes(array);
        //                    }
        //                    if (part.ParentItem.Key != null && part.ParentItem.IV != null)
        //                    {
        //                        string key = part.ParentItem.Key;
        //                        string string = (part.FilePart.Value == 0) ? part.ParentItem.IV : part.IV;
        //                        if (string == null)
        //                        {
        //                            result = new Tuple<bool, byte[]>(true, null);
        //                            return result;
        //                        }
        //                        byte[] data;
        //                        byte[] array2 = Utils.AesIge(array, key.Data, tLString.Data, true, out data);
        //                        array = array2;
        //                        int num = part.FilePart.Value + 1;
        //                        if (part.ParentItem.Parts.Count > num)
        //                        {
        //                            part.ParentItem.Parts[num].IV = TLString.FromBigEndianData(data);
        //                        }
        //                    }
        //                    result = new Tuple<bool, byte[]>(true, array);
        //                }
        //            }
        //        }
        //    }
        //    catch (FileNotFoundException ex)
        //    {
        //        Execute.ShowDebugMessage("FillBuffer FileNotFoundException\n" + ex);
        //        result = new Tuple<bool, byte[]>(false, null);
        //    }
        //    catch (Exception ex2)
        //    {
        //        Execute.ShowDebugMessage("FillBuffer Exception\n" + ex2);
        //        result = new Tuple<bool, byte[]>(true, null);
        //    }
        //    return result;
        //}

        //public static UploadableItem GetUploadableItem(long? fileId, TLObject owner, StorageFile file)
        //{
        //    return FileUtils.GetUploadableItem(fileId, owner, file, null, null);
        //}

        //public static UploadableItem GetUploadableItem(long? fileId, TLObject owner, StorageFile file, string key, string iv)
        //{
        //    UploadableItem uploadableItem = new UploadableItem(fileId, owner, file, key, iv);
        //    Task<BasicProperties> task = file.GetBasicPropertiesAsync().AsTask<BasicProperties>();
        //    task.Wait();
        //    BasicProperties result = task.Result;
        //    ulong size = result.Size;
        //    uploadableItem.Parts = FileUtils.GetItemParts(uploadableItem, (int)size);
        //    return uploadableItem;
        //}

        //private static List<UploadablePart> GetItemParts(UploadableItem item, int size)
        //{
        //    int chunkSize = FileUtils.GetChunkSize((long)size);
        //    int partsCount = FileUtils.GetPartsCount((long)size, chunkSize);
        //    List<UploadablePart> list = new List<UploadablePart>(partsCount);
        //    for (int i = 0; i < partsCount; i++)
        //    {
        //        UploadablePart uploadablePart = new UploadablePart(item, new int?(i), (long)(i * chunkSize), Math.Min((long)chunkSize, (long)size - (long)(i * chunkSize)));
        //        list.Add(uploadablePart);
        //    }
        //    item.IsSmallFile = (size < 10485760);
        //    return list;
        //}

        public static void Write(object syncRoot, string directoryName, string fileName, string str)
        {
            lock (syncRoot)
            {
                if (!Directory.Exists(GetFileName(directoryName)))
                {
                    Directory.CreateDirectory(GetFileName(directoryName));
                }

                using (var file = File.Open(Path.Combine(GetFileName(directoryName), fileName), FileMode.Append))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(str);
                    file.Write(bytes, 0, bytes.Length);
                }
            }
        }

        private static async Task WriteAsync(string directoryName, string fileName, string str)
        {
            StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(directoryName, CreationCollisionOption.OpenIfExists);
            StorageFile windowsRuntimeFile = await storageFolder.GetFileAsync(fileName);
            using (Stream stream = await windowsRuntimeFile.OpenStreamForWriteAsync())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(str);
                stream.Seek(0L, SeekOrigin.End);
                await stream.WriteAsync(bytes, 0, bytes.Length);
            }
        }

        public static void Clear(object syncRoot, string directoryName)
        {
            lock (syncRoot)
            {
                //string[] fileNames = userStoreForApplication.GetFileNames(Path.Combine(directoryName, "*.txt"));
                string[] fileNames = Directory.GetFiles(GetFileName(directoryName), "*.txt");
                for (int i = 0; i < fileNames.Length; i++)
                {
                    try
                    {
                        File.Delete(fileNames[i]);
                        //userStoreForApplication.DeleteFile(Path.Combine(directoryName, text));
                    }
                    catch (Exception e)
                    {
                        TLUtils.WriteException(e);
                    }
                }
            }
        }

        private static async Task ClearAsync(string directoryName)
        {
            StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(directoryName, CreationCollisionOption.OpenIfExists);
            IReadOnlyList<StorageFile> readOnlyList = await storageFolder.GetFilesAsync();
            using (IEnumerator<StorageFile> enumerator = readOnlyList.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    StorageFile current = enumerator.Current;
                    try
                    {
                        await current.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                    catch (Exception e)
                    {
                        TLUtils.WriteException(e);
                    }
                }
            }
        }

        public static void CopyLog(object syncRoot, string fromDirectoryName, string fromFileName, string toFileName, bool isEnabled)
        {
            lock (syncRoot)
            {
                if (File.Exists(GetFileName(Path.Combine(fromDirectoryName, fromFileName))))
                {
                    File.Copy(Path.Combine(fromDirectoryName, fromFileName), GetFileName(toFileName));
                }
                else
                {
                    using (var file = File.Open(GetFileName(toFileName), FileMode.Append))
                    {
                        string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                        byte[] bytes = Encoding.UTF8.GetBytes(string.Format("{0} {1}{2}", text, "Log.IsEnabled=" + isEnabled, Environment.NewLine));
                        file.Write(bytes, 0, bytes.Length);
                    }
                }
            }
        }

        private static async Task CopyLogAsync(object syncRoot, string fromDirectoryName, string fromFileName, string toFileName, bool isEnabled)
        {
            StorageFolder storageFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(fromDirectoryName, CreationCollisionOption.OpenIfExists);
            StorageFile storageFile = null;
            try
            {
                storageFile = await storageFolder.GetFileAsync(fromFileName);
            }
            catch (Exception)
            {
            }
            if (storageFile != null)
            {
                await storageFile.CopyAsync(ApplicationData.Current.LocalFolder, toFileName);
            }
            else
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                storageFile = await localFolder.CreateFileAsync(toFileName);
                using (Stream stream = await storageFile.OpenStreamForReadAsync())
                {
                    string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    byte[] bytes = Encoding.UTF8.GetBytes(string.Format("{0} {1}{2}", text, "Log.IsEnabled=" + isEnabled, Environment.NewLine));
                    stream.Seek(0L, SeekOrigin.End);
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }
            }
        }

        public static Stream GetLocalFileStreamForRead(string fileName)
        {
            return File.Open(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName), FileMode.OpenOrCreate);
        }

        public static Stream GetLocalFileStreamForWrite(string fileName)
        {
            string text = fileName + ".temp";
            return File.Open(Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName), FileMode.Create);
        }

        public static void SaveWithTempFile<T>(string fileName, T data) where T : TLObject
        {
            string text = fileName + ".temp";
            using (var file = File.Open(Path.Combine(ApplicationData.Current.LocalFolder.Path, text), FileMode.Create))
            {
                using (var to = new TLBinaryWriter(file))
                {
                    data.Write(to);
                }
            }
            File.Copy(Path.Combine(ApplicationData.Current.LocalFolder.Path, text), Path.Combine(ApplicationData.Current.LocalFolder.Path, fileName), true);
        }
    }

    //    public static class FileUtils
    //    {
    //        // TODO: files
    //        //        public static async Task MergePartsToFileAsync(Func<DownloadablePart, string> getPartName, IEnumerable<DownloadablePart> parts, string fileName)
    //        //        {
    //        //            using (var stream = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync(fileName, CreationCollisionOption.ReplaceExisting))
    //        //            {
    //        //                foreach (var part in parts)
    //        //                {
    //        //                    var partFileName = getPartName(part);
    //        //                    var partFile = await ApplicationData.Current.LocalFolder.GetFileAsync(partFileName);
    //        //                    using (var partStream = await partFile.OpenStreamForReadAsync())
    //        //                    {
    //        //                        // append
    //        //                        var buffer = new byte[partStream.Length];
    //        //                        stream.Seek(0, SeekOrigin.End);
    //        //                        await stream.WriteAsync(buffer, 0, buffer.Length);
    //        //                    }
    //        //                    await partFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
    //        //                }
    //        //            }
    //        //        }
    //        // TODO: files
    //        //        public static void MergePartsToFile(Func<DownloadablePart, string> getPartName, IEnumerable<DownloadablePart> parts, string fileName)
    //        //        {
    //        //#if WINDOWS_PHONE
    //        //            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
    //        //            {
    //        //                using (var stream = new IsolatedStorageFileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write, store))
    //        //                {
    //        //                    foreach (var part in parts)
    //        //                    {
    //        //                        var partFileName = getPartName(part);
    //        //                        using (var partStream = new IsolatedStorageFileStream(partFileName, FileMode.OpenOrCreate, FileAccess.Read, store))
    //        //                        {
    //        //                            var bytes = new byte[partStream.Length];
    //        //                            partStream.Read(bytes, 0, bytes.Length);
    //        //                            stream.Position = stream.Length;
    //        //                            stream.Write(bytes, 0, bytes.Length);
    //        //                        }
    //        //                        store.DeleteFile(partFileName);
    //        //                    }
    //        //                }
    //        //            }
    //        //#else
    //        //            MergePartsToFileAsync(getPartName, parts, fileName).RunSynchronously();
    //        //#endif       
    //        //        }

    //        public static bool Delete(object syncRoot, string fileName)
    //        {
    //            try
    //            {
    //                lock (syncRoot)
    //                {
    //#if WINDOWS_UWP
    //                    var getFileOperation = ApplicationData.Current.LocalFolder.TryGetItemAsync(fileName);
    //                    getFileOperation.AsTask().Wait();

    //                    var file = getFileOperation.GetResults() as StorageFile;
    //                    if (file != null)
    //                    {
    //                        Delete(file);
    //                    }
    //#elif WIN_RT
    //                    var getFileOperation = ApplicationData.Current.LocalFolder.GetFileAsync(fileName);

    //                    getFileOperation.AsTask().Wait();

    //                    var file = getFileOperation.GetResults();

    //                    Delete(file);
    //#elif WINDOWS_PHONE
    //                    var storage = IsolatedStorageFile.GetUserStoreForApplication();

    //                    if (storage.FileExists(fileName))
    //                    {
    //                        storage.DeleteFile(fileName);
    //                    }
    //#endif
    //                }

    //                return true;
    //            }
    //            catch (Exception e)
    //            {
    //                TLUtils.WriteLine("FILE ERROR: cannot delete " + fileName, LogSeverity.Error);
    //                TLUtils.WriteException(e);
    //            }

    //            return false;
    //        }

    //        public static void Copy(object syncRoot, string fileName, string destinationFileName)
    //        {
    //            try
    //            {
    //                lock (syncRoot)
    //                {
    //#if WIN_RT
    //                    var getFileOperation = ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
    //                    getFileOperation.AsTask().Wait();
    //                    var file = getFileOperation.GetResults();

    //                    var copyFileOperation = file.CopyAsync(ApplicationData.Current.LocalFolder, destinationFileName, NameCollisionOption.ReplaceExisting);
    //                    copyFileOperation.AsTask().Wait();
    //                    var destinationFile = copyFileOperation.GetResults();
    //#elif WINDOWS_PHONE
    //                    var file = IsolatedStorageFile.GetUserStoreForApplication();

    //                    file.CopyFile(fileName, destinationFileName, true);

    //#endif
    //                }
    //            }
    //            catch (Exception e)
    //            {
    //                TLUtils.WriteLine("DB ERROR: cannot copy " + fileName, LogSeverity.Error);
    //                TLUtils.WriteException(e);
    //            }
    //        }

    //        // TODO: files
    //        //public static void NotifyProgress(object itemsSyncRoot, IList<UploadableItem> items, UploadablePart part, ITelegramEventAggregator eventAggregator)
    //        //{
    //        //    bool isComplete = false;
    //        //    bool isCanceled;
    //        //    var progress = 0.0;
    //        //    lock (itemsSyncRoot)
    //        //    {
    //        //        part.Status = PartStatus.Processed;
    //        //        isCanceled = part.ParentItem.Canceled;
    //        //        if (!isCanceled)
    //        //        {
    //        //            isComplete = part.ParentItem.Parts.All(x => x.Status == PartStatus.Processed);
    //        //            if (!isComplete)
    //        //            {
    //        //                double uploadedCount = part.ParentItem.Parts.Count(x => x.Status == PartStatus.Processed);
    //        //                double totalCount = part.ParentItem.Parts.Count;
    //        //                progress = uploadedCount / totalCount;
    //        //            }
    //        //            else
    //        //            {
    //        //                items.Remove(part.ParentItem);
    //        //            }
    //        //        }
    //        //    }

    //        //    if (!isCanceled)
    //        //    {
    //        //        if (isComplete)
    //        //        {
    //        //            SwitchIdleDetectionMode(true);
    //        //            Execute.BeginOnThreadPool(() => eventAggregator.Publish(part.ParentItem));
    //        //        }
    //        //        else
    //        //        {
    //        //            if (part.ParentItem.FileNotFound)
    //        //            {
    //        //                return;
    //        //            }

    //        //            var args = new UploadProgressChangedEventArgs(part.ParentItem, progress);
    //        //            Execute.BeginOnThreadPool(() => eventAggregator.Publish(args));
    //        //        }
    //        //    }
    //        //}

    //        public static void SwitchIdleDetectionMode(bool enabled)
    //        {
    //#if WINDOWS_PHONE
    //            var mode = enabled ? IdleDetectionMode.Enabled : IdleDetectionMode.Disabled;
    //            try
    //            {
    //                PhoneApplicationService.Current.UserIdleDetectionMode = mode;
    //            }
    //            catch (Exception ex)
    //            {
    //                Execute.ShowDebugMessage("UploadVideoFileManager UserIdleDetectionMode=" + mode + Environment.NewLine + ex);
    //            }
    //#endif
    //        }

    //        public static int GetChunkSize(long totalSize)
    //        {
    //            int chunkSize = 32 * 1024; // 32Kb
    //            if (totalSize > 256 * 1024 * Constants.MaximumChunksCount)
    //            {
    //                chunkSize = 512 * 1024;
    //            }
    //            else if (totalSize > 128 * 1024 * Constants.MaximumChunksCount)
    //            {
    //                chunkSize = 256 * 1024;
    //            }
    //            else if (totalSize > 64 * 1024 * Constants.MaximumChunksCount)
    //            {
    //                chunkSize = 128 * 1024;
    //            }
    //            else if (totalSize > 32 * 1024 * Constants.MaximumChunksCount)
    //            {
    //                chunkSize = 64 * 1024;
    //            }

    //            return chunkSize;
    //        }

    //        public static int GetPartsCount(long totalSize, int chunkSize)
    //        {
    //            return (int)(totalSize / chunkSize + (totalSize % chunkSize > 0 ? 1 : 0));
    //        }

    //        public static int GetLocalFileLength(string fileName)
    //        {
    //#if WINDOWS_PHONE
    //            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
    //            {
    //                if (store.FileExists(fileName))
    //                {
    //                    using (var file = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
    //                    {
    //                        return (int)file.Length;
    //                    }
    //                }
    //            }

    //            return -1;
    //#else
    //            var file = GetLocalFile(fileName);

    //            if (file != null)
    //            {
    //                var getBasicPropertiesOperation = file.GetBasicPropertiesAsync();
    //                getBasicPropertiesOperation.AsTask().Wait();

    //                var basicProperties = getBasicPropertiesOperation.GetResults();

    //                return (int)basicProperties.Size;
    //            }

    //            return -1;
    //#endif
    //        }

    //        private static StorageFile GetLocalFile(string fileName)
    //        {
    //            StorageFile file = null;
    //            try
    //            {
    //                var getFileOperation = ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
    //                getFileOperation.AsTask().Wait();
    //                file = getFileOperation.GetResults();
    //            }
    //            catch (Exception ex)
    //            {

    //            }

    //            return file;
    //        }

    //        private static StorageFile CreateLocalFile(string fileName)
    //        {
    //            var createFileOperation = ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
    //            createFileOperation.AsTask().Wait();
    //            var file = createFileOperation.GetResults();

    //            return file;
    //        }

    //        private static void Delete(StorageFile file)
    //        {
    //            if (file != null)
    //            {
    //                var deleteFileOperation = file.DeleteAsync();
    //                deleteFileOperation.AsTask().Wait();
    //                deleteFileOperation.GetResults();
    //            }
    //        }

    //        // TODO: files
    //        //        public static void CheckMissingPart(object syncRoot, DownloadablePart part, string partName)
    //        //        {
    //        //#if WINDOWS_PHONE

    //        //            if (part.Offset.Value == 0)
    //        //            {
    //        //                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
    //        //                {
    //        //                    if (store.FileExists(partName))
    //        //                    {
    //        //                        store.DeleteFile(partName);
    //        //                    }
    //        //                }
    //        //            }

    //        //            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
    //        //            {
    //        //                if (store.FileExists(partName))
    //        //                {
    //        //                    store.DeleteFile(partName);
    //        //                }

    //        //                using (var stream = new IsolatedStorageFileStream(partName, FileMode.OpenOrCreate, FileAccess.Write, store))
    //        //                {
    //        //                    var data = part.File.Bytes.Data;
    //        //                    part.File.Bytes = new string();
    //        //                    stream.Position = stream.Length;
    //        //                    stream.Write(data, 0, data.Length);

    //        //                    if (data.Length < part.Limit.Value && (part.Number + 1) != part.ParentItem.Parts.Count)
    //        //                    {
    //        //                        lock (syncRoot)
    //        //                        {
    //        //                            var complete = part.ParentItem.Parts.All(x => x.Status == PartStatus.Processed);
    //        //                            if (!complete)
    //        //                            {
    //        //                                var emptyBufferSize = part.Limit.Value - data.Length;
    //        //                                var position = stream.Position;

    //        //                                var missingPart = new DownloadablePart(part.ParentItem, new int?((int)position),
    //        //                                    new int?(emptyBufferSize), -part.Number);

    //        //                                var currentItemIndex = part.ParentItem.Parts.IndexOf(part);
    //        //                                part.ParentItem.Parts.Insert(currentItemIndex + 1, missingPart);
    //        //                            }
    //        //                        }
    //        //                    }
    //        //                }
    //        //            }
    //        //#else
    //        //            CheckMissingPartAsync(syncRoot, part, partName).RunSynchronously();
    //        //#endif
    //        //        }

    //        // TODO: files
    //        //private static async Task CheckMissingPartAsync(object syncRoot, DownloadablePart part, string partName)
    //        //{
    //        //    try
    //        //    {
    //        //        var file = await ApplicationData.Current.LocalFolder.GetFileAsync(partName);
    //        //        await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
    //        //    }
    //        //    catch (Exception ex)
    //        //    {

    //        //    }

    //        //    using (var stream = await ApplicationData.Current.LocalFolder.OpenStreamForWriteAsync(partName, CreationCollisionOption.ReplaceExisting))
    //        //    {
    //        //        var data = part.File.Bytes;
    //        //        part.File.Bytes = new byte[0];
    //        //        stream.Position = stream.Length;
    //        //        stream.Write(data, 0, data.Length);

    //        //        if (data.Length < part.Limit && (part.Number + 1) != part.ParentItem.Parts.Count)
    //        //        {
    //        //            lock (syncRoot)
    //        //            {
    //        //                var complete = part.ParentItem.Parts.All(x => x.Status == PartStatus.Processed);
    //        //                if (!complete)
    //        //                {
    //        //                    var emptyBufferSize = part.Limit - data.Length;
    //        //                    var position = stream.Position;

    //        //                    var missingPart = new DownloadablePart(part.ParentItem, (int)position, emptyBufferSize, -part.Number);

    //        //                    var currentItemIndex = part.ParentItem.Parts.IndexOf(part);
    //        //                    part.ParentItem.Parts.Insert(currentItemIndex + 1, missingPart);
    //        //                }
    //        //            }
    //        //        }
    //        //    }
    //        //}

    //        public static void WriteBytes(string fileName, byte[] bytes)
    //        {
    //#if WINDOWS_PHONE
    //            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
    //            {
    //                using (var fileStream = store.OpenFile(fileName, FileMode.Create, FileAccess.Write))
    //                {
    //                    fileStream.Write(bytes, 0, bytes.Length);
    //                }
    //            }
    //#else
    //            var file = CreateLocalFile(fileName);
    //            WriteBytes(file, bytes);
    //#endif
    //        }

    //#if WIN_RT
    //        private static void WriteBytes(StorageFile file, byte[] data)
    //        {
    //            var writeBytesOperation = FileIO.WriteBytesAsync(file, data);
    //            writeBytesOperation.AsTask().Wait();
    //            writeBytesOperation.GetResults();
    //        }
    //#endif

    //        public static byte[] ReadBytes(string fileName, long position, long length)
    //        {
    //#if WINDOWS_PHONE
    //            byte[] bytes = null;
    //            using (var storage = IsolatedStorageFile.GetUserStoreForApplication())
    //            {
    //                using (var stream = storage.OpenFile(fileName, FileMode.Open, FileAccess.Read))
    //                {
    //                    stream.Position = position;
    //                    bytes = new byte[length];
    //                    stream.Read(bytes, 0, (int)length);
    //                }
    //            }

    //            return bytes;
    //#else
    //            var file = GetLocalFile(fileName);
    //            return file != null? ReadBytesAsync(file, position, length).Result : null;
    //#endif
    //        }

    //        private static async Task<byte[]> ReadBytesAsync(StorageFile file, long position, long length)
    //        {
    //            using (var fs = await file.OpenAsync(FileAccessMode.Read))
    //            {
    //                using (var inStream = fs.GetInputStreamAt((ulong)position))
    //                {
    //                    using (var reader = new DataReader(inStream))
    //                    {
    //                        await reader.LoadAsync((uint)length);
    //                        var data = new byte[length];
    //                        reader.ReadBytes(data);
    //                        reader.DetachStream();
    //                        return data;
    //                    }
    //                }
    //            }
    //        }

    //#if WP8
    //        public static async Task<Tuple<bool, byte[]>> FillBuffer(IStorageFile file, UploadablePart part)
    //        {
    //            try
    //            {
    //                if (part.ParentItem.FileNotFound)
    //                {
    //                    return new Tuple<bool, byte[]>(false, null);
    //                }

    //                using (var stream = await file.OpenAsync(FileAccessMode.Read))
    //                {
    //                    using (var inStream = stream.GetInputStreamAt((ulong) part.Position))
    //                    {
    //                        var bytes = new byte[part.Count];
    //                        using (var reader = new DataReader(inStream))
    //                        {
    //                            await reader.LoadAsync((uint) bytes.Length);
    //                            reader.ReadBytes(bytes);
    //                        }

    //                        // encrypting part
    //                        if (part.ParentItem.Key != null
    //                            && part.ParentItem.IV != null)
    //                        {
    //                            var key = part.ParentItem.Key;
    //                            var iv = part.FilePart.Value == 0 ? part.ParentItem.IV : part.IV;

    //                            if (iv == null)
    //                            {
    //                                return new Tuple<bool, byte[]>(true, null);
    //                            }

    //                            byte[] nextIV;

    //                            var encryptedBytes = Utils.AesIge(bytes, key.Data, iv.Data, true, out nextIV);
    //                            bytes = encryptedBytes;

    //                            var nextPartId = part.FilePart.Value + 1;
    //                            if (part.ParentItem.Parts.Count > nextPartId)
    //                            {
    //                                part.ParentItem.Parts[nextPartId].IV = string.FromBigEndianData(nextIV);
    //                            }
    //                        }

    //                        return new Tuple<bool, byte[]>(true, bytes);
    //                    }
    //                }
    //            }
    //            catch (FileNotFoundException ex)
    //            {
    //                Execute.ShowDebugMessage("FillBuffer FileNotFoundException\n" + ex);
    //                return new Tuple<bool, byte[]>(false, null);
    //            }
    //            catch (Exception ex)
    //            {
    //                Execute.ShowDebugMessage("FillBuffer Exception\n" + ex);
    //                return new Tuple<bool, byte[]>(true, null);
    //            }
    //        }

    //        public static UploadableItem GetUploadableItem(long? fileId, TLObject owner, StorageFile file)
    //        {
    //            return GetUploadableItem(fileId, owner, file, null, null);
    //        }

    //        public static UploadableItem GetUploadableItem(long? fileId, TLObject owner, StorageFile file, string key, string iv)
    //        {
    //            var item = new UploadableItem(fileId, owner, file, key, iv);

    //            var task = file.GetBasicPropertiesAsync().AsTask();
    //            task.Wait();
    //            var propertie = task.Result;
    //            var size = propertie.Size;
    //            item.Parts = GetItemParts(item, (int)size);
    //            return item;
    //        }

    //        private static List<UploadablePart> GetItemParts(UploadableItem item, int size)
    //        {
    //            var chunkSize = GetChunkSize(size);
    //            var partsCount = GetPartsCount(size, chunkSize);
    //            var parts = new List<UploadablePart>(partsCount);

    //            for (var i = 0; i < partsCount; i++)
    //            {
    //                var part = new UploadablePart(item, new int?(i), i * chunkSize, Math.Min(chunkSize, (long)size - i * chunkSize));
    //                parts.Add(part);
    //            }

    //            item.IsSmallFile = size < Constants.SmallFileMaxSize; // size < chunkSize;

    //            return parts;
    //        }
    //#endif

    //        public static void Write(object syncRoot, string directoryName, string fileName, string str)
    //        {
    //#if WINDOWS_PHONE
    //            lock (syncRoot)
    //            {
    //                using (var store = IsolatedStorageFile.GetUserStoreForApplication())
    //                {
    //                    if (!store.DirectoryExists(directoryName))
    //                    {
    //                        store.CreateDirectory(directoryName);
    //                    }

    //                    using (var file = store.OpenFile(Path.Combine(directoryName, fileName), FileMode.Append))
    //                    {
    //                        var bytes = Encoding.UTF8.GetBytes(str);
    //                        file.Write(bytes, 0, bytes.Length);
    //                    }
    //                }
    //            }
    //#else
    //            lock (syncRoot)
    //            {
    //                var task = Task.Run(async () => await WriteAsync(directoryName, fileName, str));
    //                task.Wait();
    //            }
    //#endif
    //        }

    //        private static async Task WriteAsync(string directoryName, string fileName, string str)
    //        {
    //            var logFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(directoryName, CreationCollisionOption.OpenIfExists);

    //            var logFile = await logFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
    //            using (var stream = await logFile.OpenStreamForWriteAsync())
    //            {
    //                // append
    //                var buffer = Encoding.UTF8.GetBytes(str);
    //                stream.Seek(0, SeekOrigin.End);
    //                await stream.WriteAsync(buffer, 0, buffer.Length);
    //            }
    //        }

    //        public static void Clear(object syncRoot, string directoryName)
    //        {
    //#if WINDOWS_PHONE
    //                lock (syncRoot)
    //                {
    //                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
    //                    {
    //                        var fileNames = store.GetFileNames(Path.Combine(directoryName, "*.txt"));
    //                        foreach (var fileName in fileNames)
    //                        {
    //                            try
    //                            {
    //                                store.DeleteFile(Path.Combine(directoryName, fileName));
    //                            }
    //                            catch (Exception ex)
    //                            {
    //                                TLUtils.WriteException(ex);
    //                            }
    //                        }
    //                    }
    //                }
    //#else
    //            lock (syncRoot)
    //            {
    //                ClearAsync(directoryName).RunSynchronously();
    //            }
    //#endif
    //        }

    //        private static async Task ClearAsync(string directoryName)
    //        {
    //            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(directoryName, CreationCollisionOption.OpenIfExists);

    //            var files = await folder.GetFilesAsync();
    //            foreach (var file in files)
    //            {
    //                try
    //                {
    //                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
    //                }
    //                catch (Exception ex)
    //                {
    //                    TLUtils.WriteException(ex);
    //                }
    //            }
    //        }

    //        public static void CopyLog(object syncRoot, string fromDirectoryName, string fromFileName, string toFileName, bool isEnabled)
    //        {
    //#if WINDOWS_PHONE
    //                lock (syncRoot)
    //                {
    //                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
    //                    {
    //                        if (store.FileExists(Path.Combine(fromDirectoryName, fromFileName)))
    //                        {
    //                            store.CopyFile(Path.Combine(fromDirectoryName, fromFileName), toFileName);
    //                        }
    //                        else
    //                        {
    //                            using (var file = store.OpenFile(toFileName, FileMode.Append))
    //                            {
    //                                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    //                                var bytes = Encoding.UTF8.GetBytes(string.Format("{0} {1}{2}", timestamp, "Log.IsEnabled=" + isEnabled, Environment.NewLine));
    //                                file.Write(bytes, 0, bytes.Length);
    //                            }
    //                        }
    //                    }
    //                }
    //#else
    //            lock (syncRoot)
    //            {
    //                CopyLogAsync(syncRoot, fromDirectoryName, fromFileName, toFileName, isEnabled).RunSynchronously();
    //            }
    //#endif
    //        }

    //        private static async Task CopyLogAsync(object syncRoot, string fromDirectoryName, string fromFileName, string toFileName, bool isEnabled)
    //        {
    //            var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(fromDirectoryName, CreationCollisionOption.OpenIfExists);
    //            StorageFile logFile = null;
    //            try
    //            {
    //                logFile = await folder.GetFileAsync(fromFileName);
    //            }
    //            catch (Exception ex)
    //            {

    //            }

    //            if (logFile != null)
    //            {
    //                await logFile.CopyAsync(ApplicationData.Current.LocalFolder, toFileName);
    //            }
    //            else
    //            {
    //                var logFolder = ApplicationData.Current.LocalFolder;

    //                logFile = await logFolder.CreateFileAsync(toFileName);
    //                using (var stream = await logFile.OpenStreamForReadAsync())
    //                {
    //                    // append
    //                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    //                    var buffer = Encoding.UTF8.GetBytes(string.Format("{0} {1}{2}", timestamp, "Log.IsEnabled=" + isEnabled, Environment.NewLine));
    //                    stream.Seek(0, SeekOrigin.End);
    //                    await stream.WriteAsync(buffer, 0, buffer.Length);
    //                }
    //            }
    //        }

    //        public static Stream GetLocalFileStreamForRead(string fileName)
    //        {
    //#if WINDOWS_PHONE
    //            var store = IsolatedStorageFile.GetUserStoreForApplication();

    //            return new IsolatedStorageFileStream(fileName, FileMode.OpenOrCreate, store);
    //#else
    //            var openFileOperation = ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
    //            openFileOperation.AsTask().Wait();
    //            var file = openFileOperation.GetResults();

    //            return file.OpenStreamForReadAsync().Result;
    //#endif
    //        }

    //        public static Stream GetLocalFileStreamForWrite(string fileName)
    //        {
    //#if WINDOWS_PHONE
    //            var store = IsolatedStorageFile.GetUserStoreForApplication();

    //            return new IsolatedStorageFileStream(fileName, FileMode.Create, store);
    //#else
    //            var openFileOperation = ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
    //            openFileOperation.AsTask().Wait();
    //            var file = openFileOperation.GetResults();

    //            return file.OpenStreamForWriteAsync().Result;
    //#endif
    //        }

    //        public static void SaveWithTempFile<T>(string fileName, T data) where T : TLObject
    //        {
    //#if WINDOWS_PHONE
    //            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
    //            {
    //                var tempFileName = fileName + ".temp";
    //                using (var fileStream = new IsolatedStorageFileStream(tempFileName, FileMode.Create, store))
    //                {
    //                    data.ToStream(fileStream);
    //                }
    //                //var stopwatch = Stopwatch.StartNew();
    //                store.CopyFile(tempFileName, fileName, true);
    //                //store.DeleteFile(fileName);
    //                //store.MoveFile(tempFileName, fileName);
    //                //store.DeleteFile(tempFileName);
    //                //WritePerformance("MoveFile time: " + stopwatch.Elapsed);
    //            }
    //#else


    //            var tempFileName = fileName + ".temp";
    //            var openFileOperation = ApplicationData.Current.LocalFolder.CreateFileAsync(tempFileName, CreationCollisionOption.OpenIfExists);
    //            openFileOperation.AsTask().Wait();
    //            var tempFile = openFileOperation.GetResults();

    //            using (var fileStream = tempFile.OpenStreamForWriteAsync().Result)
    //            {
    //                using (var to = new TLBinaryWriter(fileStream))
    //                {
    //                    data.Write(to);
    //                }
    //            }

    //            var copyOperation = tempFile.CopyAsync(ApplicationData.Current.LocalFolder, fileName, NameCollisionOption.ReplaceExisting);
    //            copyOperation.AsTask().Wait();
    //            copyOperation.GetResults();
    //#endif
    //        }
    //    }
}
