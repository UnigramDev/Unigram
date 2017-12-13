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
            ClearCacheCommand = new RelayCommand(ClearCacheExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            UpdateCacheSize(resetInitialCacheSize: true, updateDetailedCacheSizes: true);
            TaskCompleted = true;

            return Task.CompletedTask;
        }

        private static string[] ExcludedFileNames = new[] { Constants.WallpaperFileName };

        private long _initialCacheSize;
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

        private double _percentage;
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

        private long _cacheSize;
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

        private long _imagesCacheSize;
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

        private long _videosCacheSize;
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

        private long _otherFilesCacheSize;
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

        private bool _taskCompleted;
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

        private void UpdateCacheSize(bool resetInitialCacheSize, bool updateDetailedCacheSizes)
        {
            CacheSize = 0;

            if (resetInitialCacheSize)
            {
                InitialCacheSize = 0;
            }

            try
            {
                var cacheSize = NativeUtils.GetDirectorySize(FileUtils.GetTempFileName(string.Empty));
                CacheSize = cacheSize;

                if (resetInitialCacheSize)
                {
                    InitialCacheSize = cacheSize;
                }

                Percentage = InitialCacheSize > 0 ? Math.Round((double)(CacheSize * 100) / InitialCacheSize, 1) : 0.0D;
            }
            catch { }
            finally
            {
                if (updateDetailedCacheSizes)
                {
                    var filter = Constants.MediaTypes;
                    var images = 0L;
                    var videos = 0L;

                    for (int i = 0; i < filter.Length; i++)
                    {
                        if (Constants.PhotoTypes.Contains(filter[i]))
                        {
                            images += NativeUtils.GetDirectorySize(FileUtils.GetTempFileName(string.Empty), "\\*" + filter[i]);
                        }
                        else
                        {
                            videos += NativeUtils.GetDirectorySize(FileUtils.GetTempFileName(string.Empty), "\\*" + filter[i]);
                        }
                    }

                    ImagesCacheSize = images;
                    VideosCacheSize = videos;
                    OtherFilesCacheSize = Math.Max(_cacheSize - images - videos, 0);
                }
            }
        }

        public RelayCommand ClearCacheCommand { get; }
        private async void ClearCacheExecute()
        {
            IsLoading = true;
            TaskCompleted = false;

            await Task.Run(() =>
            {
                NativeUtils.CleanDirectory(FileUtils.GetTempFileName(string.Empty), 0);
            });

            IsLoading = false;

            UpdateCacheSize(resetInitialCacheSize: true, updateDetailedCacheSizes: true);
            TaskCompleted = true;
        }
    }
}