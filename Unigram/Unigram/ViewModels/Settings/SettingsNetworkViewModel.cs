using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNetworkViewModel : TLViewModelBase
    {
        public SettingsNetworkViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<NetworkStatisticsList>();

            ResetCommand = new RelayCommand(ResetExecute);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            ProtoService.Send(new GetNetworkStatistics(false), result =>
            {
                if (result is NetworkStatistics statistics)
                {
                    BeginOnUIThread(() =>
                    {
                        SinceDate = BindConvert.Current.DateTime(statistics.SinceDate);

                        var groups = new Dictionary<TdNetworkType, List<NetworkStatisticsEntry>>();

                        foreach (var entry in statistics.Entries)
                        {
                            var type = entry.GetNetworkType();
                            if (groups.TryGetValue(type, out List<NetworkStatisticsEntry> entries))
                            {
                                entries.Add(entry);
                            }
                            else
                            {
                                groups[type] = new List<NetworkStatisticsEntry>();
                                groups[type].Add(entry);
                            }
                        }

                        foreach (var group in groups.ToList())
                        {
                            var notes = new NetworkStatisticsEntryFile(new FileTypeNotes(), null, 0, 0);
                            var other = new NetworkStatisticsEntryFile(new FileTypeOther(), null, 0, 0);
                            var total = new NetworkStatisticsEntryFile(new FileTypeTotal(), null, 0, 0);

                            var results = new List<NetworkStatisticsEntry>
                            {
                                notes,
                                other,
                                total
                            };

                            foreach (var entry in group.Value)
                            {
                                if (entry is NetworkStatisticsEntryFile file)
                                {
                                    if (IsSecondaryType(file.FileType))
                                    {
                                        other.SentBytes += file.SentBytes;
                                        other.ReceivedBytes += file.ReceivedBytes;
                                    }
                                    else if (IsNotesType(file.FileType))
                                    {
                                        notes.SentBytes += file.SentBytes;
                                        notes.ReceivedBytes += file.ReceivedBytes;
                                    }
                                    else
                                    {
                                        results.Add(entry);
                                    }

                                    total.SentBytes += file.SentBytes;
                                    total.ReceivedBytes += file.ReceivedBytes;
                                }
                                else if (entry is NetworkStatisticsEntryCall call)
                                {
                                    results.Add(entry);

                                    total.SentBytes += call.SentBytes;
                                    total.ReceivedBytes += call.ReceivedBytes;
                                }
                            }

                            groups[group.Key] = results;
                        }

                        Items.ReplaceWith(groups.Select(x => new NetworkStatisticsList(x.Key, x.Value.OrderBy(y => y, new NetworkStatisticsComparer()))));
                        SelectedItem = Items.FirstOrDefault();
                    });
                }
            });

            return Task.CompletedTask;
        }

        private bool IsSecondaryType(FileType type)
        {
            switch (type)
            {
                case FileTypePhoto photo:
                case FileTypeVideo video:
                case FileTypeVideoNote videoNote:
                case FileTypeVoiceNote voiceNote:
                case FileTypeDocument document:
                    return false;
                default:
                    return true;
            }
        }

        private bool IsNotesType(FileType type)
        {
            switch (type)
            {
                case FileTypeVideoNote videoNote:
                case FileTypeVoiceNote voiceNote:
                    return true;
                default:
                    return false;
            }
        }

        private DateTime _sinceDate;
        public DateTime SinceDate
        {
            get => _sinceDate;
            set => Set(ref _sinceDate, value);
        }

        private NetworkStatisticsList _selectedItem;
        public NetworkStatisticsList SelectedItem
        {
            get => _selectedItem;
            set => Set(ref _selectedItem, value);
        }

        public MvxObservableCollection<NetworkStatisticsList> Items { get; private set; }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            var confirm = await MessagePopup.ShowAsync(Strings.Resources.ResetStatisticsAlert, Strings.Resources.AppName, Strings.Resources.Reset, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                await ProtoService.SendAsync(new ResetNetworkStatistics());
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }
        }
    }

    public class NetworkStatisticsList : KeyedList<TdNetworkType, NetworkStatisticsEntry>
    {
        public NetworkStatisticsList(TdNetworkType key, IEnumerable<NetworkStatisticsEntry> source)
            : base(key, source)
        {
        }

        public override string ToString()
        {
            switch (Key)
            {
                case TdNetworkType.Mobile:
                    return Strings.Resources.NetworkUsageMobileTab;
                case TdNetworkType.MobileRoaming:
                    return Strings.Resources.NetworkUsageRoamingTab;
                case TdNetworkType.WiFi:
                    return Strings.Resources.NetworkUsageWiFiTab;
                default:
                    return Key.ToString();
            }
        }
    }

    public class NetworkStatisticsComparer : IComparer<NetworkStatisticsEntry>
    {
        public int Compare(NetworkStatisticsEntry x, NetworkStatisticsEntry y)
        {
            var xv = GetValue(x);
            var yv = GetValue(y);

            return xv.CompareTo(yv);
        }

        private int GetValue(NetworkStatisticsEntry x)
        {
            switch (x)
            {
                case NetworkStatisticsEntryCall call:
                    return 4;
                case NetworkStatisticsEntryFile file:
                    switch (file.FileType)
                    {
                        case FileTypePhoto photo:
                            return 0;
                        case FileTypeVideo video:
                            return 1;
                        case FileTypeNotes notes:
                            return 2;
                        case FileTypeDocument document:
                            return 3;
                        case FileTypeOther other:
                            return 5;
                        case FileTypeTotal total:
                            return 6;
                    }
                    break;
            }

            return int.MaxValue;
        }
    }

    public class FileTypeNotes : FileType
    {
        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class FileTypeOther : FileType
    {
        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public class FileTypeTotal : FileType
    {
        public NativeObject ToUnmanaged()
        {
            throw new NotImplementedException();
        }
    }

    public enum TdNetworkType
    {
        Mobile,
        MobileRoaming,
        None,
        Other,
        WiFi
    }

    public enum TdFileType
    {
        Animation,
        Audio,
        Document,
        None,
        Photo,
        ProfilePhoto,
        Secret,
        SecretThumbnail,
        Sticker,
        Thumbnail,
        Unknown,
        Video,
        VideoNote,
        VoiceNote,
        Wallpaper
    }

    //public class SettingsStatsNetwork : BindableBase
    //{
    //    private readonly IStatsService _statsService;

    //    public SettingsStatsNetwork(IStatsService statsService, string title, NetworkType type)
    //    {
    //        _statsService = statsService;

    //        Title = title;
    //        Type = type;

    //        Items = new MvxObservableCollection<SettingsStatsDataBase>
    //        {
    //            new SettingsStatsData(statsService, Strings.Resources.LocalPhotoCache, type, DataType.Photos),
    //            new SettingsStatsData(statsService, Strings.Resources.LocalVideoCache, type, DataType.Videos),
    //            new SettingsStatsData(statsService, Strings.Resources.LocalAudioCache, type, DataType.Audios),
    //            new SettingsStatsData(statsService, Strings.Resources.FilesDataUsage, type, DataType.Files),
    //            new SettingsStatsCallData(statsService, Strings.Resources.CallsDataUsage, type, DataType.Calls),
    //            new SettingsStatsDataBase(statsService, Strings.Resources.MessagesValueUsage, type, DataType.Messages),
    //            new SettingsStatsDataBase(statsService, Strings.Resources.TotalDataUsage, type, DataType.Total)
    //        };

    //        ResetCommand = new RelayCommand(ResetExecute);

    //        Refresh();
    //    }

    //    public string Title { get; private set; }

    //    public NetworkType Type { get; private set; }

    //    public void Refresh()
    //    {
    //        ResetDate = Utils.UnixTimestampToDateTime(_statsService.GetResetStatsDate(Type) / 1000);

    //        foreach (var item in Items)
    //        {
    //            item.Refresh();
    //        }
    //    }

    //    private DateTime _resetDate;
    //    public DateTime ResetDate
    //    {
    //        get
    //        {
    //            return _resetDate;
    //        }
    //        set
    //        {
    //            Set(ref _resetDate, value);
    //        }
    //    }

    //    public MvxObservableCollection<SettingsStatsDataBase> Items { get; private set; }

    //    public RelayCommand ResetCommand { get; }
    //    private async void ResetExecute()
    //    {
    //        var confirm = await MessagePopup.ShowAsync(Strings.Resources.ResetStatisticsAlert, Strings.Resources.AppName, Strings.Resources.Reset, Strings.Resources.Cancel);
    //        if (confirm == ContentDialogResult.Primary)
    //        {
    //            _statsService.ResetStats(Type);
    //            Refresh();
    //        }
    //    }
    //}
}
