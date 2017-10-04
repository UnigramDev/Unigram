using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Unigram.Native;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStorageViewModel : UnigramViewModelBase
    {
        public SettingsStorageViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            await UpdateCacheSizeAsync(resetInitialCacheSize: true, updateDetailedCacheSizes: true);
        }

        private long _cacheSize, _initialCacheSize, _imagesCacheSize, _videosCacheSize, _otherFilesCacheSize;
        private double _percentage;
        private bool _taskCompleted;

        public long InitialCacheSize
        {
            get
            {
                return _initialCacheSize;
            }
            set
            {
                Set(ref _initialCacheSize, value);
            }
        }

        public double Percentage
        {
            get
            {
                return _percentage;
            }
            set
            {
                Set(ref _percentage, value);
            }
        }

        public long CacheSize
        {
            get
            {
                return _cacheSize;
            }
            set
            {
                Set(ref _cacheSize, value);
            }
        }

        public long ImagesCacheSize
        {
            get
            {
                return _imagesCacheSize;
            }
            set
            {
                Set(ref _imagesCacheSize, value);
            }
        }

        public long VideosCacheSize
        {
            get
            {
                return _videosCacheSize;
            }
            set
            {
                Set(ref _videosCacheSize, value);
            }
        }

        public long OtherFilesCacheSize
        {
            get
            {
                return _otherFilesCacheSize;
            }
            set
            {
                Set(ref _otherFilesCacheSize, value);
            }
        }

        public bool TaskCompleted
        {
            get
            {
                return _taskCompleted;
            }
            set
            {
                Set(ref _taskCompleted, value);
            }
        }

        private async Task UpdateCacheSizeAsync(bool resetInitialCacheSize, bool updateDetailedCacheSizes)
        {
            CacheSize = 0;
            if (resetInitialCacheSize)
                InitialCacheSize = 0;

            try
            {
                var cacheSize = NativeUtils.GetDirectorySize(FileUtils.GetTempFileName(string.Empty));
                CacheSize = cacheSize;
                if (resetInitialCacheSize)
                    InitialCacheSize = cacheSize;
                Percentage = InitialCacheSize > 0 ? Math.Round((double)(CacheSize * 100) / InitialCacheSize, 1) : 0.0D;
            }
            catch { }
            finally
            {
                if (updateDetailedCacheSizes)
                {
                    var files = await this.RetrieveCacheFilesAsync();

                    UpdateCacheTypes(files);
                }
            }
        }

        public void UpdateCacheTypes(IReadOnlyList<StorageFile> files)
        {
            if (files == null || files.Count == 0)
            {
                ImagesCacheSize = 0;
                VideosCacheSize = 0;
                OtherFilesCacheSize = 0;
                return;
            }

            ImagesCacheSize = files.OfImageType().Sum(f => (long)f.GetFileSize());
            VideosCacheSize = files.OfVideoType().Sum(f => (long)f.GetFileSize());
            OtherFilesCacheSize = files.OfOtherTypes().Sum(f => (long)f.GetFileSize());
        }

        public RelayCommand ClearCacheCommand => new RelayCommand(ClearCacheExecute);
        private async void ClearCacheExecute()
        {
            IsLoading = true;
            TaskCompleted = false;

            var files = await this.RetrieveCacheFilesAsync();

            foreach (var file in files)
            {
                try
                {
                    await file.DeleteAsync();
                    await UpdateCacheSizeAsync(resetInitialCacheSize: false, updateDetailedCacheSizes: false);
                }
                catch { }
            }

            IsLoading = false;

            await UpdateCacheSizeAsync(resetInitialCacheSize: true, updateDetailedCacheSizes: true);
            TaskCompleted = true;
        }

        private async Task<IReadOnlyList<StorageFile>> RetrieveCacheFilesAsync()
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(FileUtils.GetTempFileName(string.Empty));
            var queryOptions = new QueryOptions
            {
                FolderDepth = FolderDepth.Deep
            };

            var query = folder.CreateFileQueryWithOptions(queryOptions);
            var result = await query.GetFilesAsync();

            return result;
        }
    }

    public static class StorageFileExtensions
    {
        private const string JpegExtension = "jpg";
        private const string PngExtension = "png";
        private const string Mp4Extension = "mp4";

        public static IEnumerable<StorageFile> OfImageType(this IEnumerable<StorageFile> storageFiles)
        {
            if (storageFiles == null)
                throw new ArgumentNullException(nameof(storageFiles));

            return storageFiles.Where(f => f.FileType.ToLower().Contains(JpegExtension)
                || f.FileType.ToLower().Contains(PngExtension));
        }

        public static IEnumerable<StorageFile> OfVideoType(this IEnumerable<StorageFile> storageFiles)
        {
            if (storageFiles == null)
                throw new ArgumentNullException(nameof(storageFiles));

            return storageFiles.Where(f => f.FileType.ToLower().Contains(Mp4Extension));
        }

        public static IEnumerable<StorageFile> OfOtherTypes(this IEnumerable<StorageFile> storageFiles)
        {
            if (storageFiles == null)
                throw new ArgumentNullException(nameof(storageFiles));

            return storageFiles.Where(f => !f.FileType.ToLower().Contains(JpegExtension)
                && !f.FileType.ToLower().Contains(PngExtension)
                && !f.FileType.ToLower().Contains(Mp4Extension));
        }

        public static ulong GetFileSize(this StorageFile storageFile)
        {
            var task = storageFile.GetBasicPropertiesAsync().AsTask();
            task.Wait();
            return task.Result.Size;
        }
    }
}