//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsStorageViewModel : ViewModelBase
    {
        public SettingsStorageViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chats = new List<StorageStatisticsByChat>(10);

            for (int i = 0; i < 10; i++)
            {
                chats.Add(new StorageStatisticsByChat(0, 0, 0, null));
            }

            _statistics = new StorageStatistics(0, 0, chats);
            RaisePropertyChanged(nameof(Statistics));

            IsLoading = true;

            ClientService.Send(new GetStorageStatisticsFast(), result =>
            {
                if (result is StorageStatisticsFast stats)
                {
                    BeginOnUIThread(() => StatisticsFast = stats);
                }
            });

            ClientService.Send(new GetStorageStatistics(25), result =>
            {
                if (result is StorageStatistics stats)
                {
                    BeginOnUIThread(() => Statistics = stats);
                }
            });

            TaskCompleted = true;

            return Task.CompletedTask;
        }

        public int KeepMedia
        {
            get
            {
                var enabled = ClientService.Options.UseStorageOptimizer;
                var ttl = (int)ClientService.Options.StorageMaxTimeFromLastAccess;

                return enabled ? ttl / 60 / 60 / 24 : 0;
            }
            set
            {
                ClientService.Options.StorageMaxTimeFromLastAccess =
                    Settings.Diagnostics.StorageMaxTimeFromLastAccess = value * 60 * 60 * 24;
                ClientService.Options.UseStorageOptimizer =
                    Settings.Diagnostics.UseStorageOptimizer = value > 0;

                RaisePropertyChanged();
            }
        }

        private StorageStatisticsFast _statisticsFast;
        public StorageStatisticsFast StatisticsFast
        {
            get => _statisticsFast;
            set => Set(ref _statisticsFast, value);
        }

        private StorageStatistics _statistics;
        public StorageStatistics Statistics
        {
            get => _statistics;
            set => Set(ref _statistics, ProcessTotal(value));
        }

        private StorageStatisticsByChat _totalStatistics;
        public StorageStatisticsByChat TotalStatistics
        {
            get => _totalStatistics;
            set => Set(ref _totalStatistics, value);
        }

        private IList<StorageChartItem> _itemsView;
        public IList<StorageChartItem> ItemsView
        {
            get => _itemsView;
            set => Set(ref _itemsView, value);
        }

        private ulong _systemFreeSpace;
        public ulong SystemFreeSpace
        {
            get => _systemFreeSpace;
            set => Set(ref _systemFreeSpace, value);
        }

        private ulong _systemCapacity;
        public ulong SystemCapacity
        {
            get => _systemCapacity;
            set => Set(ref _systemCapacity, value);
        }

        private long _totalBytes = -1;
        public long TotalBytes
        {
            get => _totalBytes;
            set => Set(ref _totalBytes, value);
        }

        private bool _taskCompleted;
        public bool TaskCompleted
        {
            get => _taskCompleted;
            set => Set(ref _taskCompleted, value);
        }

        public async void ClearCache()
        {
            var confirm = await ShowPopupAsync(Strings.StorageUsageInfo, Strings.ClearCache, Strings.ClearCache, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var types = ItemsView.Where(x => x.IsVisible).SelectMany(x => x.Types).ToList();
            if (types == null || types.Empty())
            {
                return;
            }

            IsLoading = true;
            TaskCompleted = false;

            var response = await ClientService.SendAsync(new OptimizeStorage(long.MaxValue, 0, int.MaxValue, 0, types, Array.Empty<long>(), Array.Empty<long>(), false, 25));
            if (response is StorageStatistics statistics)
            {
                Statistics = statistics;
            }

            IsLoading = false;
            TaskCompleted = true;
        }

        public async void Clear(StorageStatisticsByChat byChat)
        {
            if (byChat == null || byChat.ByFileType.Empty())
            {
                return;
            }

            var dialog = new SettingsStorageOptimizationPage(ClientService, byChat);

            var confirm = await ShowPopupAsync(dialog);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var types = dialog.SelectedItems;
            if (types == null || types.Empty())
            {
                return;
            }

            var chatIds = Array.Empty<long>();
            var excludedChatIds = Array.Empty<long>();

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

            var response = await ClientService.SendAsync(new OptimizeStorage(long.MaxValue, 0, int.MaxValue, 0, types, chatIds, excludedChatIds, false, 25));
            if (response is StorageStatistics statistics)
            {
                Statistics = statistics;
            }

            IsLoading = false;
            TaskCompleted = true;
        }

        private StorageStatistics ProcessTotal(StorageStatistics value)
        {
            var result = new StorageStatisticsByChat();
            result.ByFileType = new List<StorageStatisticsByFileType>();

            StorageChartItem photo = null;
            StorageChartItem video = null;
            StorageChartItem document = null;
            StorageChartItem audio = null;
            StorageChartItem voice = null;
            StorageChartItem stickers = null;
            StorageChartItem stories = null;
            StorageChartItem local = null;

            for (int i = 0; i < value.ByChat.Count; i++)
            {
                var chat = value.ByChat[i];

                result.Count += chat.Count;
                result.Size += chat.Size;

                for (int j = 0; j < chat.ByFileType.Count; j++)
                {
                    var fileType = chat.ByFileType[j];

                    switch (fileType.FileType)
                    {
                        case FileTypePhoto:
                            photo = new StorageChartItem(fileType);
                            break;
                        case FileTypeVideo:
                        case FileTypeAnimation:
                            video = video?.Add(fileType) ?? new StorageChartItem(fileType);
                            break;
                        case FileTypeDocument:
                            document = new StorageChartItem(fileType);
                            break;
                        case FileTypeAudio:
                            audio = new StorageChartItem(fileType);
                            break;
                        case FileTypeVideoNote:
                        case FileTypeVoiceNote:
                            voice = voice?.Add(fileType) ?? new StorageChartItem(fileType);
                            break;
                        case FileTypeSticker:
                            stickers = new StorageChartItem(fileType);
                            break;
                        case FileTypePhotoStory:
                        case FileTypeVideoStory:
                            stories = stories?.Add(fileType) ?? new StorageChartItem(fileType);
                            break;
                        case FileTypeProfilePhoto:
                        case FileTypeWallpaper:
                            break;
                        default:
                            local = local?.Add(fileType) ?? new StorageChartItem(fileType);
                            break;
                    }

                    if (fileType.FileType is FileTypeProfilePhoto or FileTypeWallpaper)
                    {
                        result.Count -= fileType.Count;
                        result.Size -= fileType.Size;

                        chat.Count -= fileType.Count;
                        chat.Size -= fileType.Size;

                        chat.ByFileType.Remove(fileType);
                        j--;

                        continue;
                    }

                    var already = result.ByFileType.FirstOrDefault(x => x.FileType.TypeEquals(fileType.FileType));
                    if (already == null)
                    {
                        already = new StorageStatisticsByFileType(fileType.FileType, 0, 0);
                        result.ByFileType.Add(already);
                    }

                    already.Count += fileType.Count;
                    already.Size += fileType.Size;
                }

                if (chat.ChatId == 0 || chat.ByFileType.Empty())
                {
                    value.ByChat.Remove(chat);
                    i--;
                }
            }

            ItemsView = new[]
            {
                photo,
                video,
                document,
                audio,
                voice,
                stickers,
                stories,
                local
            }.Where(x => x != null).OrderByDescending(x => x.TotalBytes).ToList();

            LoadSystem();

            TotalStatistics = result;
            IsLoading = false;

            return value;
        }

        private async void LoadSystem()
        {
            var info = await GetSystemTotalBytes();
            SystemFreeSpace = info.FreeSpace;
            SystemCapacity = info.Capacity;

            TotalBytes = ItemsView.Where(x => x.IsVisible).Sum(x => x.TotalBytes);
        }

        private async Task<(ulong FreeSpace, ulong Capacity)> GetSystemTotalBytes()
        {
            const String c_freeSpace = "System.FreeSpace";
            const String c_capacity = "System.Capacity";

            try
            {
                var retrieveProperties = await ApplicationData.Current.LocalFolder.Properties.RetrievePropertiesAsync(new[] { c_freeSpace, c_capacity });
                var freeSpace = (ulong)retrieveProperties[c_freeSpace];
                var capacity = (ulong)retrieveProperties[c_capacity];

                return (freeSpace, capacity);
            }
            catch
            {
                return (0, 0);
            }
        }

        public async void ClearDatabase()
        {
            if (StatisticsFast == null)
            {
                return;
            }

            var size = string.Format(Strings.LocalDatabaseClearText2, FileSizeConverter.Convert(StatisticsFast.DatabaseSize, true));

            var confirm = await ShowPopupAsync(Strings.LocalDatabaseClearText + "\n\n" + size + "\n\n" + Strings.LocalDatabaseClearText3, Strings.LocalDatabaseClearTextTitle, Strings.CacheClear, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ClientService.Delete(true);
        }
    }
}