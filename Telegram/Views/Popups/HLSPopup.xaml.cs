//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LibVLCSharp.Shared;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Media.Core;
using Windows.Media.Streaming.Adaptive;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Popups
{
    public class AdaptiveVideoSource
    {
        private readonly IClientService _clientService;
        private readonly MessageVideo _video;
        private readonly List<AlternativeVideo> _playlist = new();

        private readonly Dictionary<int, int> _map = new();

        private readonly MediaBinder _binder = new();

        private AdaptiveMediaSource _source;

        public AdaptiveVideoSource(IClientService clientService, MessageVideo video)
        {
            _clientService = clientService;
            _video = video;

            for (int i = 0; i < video.AlternativeVideos.Count; i++)
            {
                if (string.Equals(video.AlternativeVideos[i].Codec, "h264", StringComparison.OrdinalIgnoreCase))
                {
                    _playlist.Add(video.AlternativeVideos[i]);
                    _map[video.AlternativeVideos[i].HlsFile.Id] = video.AlternativeVideos[i].Video.Id;
                }
            }

            _binder.Binding += OnBinding;
        }

        public event EventHandler<AdaptiveMediaSourcePlaybackBitrateChangedEventArgs> BitrateChanged;

        public IReadOnlyList<uint> AvailableBitrates => _source?.AvailableBitrates ?? Array.Empty<uint>();

        public uint? DesiredBitrate
        {
            get => _source?.DesiredMinBitrate + 1 ?? null;
            set
            {
                if (_source != null)
                {
                    _source.DesiredMinBitrate = value.HasValue ? value - 1 : null;
                    _source.DesiredMaxBitrate = value.HasValue ? value + 1 : null;
                }
            }
        }

        public MediaSource CreateMediaSource()
        {
            return MediaSource.CreateFromMediaBinder(_binder);
        }

        private async void OnBinding(MediaBinder sender, MediaBindingEventArgs args)
        {
            var deferral = args.GetDeferral();
            var master = GetMasterPlaylist();

            var result = await AdaptiveMediaSource.CreateFromUriAsync(new Uri("http://localhost:8899/" + ToBase64(master) + ".m3u8"));
            if (result.Status == AdaptiveMediaSourceCreationStatus.Success)
            {
                _source = result.MediaSource;
                _source.PlaybackBitrateChanged += OnPlaybackBitrateChanged;

                args.SetAdaptiveMediaSource(result.MediaSource);
            }

            deferral.Complete();
        }

        private void OnPlaybackBitrateChanged(AdaptiveMediaSource sender, AdaptiveMediaSourcePlaybackBitrateChangedEventArgs args)
        {
            BitrateChanged?.Invoke(this, args);
        }

        private string GetMasterPlaylist()
        {
            var playlistString = new StringBuilder();
            playlistString.Append("#EXTM3U\n");

            foreach (var item in _playlist.OrderBy(x => x.Width * x.Height))
            {
                int width = Math.Max(item.Width, 1280);
                int height = Math.Max(item.Height, 720);

                int bandwidth;

                if (_video.Video.Duration != 0)
                {
                    bandwidth = (int)((double)item.Video.Size / _video.Video.Duration) * 8;
                }
                else
                {
                    bandwidth = 1000000;
                }

                playlistString.Append($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},CODECS=\"{item.Codec}\",RESOLUTION={width}x{height}\n");
                playlistString.Append($"{item.HlsFile.Id}.m3u8\n");
            }

            return playlistString.ToString();
        }

        private string ToBase64(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            return System.Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private byte[] FromBase64(string base64)
        {
            string incoming = base64.Replace('_', '/').Replace('-', '+');
            switch (base64.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            return Convert.FromBase64String(incoming);
            //return Encoding.ASCII.GetString(bytes);
        }
    }

    public sealed partial class HLSPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly MessageVideo _video;
        private readonly List<AlternativeVideo> _playlist = new();

        private readonly Telegram.Common.HttpServer _server;

        private Dictionary<int, int> _map = new();

        public HLSPopup(IClientService clientService, MessageVideo video)
        {
            InitializeComponent();

            //Width = 500;
            //MinWidth = 500;
            //MaxWidth = 500;
            ContentMaxWidth = 500;

            PrimaryButtonText = "OK";
            SecondaryButtonText = "No";

            _clientService = clientService;
            _video = video;

            for (int i = 0; i < video.AlternativeVideos.Count; i++)
            {
                if (string.Equals(video.AlternativeVideos[i].Codec, "h264", StringComparison.OrdinalIgnoreCase))
                {
                    _playlist.Add(video.AlternativeVideos[i]);
                    _map[video.AlternativeVideos[i].HlsFile.Id] = video.AlternativeVideos[i].Video.Id;
                }
            }

            _server = new(8899, Test);
            //_server.Start();

            Unloaded += HLSPopup_Unloaded;
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            WebView.CoreWebView2Initialized += OnInitialized;
            await WebView.EnsureCoreWebView2Async();
        }

        private void OnInitialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            var core = sender.CoreWebView2;
            if (core == null)
            {
                // Dio boia
                return;
            }

            core.WebResourceRequested += OnWebResourceRequested;
            core.WebMessageReceived += OnWebMessageReceived;

            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.Navigate("http://127.0.0.1/hls.html");
        }

        private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            Logger.Info(args.WebMessageAsJson);

            if (JsonObject.TryParse(args.WebMessageAsJson, out JsonObject data))
            {
                //{"event":"playerStatus","data":{"isReady":true,"isFirstFrameReady":true,"isPlaying":true,"rate":1,"defaultRate":1,"levels":[{"index":0,"bitrate":530312,"width":1280,"height":720},{"index":1,"bitrate":1122824,"width":1280,"height":720},{"index":2,"bitrate":2421904,"width":1920,"height":1080}],"currentLevel":2}}
                //{ "event":"playerCurrentTime","data":{ "value":6.041956} }
                var eventName = data.GetNamedString("event", string.Empty);
                var eventData = data.GetNamedObject("data", new JsonObject());

                if (eventName == "playerStatus")
                {
                    var isReady = eventData.GetNamedBoolean("isReady", false);
                    var isFirstFrameReady = eventData.GetNamedBoolean("isFirstFrameReady", false);
                    var isPlaying = eventData.GetNamedBoolean("isPlaying", false);
                    var rate = eventData.GetNamedNumber("rate", 1);
                    var defaultRate = eventData.GetNamedNumber("defaultRate", 1);
                    var volume = eventData.GetNamedNumber("volume", 1);
                    var duration = eventData.GetNamedNumber("duration", 0);

                    var levels = eventData.GetNamedArray("levels", new JsonArray());
                    var currentLevel = eventData.GetNamedNumber("currentLevel", -1);

                }
                else if (eventName == "playerCurrentTime")
                {
                    var value = eventData.GetNamedNumber("value", 0);
                }
            }
        }

        private async void OnWebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();

            var segments = args.Request.Uri.Split('/');
            var resource = segments[^1];

            Logger.Info(args.Request.Uri);

            async Task<IRandomAccessStream> ToStreamAsync(string source)
            {
                var stream = new InMemoryRandomAccessStream();
                using var writer = new DataWriter(stream.GetOutputStreamAt(0));

                var bytes = Encoding.ASCII.GetBytes(source);

                writer.WriteBytes(bytes);
                await writer.StoreAsync();

                return stream;
            }

            CoreWebView2WebResourceResponse CreateWebResourceResponse(IRandomAccessStream Content, Dictionary<string, object> Headers)
            {
                return sender.Environment.CreateWebResourceResponse(Content, 200, "OK", string.Join('\n', Headers.Select(x => string.Format("{0}: {1}", x.Key, x.Value))));
            }

            var headers = new Dictionary<string, object>();

            var fileName = System.IO.Path.GetFileNameWithoutExtension(resource);
            var extension = System.IO.Path.GetExtension(resource);

            if (resource == "hls.html" || resource == "hls.js")
            {
                var file = await Package.Current.InstalledLocation.GetFileAsync("Assets\\" + resource);
                //var text = await FileIO.ReadTextAsync(file);

                headers["Content-Type"] = file.ContentType;

                using (var stream = await file.OpenReadAsync())
                {
                    args.Response = CreateWebResourceResponse(stream, headers);
                }
            }
            else if (resource == "master.m3u8")
            {
                var playlist = ToPlaylist(_playlist);

                headers["Content-Type"] = "application/vnd.apple.mpegurl";

                using (var stream = await ToStreamAsync(playlist))
                {
                    args.Response = CreateWebResourceResponse(stream, headers);
                }
            }
            else if (int.TryParse(fileName, out int fileId))
            {
                long offset = 0;
                long limit = 0;

                if (args.Request.Headers.Contains("Range"))
                {
                    var range = args.Request.Headers.GetHeader("Range");
                    if (range != null && System.Net.Http.Headers.RangeHeaderValue.TryParse(range, out var ranges))
                    {
                        foreach (var part in ranges.Ranges)
                        {
                            offset = part.From ?? 0;
                            limit = part.To ?? -1;

                            limit = limit - offset + 1;
                            break;
                        }
                    }
                }

                Debug.WriteLine(resource + ", offset: " + offset + ", length:" + limit);

                var file = _clientService.GetFileAsync(fileId).Result;
                var remote = new Telegram.Streams.RemoteFileSource(_clientService, file, extension == ".m3u8" ? 32 : 31, true);

                if (limit == 0)
                {
                    limit = file.Size;
                }

                remote.SeekCallback(offset);
                remote.ReadCallback(limit);

                if (extension == ".m3u8")
                {
                    var text = System.IO.File.ReadAllText(file.Local.Path);
                    var playlist = ToPlaylist(text, _map[fileId]);
                    var bytes = Encoding.ASCII.GetBytes(playlist);

                    headers["Content-Type"] = "application/vnd.apple.mpegurl";
                    headers["Content-Length"] = bytes.Length.ToString();

                    using (var stream = await ToStreamAsync(playlist))
                    {
                        args.Response = CreateWebResourceResponse(stream, headers);
                    }
                }
                else
                {
                    //response.StatusCode = "206";
                    headers["Content-Type"] = "video/mp4";
                    headers["Content-Type"] = limit.ToString();

                    using (var stream = new System.IO.FileStream(file.Local.Path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                    {
                        stream.Seek(offset, System.IO.SeekOrigin.Begin);

                        byte[] buffer = new byte[(int)limit];
                        stream.Read(buffer, 0, buffer.Length);

                        using var memory = new InMemoryRandomAccessStream();
                        using var writer = new DataWriter(memory.GetOutputStreamAt(0));

                        writer.WriteBytes(buffer);
                        await writer.StoreAsync();

                        args.Response = CreateWebResourceResponse(memory, headers);
                    }
                }
            }

            deferral.Complete();
        }

        private void HLSPopup_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            _server.Stop();
        }

        private Telegram.Common.HttpResponse Test(Telegram.Common.HttpRequest request)
        {
            var response = new Telegram.Common.HttpResponse();

            var fileName = System.IO.Path.GetFileNameWithoutExtension(request.Path);
            var extension = System.IO.Path.GetExtension(request.Path);

            response.Headers["Access-Control-Allow-Origin"] = "*";

            if (!int.TryParse(fileName, out int fileId))
            {
                Debug.WriteLine(request.Path + ", master");

                if (extension == ".m3u8")
                {
                    var manifest = FromBase64(fileName);
                    response.Headers["Content-Type"] = "application/vnd.apple.mpegurl";
                    response.Headers["Content-Length"] = manifest.Length.ToString();
                    response.Content = manifest;
                    //response.OutputStream.Write(manifest, 0, manifest.Length);
                    //response.OutputStream.Close();
                }
                else
                {
                    response.Headers["Content-Type"] = "text/javascript";
                    response.Content = System.IO.File.ReadAllBytes(System.IO.Path.Combine(Package.Current.InstalledLocation.Path, "Assets\\hls.js"));
                }
                return response;
            }

            long offset = 0;
            long limit = 0;

            if (request.Headers.ContainsKey("Range") && System.Net.Http.Headers.RangeHeaderValue.TryParse(request.Headers["Range"], out var ranges))
            {
                foreach (var range in ranges.Ranges)
                {
                    offset = range.From ?? 0;
                    limit = range.To ?? -1;

                    limit = limit - offset + 1;
                    break;
                }
            }

            Debug.WriteLine(request.Path + ", offset: " + offset + ", length:" + limit);

            var file = _clientService.GetFileAsync(fileId).Result;
            var remote = new Telegram.Streams.RemoteFileSource(_clientService, file, extension == ".m3u8" ? 32 : 31, true);

            if (limit == 0)
            {
                limit = file.Size;
            }

            remote.SeekCallback(offset);
            remote.ReadCallback(limit);

            //var file = await _clientService.SendAsync(new DownloadFile(fileId, 15, offset, limit, true)) as File;
            //if (file == null || !file.Local.IsDownloadingCompleted)
            //{
            //    response.Close();
            //    return;
            //}

            if (extension == ".m3u8")
            {
                var text = System.IO.File.ReadAllText(file.Local.Path);
                var playlist = ToPlaylist(text, _map[fileId]);
                var bytes = Encoding.ASCII.GetBytes(playlist);

                response.Headers["Content-Type"] = "application/vnd.apple.mpegurl";
                response.Headers["Content-Length"] = bytes.Length.ToString();

                response.Content = bytes;
            }
            else
            {
                response.StatusCode = "206";
                response.Headers["Content-Type"] = "video/mp4";
                response.Headers["Content-Type"] = limit.ToString();

                using (var stream = new System.IO.FileStream(file.Local.Path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                {
                    stream.Seek(offset, System.IO.SeekOrigin.Begin);

                    byte[] buffer = new byte[(int)limit];
                    stream.Read(buffer, 0, buffer.Length);
                    response.Content = buffer;
                }
            }

            //response.OutputStream.Close();
            //response.OutputStream.Close();
            //response.Close();
            return response;
        }

        private string ToBase64(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            return System.Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private byte[] FromBase64(string base64)
        {
            string incoming = base64.Replace('_', '/').Replace('-', '+');
            switch (base64.Length % 4)
            {
                case 2: incoming += "=="; break;
                case 3: incoming += "="; break;
            }
            return Convert.FromBase64String(incoming);
            //return Encoding.ASCII.GetString(bytes);
        }

        private AdaptiveVideoSource _source;

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;

            //_source = new AdaptiveVideoSource(_clientService, _video);
            //_source.BitrateChanged += MediaSource_PlaybackBitrateChanged;
            //Element.Source = _source.CreateMediaSource();
            //Element.MediaPlayer.Play();
            //return;

            var playlist = ToPlaylist(_playlist);
            var bytes = Encoding.ASCII.GetBytes(playlist);

            var html = @"
<html>
  <head>
    <style> 
        body { 
            margin: 0; 
            padding: 0; 
        } 
    </style> 
  </head>

  <body>
    <script src=""http://localhost:8899/hls.js""></script>

    <video width=""100%"" height=""100%"" id=""video"" controls></video>

    <script>
      var video = document.getElementById('video');
      if (Hls.isSupported()) {
        var hls = new Hls({
          debug: true,
        });
        hls.loadSource('{0}');
        hls.attachMedia(video);
        hls.on(Hls.Events.MEDIA_ATTACHED, function () {
          video.muted = true;
          video.play();
        });
      }
      // hls.js is not supported on platforms that do not have Media Source Extensions (MSE) enabled.
      // When the browser has built-in HLS support (check using `canPlayType`), we can provide an HLS manifest (i.e. .m3u8 URL) directly to the video element through the `src` property.
      // This is using the built-in support of the plain video element, without using hls.js.
      else if (video.canPlayType('application/vnd.apple.mpegurl')) {
        video.src = '{0}';
        video.addEventListener('canplay', function () {
          video.play();
        });
      }
    </script>
</html>";

            html = html.Replace("{0}", "http://localhost:8899/" + ToBase64(playlist) + ".m3u8");

            //View.NavigateToString(html);
            //return;
        }

        private void Diagnostics_DiagnosticAvailable(AdaptiveMediaSourceDiagnostics sender, AdaptiveMediaSourceDiagnosticAvailableEventArgs args)
        {
            Debug.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(args));
        }

        private void MediaSource_PlaybackBitrateChanged(object sender, AdaptiveMediaSourcePlaybackBitrateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => Quality.Content = args.NewValue);
        }

        private readonly Regex _pattern = new("#EXT-X-BYTERANGE:(\\d+)@(\\d+)\\nmtproto:\\d+\\b", RegexOptions.Compiled);

        private string ToPlaylist(string manifest, int fileId)
        {
            return Regex.Replace(manifest, "mtproto:\\d+\\b", fileId + ".mp4");

            manifest = _pattern.Replace(manifest, match =>
            {
                return string.Format("#EXT-X-BYTERANGE:{0}@{1}\n{2}_{1}_{0}.ts", match.Groups[1].Value, match.Groups[2].Value, fileId);
            });

            return Regex.Replace(manifest, "mtproto:\\d+\\b", fileId + ".ts");
        }

        private string ToPlaylist(List<AlternativeVideo> videos)
        {
            var playlistString = new StringBuilder();
            playlistString.Append("#EXTM3U\n");
            //playlistString.Append("#EXT-X-VERSION:2\n");
            //playlistString.Append("#EXTINF:Test\n");

            foreach (var item in videos.OrderBy(x => x.Width * x.Height))
            {
                //for (quality, file) in self.qualityFiles.sorted(by: { $0.key > $1.key }) {
                int width = Math.Max(item.Width, 1280);
                int height = Math.Max(item.Height, 720);

                int bandwidth;

                if (_video.Video.Duration != 0)
                {
                    bandwidth = (int)((double)item.Video.Size / _video.Video.Duration) * 8;
                }
                else
                {
                    bandwidth = 1000000;
                }

                playlistString.Append($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={width}x{height}\n");
                playlistString.Append($"{item.HlsFile.Id}.m3u8\n");
            }

            return playlistString.ToString();
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void Button_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            flyout.CreateFlyoutItem(() =>
            {
                _source.DesiredBitrate = null;
            }, "Auto");

            foreach (var avail in _source.AvailableBitrates)
            {
                var local = avail + 0;
                flyout.CreateFlyoutItem(() =>
                {
                    _source.DesiredBitrate = local;
                }, avail.ToString());
            }

            flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.Bottom);
        }
    }
}
