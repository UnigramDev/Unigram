//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Settings
{
    public class SettingsNetworkViewModel : TLViewModelBase
    {
        public SettingsNetworkViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<NetworkStatisticsItem>();
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetNetworkStatistics(false));
            if (response is NetworkStatistics statistics)
            {
                SinceDate = Formatter.ToLocalTime(statistics.SinceDate);

                var notes = new NetworkStatisticsEntryFile(new FileTypeNotes(), null, 0, 0);
                var other = new NetworkStatisticsEntryFile(new FileTypeOther(), null, 0, 0);

                var totalSent = 0l;
                var totalReceived = 0l;

                var results = new List<NetworkStatisticsEntry>();

                foreach (var entry in statistics.Entries)
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

                        totalSent += file.SentBytes;
                        totalReceived += file.ReceivedBytes;
                    }
                    else if (entry is NetworkStatisticsEntryCall call)
                    {
                        results.Add(entry);

                        totalSent += call.SentBytes;
                        totalReceived += call.ReceivedBytes;
                    }
                }

                if (notes.SentBytes > 0 || notes.ReceivedBytes > 0)
                {
                    results.Add(notes);
                }

                if (other.SentBytes > 0 || other.ReceivedBytes > 0)
                {
                    results.Add(other);
                }

                Items.ReplaceWith(results.Select(x => new NetworkStatisticsItem(x)).OrderByDescending(x => x.TotalBytes));

                TotalSentBytes = totalSent;
                TotalReceivedBytes = totalReceived;
            }
        }

        private bool IsSecondaryType(FileType type)
        {
            switch (type)
            {
                case FileTypePhoto:
                case FileTypeVideo:
                case FileTypeVideoNote:
                case FileTypeVoiceNote:
                case FileTypeDocument:
                case FileTypeAudio:
                    return false;
                default:
                    return true;
            }
        }

        private bool IsNotesType(FileType type)
        {
            switch (type)
            {
                case FileTypeVideoNote:
                case FileTypeVoiceNote:
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

        public MvxObservableCollection<NetworkStatisticsItem> Items { get; private set; }

        public async void Reset()
        {
            var confirm = await ShowPopupAsync(Strings.ResetStatisticsAlert, Strings.AppName, Strings.Reset, Strings.Cancel);
            if (confirm == ContentDialogResult.Primary)
            {
                await ClientService.SendAsync(new ResetNetworkStatistics());
                await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
            }
        }
    }

    public class NetworkStatisticsItem
    {
        public NetworkStatisticsItem(NetworkStatisticsEntry entry)
            : this(entry as NetworkStatisticsEntryFile)
        {

        }

        public NetworkStatisticsItem(NetworkStatisticsEntryFile entry)
        {
            if (entry == null)
            {
                return;
            }

            TotalBytes = entry.SentBytes + entry.ReceivedBytes;
            SentBytes = entry.SentBytes;
            ReceivedBytes = entry.ReceivedBytes;

            switch (entry.FileType)
            {
                case FileTypeNotes:
                    Name = Strings.LocalAudioCache;
                    Glyph = Icons.MicOn;
                    break;
                case FileTypeOther:
                    Name = Strings.MessagesOverview;
                    Glyph = Icons.ChatMultiple;
                    break;
                case FileTypeAudio:
                    Name = Strings.LocalMusicCache;
                    Glyph = Icons.PlayCircle;
                    break;
                case FileTypeDocument:
                    Name = Strings.LocalDocumentCache;
                    Glyph = Icons.Document;
                    break;
                case FileTypePhoto:
                    Name = Strings.LocalPhotoCache;
                    Glyph = Icons.Image;
                    break;
                case FileTypeVideo:
                    Name = Strings.LocalVideoCache;
                    Glyph = Icons.Video;
                    break;
            }

        }

        public string Glyph { get; }

        public string Name { get; }

        public long TotalBytes { get; }

        public long SentBytes { get; }

        public long ReceivedBytes { get; }
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
}
