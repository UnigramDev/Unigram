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
            var chats = new MutableVector<StorageStatisticsByChat>(10);

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
                    AppSettings.Diagnostics.StorageMaxTimeFromLastAccess = value * 60 * 60 * 24;
                ClientService.Options.UseStorageOptimizer =
                    AppSettings.Diagnostics.UseStorageOptimizer = value > 0;

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

        // The four biggest categories keep a row of their own and the rest are folded into one
        // "Other" - but only from six up, because at five the fold would trade one row for one row.
        private const int MaxNotCollapsed = 4;

        // Every category, in the order the ring uses them, with nothing folded. This is what a clear
        // operates on and what the totals are counted from - the two views below are presentation.
        private List<StorageChartItem> _sections = new();

        // The fold, or null when there was nothing to fold.
        private StorageChartItem _other;

        // The rows: at most the four biggest and then the fold.
        private List<StorageChartItem> _itemsView;
        public List<StorageChartItem> ItemsView
        {
            get => _itemsView;
            set
            {
                Set(ref _itemsView, value);
                RaisePropertyChanged(nameof(ChartView));
            }
        }

        // The ring is a partition, so it carries either the fold or its contents, never both.
        // Expanding splits its arc into the categories it stands for. The order survives that - the
        // fold sits where its largest child does - so every frame of the blend is still a
        // partition.
        public IList<StorageChartItem> ChartView => _other is { IsExpanded: true } ? _sections : _itemsView;

        public void SetExpanded(bool expanded)
        {
            if (_other != null)
            {
                _other.IsExpanded = expanded;
                RaisePropertyChanged(nameof(ChartView));
            }
        }

        // The ring has to keep something in it, so the last checked category cannot be unchecked.
        // False says that is what was asked for, which is the caller's cue to shake instead.
        public bool ToggleVisibility(StorageChartItem item)
        {
            var visible = !item.IsVisible;
            var folded = item.Children;

            if (!visible)
            {
                var remaining = 0;

                foreach (var section in _sections)
                {
                    if (section.IsVisible && section != item && folded?.Contains(section) is not true)
                    {
                        remaining++;
                    }
                }

                if (remaining == 0)
                {
                    return false;
                }
            }

            item.IsVisible = visible;

            if (folded != null)
            {
                foreach (var child in folded)
                {
                    child.IsVisible = visible;
                }

                item.UpdateCheckState();
            }
            else if (_other != null && _other.Children.Contains(item))
            {
                _other.UpdateCheckState();
            }

            RaisePropertyChanged(nameof(SelectedBytes));
            return true;
        }

        public long SelectedBytes
        {
            get
            {
                long sum = 0;

                foreach (var section in _sections)
                {
                    if (section.IsVisible)
                    {
                        sum += section.TotalBytes;
                    }
                }

                return sum;
            }
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

            var types = _sections.Where(x => x.IsVisible).SelectMany(x => x.Types).ToVector();
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
            var resultByFileType = new MutableVector<StorageStatisticsByFileType>();
            var valueByChat = value.ByChat.ToMutableVector();

            var result = new StorageStatisticsByChat();
            result.ByFileType = resultByFileType;
            value.ByChat = valueByChat;

            StorageChartItem photo = null;
            StorageChartItem video = null;
            StorageChartItem document = null;
            StorageChartItem audio = null;
            StorageChartItem voice = null;
            StorageChartItem stickers = null;
            StorageChartItem stories = null;
            StorageChartItem local = null;

            for (int i = 0; i < valueByChat.Count; i++)
            {
                var chat = valueByChat[i];
                var chatByFileType = chat.ByFileType.ToMutableVector();

                result.Count += chat.Count;
                result.Size += chat.Size;

                for (int j = 0; j < chatByFileType.Count; j++)
                {
                    var fileType = chatByFileType[j];

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

                        chatByFileType.Remove(fileType);
                        j--;

                        continue;
                    }

                    var already = resultByFileType.FirstOrDefault(x => x.FileType.TypeEquals(fileType.FileType));
                    if (already == null)
                    {
                        already = new StorageStatisticsByFileType(fileType.FileType, 0, 0);
                        resultByFileType.Add(already);
                    }

                    already.Count += fileType.Count;
                    already.Size += fileType.Size;
                }

                chat.ByFileType = chatByFileType;

                if (chat.ChatId == 0 || chat.ByFileType.Empty())
                {
                    valueByChat.Remove(chat);
                    i--;
                }
            }

            _sections = new[]
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

            if (_sections.Count > MaxNotCollapsed + 1)
            {
                _other = new StorageChartItem(_sections.GetRange(MaxNotCollapsed, _sections.Count - MaxNotCollapsed));

                var rows = _sections.GetRange(0, MaxNotCollapsed);
                rows.Add(_other);

                ItemsView = rows;
            }
            else
            {
                _other = null;

                ItemsView = _sections;
            }

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

            TotalBytes = SelectedBytes;
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