using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Core.Common;
using Unigram.Services;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Settings
{
    public class SettingsNetworkViewModel : UnigramViewModelBase
    {
        public SettingsNetworkViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<KeyedList<TdNetworkType, NetworkStatisticsEntry>>();

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

                        Items.ReplaceWith(groups.Select(x => new KeyedList<TdNetworkType, NetworkStatisticsEntry>(x.Key, x.Value)));
                        SelectedItem = Items.FirstOrDefault();
                    });
                }
            });

            return Task.CompletedTask;
        }

        private DateTime _sinceDate;
        public DateTime SinceDate
        {
            get
            {
                return _sinceDate;
            }
            set
            {
                Set(ref _sinceDate, value);
            }
        }

        private KeyedList<TdNetworkType, NetworkStatisticsEntry> _selectedItem;
        public KeyedList<TdNetworkType, NetworkStatisticsEntry> SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
            }
        }

        public MvxObservableCollection<KeyedList<TdNetworkType, NetworkStatisticsEntry>> Items { get; private set; }

        public RelayCommand ResetCommand { get; }
        private async void ResetExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.ResetStatisticsAlert, Strings.Resources.AppName, Strings.Resources.Reset, Strings.Resources.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                //_statsService.ResetStats(Type);
                //Refresh();
            }
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
    //        var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.ResetStatisticsAlert, Strings.Resources.AppName, Strings.Resources.Reset, Strings.Resources.Cancel);
    //        if (confirm == ContentDialogResult.Primary)
    //        {
    //            _statsService.ResetStats(Type);
    //            Refresh();
    //        }
    //    }
    //}
}
