using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
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
                var folder = await ApplicationData.Current.LocalFolder.GetFolderAsync("temp");
                CacheSize = NativeUtils.GetDirectorySize(folder.Path);
            }
            catch { }
        }

        public RelayCommand ClearCacheCommand => new RelayCommand(ClearCacheExecute);
        private async void ClearCacheExecute()
        {
            await ApplicationData.Current.LocalFolder.CreateFolderAsync("temp", CreationCollisionOption.ReplaceExisting);
            await UpdateCacheSizeAsync();
        }
    }
}
