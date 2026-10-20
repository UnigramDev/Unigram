//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
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

        // Written and read on the player's demux thread only.
        private RemoteFileSource _source;

        public RemoteRendition(IClientService clientService, File file, File playlist, int width, int height, double duration)
        {
            _clientService = clientService;
            _playlist = playlist;
            _duration = duration;

            File = file;
            Width = width;
            Height = height;
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
                    var response = _clientService.SendAsync(new DownloadFile(_playlist.Id, 32, 0, 0, true)).Result;

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

        public IAsyncMediaPlayerSource CreateSource()
        {
            return _source = new RemoteFileSource(_clientService, File, _duration);
        }

        public double DownloadSpeed => _source?.DownloadBytesPerSecond ?? 0;
    }
}
