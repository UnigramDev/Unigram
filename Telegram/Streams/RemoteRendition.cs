//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Native.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Storage;

namespace Telegram.Streams
{
    /// <summary>
    /// One quality of a video: an alternative TDLib lists with its HLS playlist, or the original
    /// file, which has none and plays as one plain file.
    /// </summary>
    public partial class RemoteRendition : IAsyncMediaPlayerRendition
    {
        private readonly IClientService _clientService;
        private readonly File _playlist;
        private readonly double _duration;
        private readonly FileReadRank _rank;

        // Written and read on the player's demux thread only.
        private RemoteFileSource _source;

        // Whether LoadPlaylist is waiting on the download. Under the lock, so that a cancel either
        // finds no request or is sent after it.
        private readonly object _loadLock = new();
        private bool _loading;

        public RemoteRendition(IClientService clientService, File file, File playlist, int width, int height, double duration, FileReadRank rank = FileReadRank.Playback)
        {
            _clientService = clientService;
            _playlist = playlist;
            _duration = duration;
            _rank = rank;

            File = file;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// The qualities a video plays in, or null for one that plays its own file only.
        /// </summary>
        public static List<RemoteRendition> CreateRenditions(IClientService clientService, MessageVideo video, FileReadRank rank = FileReadRank.Playback)
        {
            if (!video.IsHls())
            {
                return null;
            }

            var renditions = new List<RemoteRendition>();
            var duration = video.Video.Duration;

            foreach (var alternative in video.AlternativeVideos)
            {
                if (alternative.Codec == "h264" && alternative.Width > 0 && alternative.Height > 0 && alternative.HlsFile != null)
                {
                    renditions.Add(new RemoteRendition(clientService, alternative.Video, alternative.HlsFile, alternative.Width, alternative.Height, duration, rank));
                }
            }

            if (renditions.Count == 0)
            {
                return null;
            }

            // The original only once it is on disk: then nothing plays better, and nothing more has
            // to be downloaded. Before that it is usually far larger than the best alternative.
            var original = video.Video;
            if (original.VideoValue.Local.IsDownloadingCompleted)
            {
                renditions.Add(new RemoteRendition(clientService, original.VideoValue, null, original.Width, original.Height, duration, rank));
            }

            renditions.Sort(CompareRenditions);

            // One per quality: the smaller file of two that look the same.
            for (int i = renditions.Count - 1; i > 0; i--)
            {
                if (renditions[i].Quality == renditions[i - 1].Quality && !renditions[i].IsOriginal && !renditions[i - 1].IsOriginal)
                {
                    renditions.RemoveAt(i);
                }
            }

            return renditions;
        }

        private static int CompareRenditions(RemoteRendition x, RemoteRendition y)
        {
            return x.Quality != y.Quality
                ? x.Quality.CompareTo(y.Quality)
                : x.Size.CompareTo(y.Size);
        }

        /// <summary>
        /// The quality playback starts with.
        /// </summary>
        public static int PickRendition(List<RemoteRendition> renditions)
        {
            // Whatever is already on disk wins, the largest first: it plays at once and costs nothing.
            for (int i = renditions.Count - 1; i >= 0; i--)
            {
                if (renditions[i].File.Local.IsDownloadingCompleted)
                {
                    return i;
                }
            }

            // Then the quality picked by hand, or the one automatic quality last settled on, as the
            // best guess at what the connection carries.
            var quality = AppSettings.Playback.VideoQuality;
            if (quality <= 0)
            {
                quality = 480;
            }

            var best = 0;

            for (int i = 1; i < renditions.Count; i++)
            {
                if (Math.Abs(renditions[i].Quality - quality) < Math.Abs(renditions[best].Quality - quality))
                {
                    best = i;
                }
            }

            return best;
        }

        public File File { get; }

        public bool IsOriginal => _playlist == null;

        public int Width { get; }

        public int Height { get; }

        /// <summary>
        /// The short side, which is what a quality is named by: a portrait 1080x1920 video is 1080p.
        /// </summary>
        public int Quality => Math.Min(Width, Height);

        public long Size => File.Size;

        public bool HasPlaylist => _playlist != null;

        public string LoadPlaylist()
        {
            if (_playlist == null)
            {
                return string.Empty;
            }

            try
            {
                // On the player's demux thread, which is expected to wait: a playlist is a few
                // hundred bytes, and nothing of this quality can be read without it.
                //
                // Asked for directly rather than through DownloadFileAsync, which would list the
                // playlist among the files the user downloaded.
                var file = _playlist;

                if (!file.Local.IsDownloadingCompleted)
                {
                    Task<Object> request;

                    lock (_loadLock)
                    {
                        _loading = true;
                        request = _clientService.SendAsync(new DownloadFile(_playlist.Id, 32, 0, 0, true));
                    }

                    Object response;

                    try
                    {
                        response = request.Result;
                    }
                    finally
                    {
                        lock (_loadLock)
                        {
                            _loading = false;
                        }
                    }

                    if (response is not File downloaded || !downloaded.Local.IsDownloadingCompleted)
                    {
                        return string.Empty;
                    }

                    file = downloaded;
                }

                try
                {
                    return System.IO.File.ReadAllText(file.Local.Path);
                }
                catch
                {
                    // A download folder outside the app's own storage is only reachable this way.
                    var storage = StorageFile.GetFileFromPathAsync(file.Local.Path).AsTask().Result;
                    return FileIO.ReadTextAsync(storage).AsTask().Result;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return string.Empty;
            }
        }

        public void CancelPlaylist()
        {
            lock (_loadLock)
            {
                // A synchronous download answers once it is canceled, which LoadPlaylist then
                // reads as no playlist.
                if (_loading)
                {
                    _clientService.Send(new CancelDownloadFile(_playlist.Id, false));
                }
            }
        }

        public IAsyncMediaPlayerSource CreateSource()
        {
            return _source = new RemoteFileSource(_clientService, File, _duration, rank: _rank);
        }

        public double DownloadSpeed => _source?.DownloadBytesPerSecond ?? 0;
    }
}
