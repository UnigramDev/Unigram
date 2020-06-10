using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Popups;
using Unigram.Views.Settings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsStorageViewModel : TLViewModelBase
    {
        public SettingsStorageViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            ChangeTtlCommand = new RelayCommand(ChangeTtlExecute);
            ClearCacheCommand = new RelayCommand(ClearCacheExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            IsLoading = true;

            ProtoService.Send(new GetStorageStatisticsFast(), result =>
            {
                if (result is StorageStatisticsFast stats)
                {
                    BeginOnUIThread(() => StatisticsFast = stats);
                }
            });

            ProtoService.Send(new GetStorageStatistics(25), result =>
            {
                if (result is StorageStatistics stats)
                {
                    BeginOnUIThread(() => Statistics = stats);
                }
            });

            TaskCompleted = true;

            return Task.CompletedTask;
        }

        private static string[] ExcludedFileNames = new[] { Constants.WallpaperFileName };

        public int FilesTtl
        {
            get
            {
                var enabled = CacheService.Options.UseStorageOptimizer;
                var ttl = CacheService.Options.StorageMaxTimeFromLastAccess;

                return CacheService.Options.UseStorageOptimizer ? ttl / 60 / 60 / 24 : 0;
            }
            //set
            //{
            //    Settings.FilesTtl = value;
            //    RaisePropertyChanged();
            //}
        }

        private StorageStatisticsFast _statisticsFast;
        public StorageStatisticsFast StatisticsFast
        {
            get
            {
                return _statisticsFast;
            }
            set
            {
                Set(ref _statisticsFast, value);
            }
        }

        private StorageStatistics _statistics;
        public StorageStatistics Statistics
        {
            get
            {
                return _statistics;
            }
            set
            {
                Set(ref _statistics, value);
                ProcessTotal(value);
            }
        }

        private StorageStatisticsByChat _totalStatistics;
        public StorageStatisticsByChat TotalStatistics
        {
            get
            {
                return _totalStatistics;
            }
            set
            {
                Set(ref _totalStatistics, value);
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

        public RelayCommand ClearCacheCommand { get; }
        private void ClearCacheExecute()
        {
            var statistics = _totalStatistics;
            if (statistics == null)
            {
                return;
            }

            Clear(statistics);
        }

        public async void Clear(StorageStatisticsByChat byChat)
        {
            if (byChat == null || byChat.ByFileType.IsEmpty())
            {
                return;
            }

            var dialog = new SettingsStorageOptimizationPage(ProtoService, byChat);

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var types = dialog.SelectedItems ?? new FileType[0];
            if (types.IsEmpty())
            {
                return;
            }

            var chatIds = new long[0];
            var excludedChatIds = new long[0];

            if (byChat.ChatId != 0)
            {
                chatIds = new[] { byChat.ChatId };
            }
            else if (byChat != _totalStatistics)
            {
                excludedChatIds = _statistics.ByChat.Select(x => x.ChatId).Where(x => x != 0).ToArray();
            }

            IsLoading = true;
            TaskCompleted = false;

            var response = await ProtoService.SendAsync(new OptimizeStorage(long.MaxValue, 0, int.MaxValue, 0, types, chatIds, excludedChatIds, false, 25));
            if (response is StorageStatistics statistics)
            {
                Statistics = statistics;
            }

            IsLoading = false;
            TaskCompleted = true;
        }

        private void ProcessTotal(StorageStatistics value)
        {
            var result = new StorageStatisticsByChat();
            result.ByFileType = new List<StorageStatisticsByFileType>();

            foreach (var chat in value.ByChat)
            {
                result.Count += chat.Count;
                result.Size += chat.Size;

                foreach (var type in chat.ByFileType)
                {
                    var already = result.ByFileType.FirstOrDefault(x => x.FileType.TypeEquals(type.FileType));
                    if (already == null)
                    {
                        already = new StorageStatisticsByFileType(type.FileType, 0, 0);
                        result.ByFileType.Add(already);
                    }

                    already.Count += type.Count;
                    already.Size += type.Size;
                }
            }

            TotalStatistics = result;
            IsLoading = false;
        }

        public RelayCommand ChangeTtlCommand { get; }
        private async void ChangeTtlExecute()
        {
            var enabled = CacheService.Options.UseStorageOptimizer;
            var ttl = CacheService.Options.StorageMaxTimeFromLastAccess;

            var items = new[]
            {
                new SelectRadioItem(3, Locale.Declension("Days", 3), enabled && ttl == 3 * 60 * 60 * 24),
                new SelectRadioItem(7, Locale.Declension("Weeks", 1), enabled && ttl == 7 * 60 * 60 * 24),
                new SelectRadioItem(30, Locale.Declension("Months", 1), enabled && ttl == 30 * 60 * 60 * 24),
                new SelectRadioItem(0, Strings.Resources.KeepMediaForever, !enabled)
            };

            var dialog = new SelectRadioPopup(items);
            dialog.Title = Strings.Resources.KeepMedia;
            dialog.PrimaryButtonText = Strings.Resources.OK;
            dialog.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary && dialog.SelectedIndex is int index)
            {
                CacheService.Options.StorageMaxTimeFromLastAccess = index * 60 * 60 * 24;
                CacheService.Options.UseStorageOptimizer = index > 0;

                RaisePropertyChanged(() => FilesTtl);
            }
        }
    }
}