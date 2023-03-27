//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Settings;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsStorageViewModel : TLViewModelBase
    {
        public SettingsStorageViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            IsLoading = true;

            ClientService.Send(new GetStorageStatisticsFast(), result =>
            {
                if (result is StorageStatisticsFast stats)
                {
                    BeginOnUIThread(() => StatisticsFast = stats);
                }
            });

            ClientService.Send(new GetStorageStatistics(int.MaxValue), result =>
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
                ClientService.Options.StorageMaxTimeFromLastAccess = value * 60 * 60 * 24;
                ClientService.Options.UseStorageOptimizer = value > 0;

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

        private bool _taskCompleted;
        public bool TaskCompleted
        {
            get => _taskCompleted;
            set => Set(ref _taskCompleted, value);
        }

        public void ClearCache()
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

            var dialog = new SettingsStorageOptimizationPage(ClientService, byChat);

            var confirm = await ShowPopupAsync(dialog);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var types = dialog.SelectedItems;
            if (types == null || types.IsEmpty())
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

            for (int i = 0; i < value.ByChat.Count; i++)
            {
                var chat = value.ByChat[i];

                result.Count += chat.Count;
                result.Size += chat.Size;

                for (int j = 0; j < chat.ByFileType.Count; j++)
                {
                    var type = chat.ByFileType[j];

                    if (type.FileType is FileTypeProfilePhoto or FileTypeWallpaper)
                    {
                        result.Count -= type.Count;
                        result.Size -= type.Size;

                        chat.Count -= type.Count;
                        chat.Size -= type.Size;

                        chat.ByFileType.Remove(type);
                        j--;

                        continue;
                    }

                    var already = result.ByFileType.FirstOrDefault(x => x.FileType.TypeEquals(type.FileType));
                    if (already == null)
                    {
                        already = new StorageStatisticsByFileType(type.FileType, 0, 0);
                        result.ByFileType.Add(already);
                    }

                    already.Count += type.Count;
                    already.Size += type.Size;
                }

                if (chat.ByFileType.IsEmpty())
                {
                    value.ByChat.Remove(chat);
                    i--;
                }
            }

            TotalStatistics = result;
            IsLoading = false;

            return value;
        }
    }
}