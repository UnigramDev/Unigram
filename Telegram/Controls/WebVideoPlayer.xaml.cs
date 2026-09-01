//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    public sealed partial class WebVideoPlayer : VideoPlayerBase
    {
        private CoreWebView2 _core;
        private GalleryMedia _video;

        private readonly Dictionary<int, AlternativeVideo> _playlist = new();

        // Cancelled when the player goes away for good. A request in flight can be
        // waiting on a range that will never finish downloading, and it holds the
        // WebView2 deferral, the source and its download open until it returns.
        private readonly CancellationTokenSource _cancellation = new();

        private double _initialPosition;

        public WebVideoPlayer()
        {
            InitializeComponent();
        }

        protected override void OnLoaded()
        {
            IsUnloadedExpected = false;
        }

        protected override void OnUnloaded()
        {
            if (IsUnloadedExpected)
            {
                return;
            }

            _cancellation.Cancel();

            if (_core != null)
            {
                _core.NavigationCompleted -= OnNavigationCompleted;
                _core.WebResourceRequested -= OnWebResourceRequested;
                _core.WebMessageReceived -= OnWebMessageReceived;
                _core = null;
            }

            try
            {
                Video.Close();
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width != 0 && e.NewSize.Height != 0 && IsConnected)
            {
                OnTreeUpdated();
            }
        }

        public override void Play(GalleryMedia video, double position)
        {
            _video = video;
            _initialPosition = position;

            // Keyed by file id, and the ids of whatever played before are still in here
            // otherwise: the m3u8 handler looks the requested one up to rewrite the
            // playlist, and would find a segment belonging to the previous video.
            _playlist.Clear();

            foreach (var item in video.FindAlternatives("h264"))
            {
                _playlist[item.HlsFile.Id] = item;
            }

            if (_core == null)
            {
                _ = Video.EnsureCoreWebView2Async();
            }
            else
            {
                Navigate();
            }
        }

        private void Navigate()
        {
            if (_core == null || _video == null)
            {
                return;
            }

            _core.Navigate(_video.AlternativeVideos.Count > 0
                ? "http://127.0.0.1/hls.html"
                : "http://127.0.0.1/mp4.html");
        }

        public override void Play()
        {
            ExecuteScript("playerPlay();");
        }

        public override void Pause()
        {
            ExecuteScript("playerPause();");
        }

        public override void Toggle()
        {
            ExecuteScript("playerToggle()");
        }

        public override void Clear()
        {
            try
            {
                _core?.NavigateToString(string.Empty);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        public override void Seek(double value)
        {
            ExecuteScript($"playerAddTime({value.ToString(CultureInfo.InvariantCulture)})");
        }

        private double _position;
        public override double Position
        {
            get => _position;
            set
            {
                OnPositionChanged(_position = value);

                // Invariant, as everywhere a number is written into a script: a locale
                // with a comma for a decimal separator turns the argument into two.
                ExecuteScript($"playerSeek({value.ToString(CultureInfo.InvariantCulture)})");
            }
        }

        private double _buffered;
        public override double Buffered => _buffered;

        private double _duration;
        public override double Duration => _duration;

        private bool _isPlaying;
        public override bool IsPlaying => _isPlaying;

        private double _volume = 1;
        public override double Volume
        {
            get => _volume;
            set
            {
                OnVolumeChanged(_volume = value);
                ExecuteScript($"playerSetVolume({value.ToString(CultureInfo.InvariantCulture)})");
            }
        }

        private double _rate = 1;
        public override double Rate
        {
            get => _rate;
            set
            {
                _rate = value;
                ExecuteScript($"playerSetBaseRate({value.ToString(CultureInfo.InvariantCulture)})");
            }
        }

        private bool _mute;
        public override bool Mute
        {
            get => _mute;
            set
            {
                _mute = value;
                ExecuteScript($"playerSetIsMuted({(value ? 1 : 0)})");
            }
        }

        private VideoPlayerLevel _currentLevel;
        public override VideoPlayerLevel CurrentLevel
        {
            get => _currentLevel;
            set
            {
                IsCurrentLevelAuto = value == null;
                OnLevelsChanged(Levels, _currentLevel = value);
                ExecuteScript($"playerSetLevel({value?.Index ?? -1})");
            }
        }

        private void OnInitialized(WebView2 sender, CoreWebView2InitializedEventArgs e)
        {
            _core = sender.CoreWebView2;
            _core.NavigationCompleted += OnNavigationCompleted;
            _core.WebResourceRequested += OnWebResourceRequested;
            _core.WebMessageReceived += OnWebMessageReceived;

            if (AppSettings.Diagnostics.EnableWebViewDevTools)
            {
                _core.OpenDevToolsWindow();
            }

            _core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);

            Navigate();
        }

        private void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            var debug = AppSettings.Diagnostics.EnableWebViewDevTools ? "true" : "false";
            ExecuteScript("playerInitialize({debug:" + debug + "});playerPlay();");
        }

        private async void OnWebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();

            try
            {
                await ProcessWebResourceRequestAsync(sender, args, _cancellation.Token);
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
            finally
            {
                // The request stays outstanding until this runs, and the player stalls
                // waiting for a segment that is never answered.
                deferral.Complete();
            }
        }

        private async Task ProcessWebResourceRequestAsync(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args, CancellationToken cancellationToken)
        {
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

            void CreateWebResourceResponse(IRandomAccessStream Content, int StatusCode, string ReasonPhrase, string Headers)
            {
                try
                {
                    if (IsConnected && _core != null)
                    {
                        args.Response = sender.Environment.CreateWebResourceResponse(Content, StatusCode, ReasonPhrase, Headers);
                    }
                }
                catch
                {
                    // All bla bla bla
                }
            }

            if (resource == "favicon.ico")
            {
                CreateWebResourceResponse(null, 404, "Not Found", string.Empty);
                return;
            }

            if (resource == "video.mp4")
            {
                resource = _video.File.Id + ".mp4";
            }

            var fileName = System.IO.Path.GetFileNameWithoutExtension(resource);
            var extension = System.IO.Path.GetExtension(resource);

            if (resource == "hls.html" || resource == "hls.js" || resource == "mp4.html")
            {
                var file = await Package.Current.InstalledLocation.GetFileAsync("Assets\\" + resource);

                using (var stream = await file.OpenReadAsync())
                {
                    CreateWebResourceResponse(stream, 200, "OK", "Content-Type: " + file.ContentType);
                }
            }
            else if (resource == "master.m3u8")
            {
                var playlistString = new StringBuilder();
                playlistString.Append("#EXTM3U\n");

                var duration = Math.Max(_video.Duration, 1);

                foreach (var item in _playlist.Values.OrderBy(x => x.Width * x.Height))
                {
                    int width = item.Width;
                    int height = item.Height;

                    // Only used by hls.js to rank the variants, so the average is enough.
                    // Guarded against a missing duration, which would divide by zero and
                    // make every variant look the same.
                    int bandwidth = (int)(item.Video.Size * 8 / duration);

                    playlistString.Append($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={width}x{height}\n");
                    playlistString.Append($"{item.HlsFile.Id}.m3u8\n");
                }

                using (var stream = await ToStreamAsync(playlistString.ToString()))
                {
                    CreateWebResourceResponse(stream, 200, "OK", "Content-Type: application/vnd.apple.mpegurl");
                }
            }
            else if (int.TryParse(fileName, out int fileId))
            {
                var file = await _video.ClientService.GetFileAsync(fileId);
                if (file == null || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var query = new Uri(args.Request.Uri).Query.ParseQueryString();

                double duration = 0;
                if (query.TryGetValue("duration", out string durationValue))
                {
                    double.TryParse(durationValue, NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
                }

                args.Request.Headers.TryGetValue("Range", out string range);

                var media = MediaRange.Parse(range, file.Size, duration);
                if (media.Count == 0)
                {
                    CreateWebResourceResponse(null, 416, "Range Not Satisfiable", string.Empty);
                    return;
                }

                //Logger.Info(resource + ", offset: " + media.Offset + ", count:" + media.Count);

                // hls.js asks for explicit ranges and this source lives for one of them,
                // so the window is the request's to choose rather than measured here.
                var remote = new RemoteFileSource(_video.ClientService, file, _video.Duration, streaming: false);
                remote.SeekCallback(media.Offset);

                long bytesRead;

                // Closing is what ends the wait: the read blocks until the range is
                // downloaded, and once the player is gone nothing else ever completes it.
                using (cancellationToken.Register(remote.Close))
                {
                    bytesRead = await remote.ReadCallbackAsync(media.Count, media.Window);
                }

                remote.Close();

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (bytesRead >= 0)
                {
                    if (extension == ".m3u8")
                    {
                        try
                        {
                            var storage = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                            var text = await FileIO.ReadTextAsync(storage);
                            var playlist = Regex.Replace(text, "mtproto:\\d+\\b", _playlist[fileId].Video.Id + ".mp4");

                            using (var stream = await ToStreamAsync(playlist))
                            {
                                CreateWebResourceResponse(stream, 200, "OK", "Content-Type: application/vnd.apple.mpegurl");
                            }
                        }
                        catch
                        {
                            // TODO: file name changes when download is completed and a race seems to be happening some times.
                        }
                    }
                    else
                    {
                        try
                        {
                            var storage = await StorageFile.GetFileFromPathAsync(file.Local.Path);
                            using (var stream = await storage.OpenReadAsync())
                            using (var destination = new InMemoryRandomAccessStream())
                            {
                                stream.Seek((ulong)media.Offset);

                                // Copied straight from the file into the response, rather
                                // than through a buffer holding the whole range a second
                                // time. A response runs to megabytes, and the intermediate
                                // doubled that for as long as the two were both alive.
                                var copied = await RandomAccessStream.CopyAsync(stream, destination, (ulong)media.Count);

                                if (copied > 0)
                                {
                                    // Describes what the body actually carries. The file is
                                    // still downloading, so the copy can come up short of
                                    // what was asked for, and a range promising more leaves
                                    // the player waiting on bytes that are never sent.
                                    CreateWebResourceResponse(destination, 206, "OK", string.Format("Content-Type: video/mp4\nContent-Range: bytes {0}-{1}/{2}", media.Offset, media.Offset + (long)copied - 1, file.Size));
                                }
                                else
                                {
                                    CreateWebResourceResponse(null, 404, "Not Found", string.Empty);
                                }
                            }
                        }
                        catch
                        {
                            // TODO: file name changes when download is completed and a race seems to be happening some times.
                        }
                    }
                }
                else
                {
                    CreateWebResourceResponse(null, 404, "Not Found", string.Empty);
                }
            }
        }

        private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            // TODO: some clear nonsense from WebView2 here:
            // The JSON string received from mini apps (WebViewer.cs)
            // fails to be parsed by JsonArray.TryParse if retrieved using WebMessageAsJson.
            // The JSON string received from HLS player (WebVideoPlayer.xaml.cs)
            // throws a random exception if retrieved using TryGetWebMessageAsString.

            if (JsonObject.TryParse(args.WebMessageAsJson, out JsonObject data))
            {
                var eventName = data.GetNamedString("event", string.Empty);
                var eventData = data.GetNamedObject("data", new JsonObject());

                if (eventName == "playerStatus")
                {
                    OnReady(_isReady = eventData.GetNamedBoolean("isReady", false));
                    OnFirstFrameReady(_isFirstFrameReady = eventData.GetNamedBoolean("isFirstFrameReady", false));
                    OnIsPlayingChanged(_isPlaying = eventData.GetNamedBoolean("isPlaying", false));
                    _rate = eventData.GetNamedNumber("rate", 1);
                    var defaultRate = eventData.GetNamedNumber("defaultRate", 1);
                    OnVolumeChanged(_volume = eventData.GetNamedNumber("volume", 1));
                    OnDurationChanged(_duration = eventData.GetNamedNumber("duration", 0));

                    // Seeking is setting currentTime, which the element only honours once
                    // it knows how long the media is, so the position the gallery opened
                    // at waits here for the first status that reports a duration.
                    if (_initialPosition > 0 && _duration > 0)
                    {
                        Position = _initialPosition;
                        _initialPosition = 0;
                    }

                    if (eventData.ContainsKey("levels"))
                    {
                        var levels = eventData.GetNamedArray("levels");
                        var currentLevel = eventData.GetNamedInt32("currentLevel", -1);

                        var mapped = new List<VideoPlayerLevel>(levels.Count);

                        foreach (var level in levels.Select(x => x.GetObject()))
                        {
                            mapped.Add(new VideoPlayerLevel(level));
                        }

                        OnLevelsChanged(mapped, _currentLevel = mapped.Count == 0 || currentLevel == -1 ? null : mapped[currentLevel]);
                    }
                }
                else if (eventName == "playerCurrentTime")
                {
                    OnPositionChanged(_position = eventData.GetNamedNumber("value", 0));
                    OnBufferedChanged(_buffered = eventData.GetNamedNumber("buffered", 0) / Math.Max(_duration, 1));
                }
                else if (eventName == "playerError")
                {
                    OnFailed();
                }
            }
        }

        private void ExecuteScript(string script)
        {
            if (_core != null)
            {
                _ = _core.ExecuteScriptAsync(script);
            }
        }
    }
}
