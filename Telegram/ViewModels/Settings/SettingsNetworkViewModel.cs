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
using Telegram.Collections;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public partial class SettingsNetworkViewModel : ViewModelBase
    {
        public SettingsNetworkViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<StorageChartItem>();
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetNetworkStatistics(false));
            if (response is NetworkStatistics statistics)
            {
                SinceDate = Formatter.ToLocalTime(statistics.SinceDate);

                var totalSent = 0L;
                var totalReceived = 0L;

                StorageChartItem photo = null;
                StorageChartItem video = null;
                StorageChartItem document = null;
                StorageChartItem audio = null;
                StorageChartItem voice = null;
                StorageChartItem local = null;

                foreach (var entry in statistics.Entries)
                {
                    if (entry is NetworkStatisticsEntryFile file)
                    {
                        switch (file.FileType)
                        {
                            case FileTypePhoto:
                                photo = new StorageChartItem(file);
                                break;
                            case FileTypeVideo:
                            case FileTypeAnimation:
                                video = video?.Add(file) ?? new StorageChartItem(file);
                                break;
                            case FileTypeDocument:
                                document = new StorageChartItem(file);
                                break;
                            case FileTypeAudio:
                                audio = new StorageChartItem(file);
                                break;
                            case FileTypeVideoNote:
                            case FileTypeVoiceNote:
                                voice = voice?.Add(file) ?? new StorageChartItem(file);
                                break;
                            default:
                                local = local?.Add(file) ?? new StorageChartItem(file);
                                break;
                        }

                        totalSent += file.SentBytes;
                        totalReceived += file.ReceivedBytes;
                    }
                    else if (entry is NetworkStatisticsEntryCall call)
                    {
                        //results.Add(entry);

                        //totalSent += call.SentBytes;
                        //totalReceived += call.ReceivedBytes;
                    }
                }

                Items.ReplaceWith(new[]
                {
                    photo ?? new StorageChartItem(new FileTypePhoto()),
                    video ?? new StorageChartItem(new FileTypeVideo()),
                    document ?? new StorageChartItem(new FileTypeDocument()),
                    audio ?? new StorageChartItem(new FileTypeAudio()),
                    voice ?? new StorageChartItem(new FileTypeVoiceNote()),
                    local ?? new StorageChartItem(new FileTypeUnknown())
                }.Where(x => x != null).OrderByDescending(x => x.TotalBytes));

                TotalSentBytes = totalSent;
                TotalReceivedBytes = totalReceived;

                RaisePropertyChanged(nameof(ItemsView));
                RaisePropertyChanged(nameof(TotalBytes));
            }
        }

        private DateTime _sinceDate;
        public DateTime SinceDate
        {
            get => _sinceDate;
            set => Set(ref _sinceDate, value);
        }

        private long _totalSentBytes;
        public long TotalSentBytes
        {
            get => _totalSentBytes;
            set => Set(ref _totalSentBytes, value);
        }

        private long _totalReceivedBytes;
        public long TotalReceivedBytes
        {
            get => _totalReceivedBytes;
            set => Set(ref _totalReceivedBytes, value);
        }

        public long TotalBytes => TotalSentBytes + TotalReceivedBytes;

        public MvxObservableCollection<StorageChartItem> Items { get; private set; }

        public IList<StorageChartItem> ItemsView => Items.Count > 0 ? Items : null;

        public async void Reset()
        {
            var confirm = await ShowPopupAsync(Strings.ResetStatisticsAlert, Strings.ResetStatisticsAlertTitle, Strings.Reset, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                await ClientService.SendAsync(new ResetNetworkStatistics());
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }
        }
    }
}
