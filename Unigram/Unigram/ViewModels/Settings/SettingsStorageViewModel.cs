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
using Unigram.Controls;
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
            await UpdateCacheSizeAsync();
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

        private async Task UpdateCacheSizeAsync()
        {
            CacheSize = 0;

            try
            {
                CacheSize = NativeUtils.GetDirectorySize(FileUtils.GetTempFileName(string.Empty));
            }
            catch { }
        }

        public RelayCommand ClearCacheCommand => new RelayCommand(ClearCacheExecute);
        private async void ClearCacheExecute()
        {
            IsLoading = true;

            var folder = await StorageFolder.GetFolderFromPathAsync(FileUtils.GetTempFileName(string.Empty));
            var queryOptions = new QueryOptions();
            queryOptions.FolderDepth = FolderDepth.Deep;

            var query = folder.CreateFileQueryWithOptions(queryOptions);
            var result = await query.GetFilesAsync();

            foreach (var file in result)
            {
                try
                {
                    //await file.DeleteAsync();
                    NativeUtils.Delete(file.Path);
                    await UpdateCacheSizeAsync();
                }
                catch { }
            }

            IsLoading = false;

            await UpdateCacheSizeAsync();
            await TLMessageDialog.ShowAsync("Done", "Done", "Done");
        }
    }
}
