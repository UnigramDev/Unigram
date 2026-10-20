//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Native.Media;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    public sealed partial class NativeVideoPlayer : VideoPlayerBase
    {
        private AsyncMediaPlayer _core;
        private GalleryMedia _video;

        private long _bufferedToken;

        private double _initialPosition;

        // Set when the core is the ffmpeg backend, the only one that moves between qualities.
        private bool _renditionsSupported;

        // The qualities of the video playing, sorted from the smallest, or null when it has none.
        private List<RemoteRendition> _renditions;
        private IClientService _clientService;
        private double _duration;

        private IList<VideoPlayerLevel> _levels;
        private VideoPlayerLevel _currentLevel;

        public NativeVideoPlayer()
        {
            InitializeComponent();
        }

        public override bool IsUnloadedExpected { get; set; }

        private void OnConnected(object sender, RoutedEventArgs e)
        {
            IsUnloadedExpected = false;

            if (_core == null)
            {
                var options = new AsyncMediaPlayerOptions
                {
                    Backend = AppSettings.Diagnostics.UseFFmpegPlayer
                        ? AsyncMediaPlayerBackend.FFmpeg
                        : AsyncMediaPlayerBackend.LibVlc,
                    CreateSwapChain = true,
                    Mute = AppSettings.VolumeMuted,
                    Volume = AppSettings.VolumeLevel,
                    Rate = AppSettings.Playback.VideoSpeed,
                    LogLevel = AppSettings.VerbosityLevel >= 4
                        ? AsyncMediaPlayerLogLevel.Debug
                        : AppSettings.VerbosityLevel >= 3
                        ? AsyncMediaPlayerLogLevel.Notice
                        : AppSettings.VerbosityLevel >= 2
                        ? AsyncMediaPlayerLogLevel.Warning
                        : AsyncMediaPlayerLogLevel.Error,
                };

                try
                {
                    _core = new AsyncMediaPlayer(options);
                }
                catch (Exception ex)
                {
                    // libvlc could not start: no plugin bank, or out of memory. Every member of
                    // this class already tolerates a null core, so leaving it unset degrades to
                    // a player that never plays instead of faulting on the next call into it.
                    Logger.Error(ex);
                    return;
                }

                _core.VideoOut += OnVout;
                _core.Stopped += OnStopped;
                _core.PositionChanged += OnTimeChanged;
                _core.DurationChanged += OnLengthChanged;
                _core.EndReached += OnEndReached;
                _core.Playing += OnPlaying;
                _core.Paused += OnPaused;
                _core.VolumeChanged += OnVolumeChanged;
                _core.StreamSelected += OnEESelected;
                _core.RenditionChanged += OnRenditionChanged;

                _renditionsSupported = options.Backend == AsyncMediaPlayerBackend.FFmpeg;

                _core.Context.Attach(Video, true);

                if (_video != null)
                {
                    Open(_video, _initialPosition);
                }

                _video = null;
                _initialPosition = 0;
            }
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            if (IsUnloadedExpected)
            {
                return;
            }

            if (_core != null)
            {
                _core.VideoOut -= OnVout;
                _core.Stopped -= OnStopped;
                _core.PositionChanged -= OnTimeChanged;
                _core.DurationChanged -= OnLengthChanged;
                _core.EndReached -= OnEndReached;
                _core.Playing -= OnPlaying;
                _core.Paused -= OnPaused;
                _core.VolumeChanged -= OnVolumeChanged;
                _core.StreamSelected -= OnEESelected;
                _core.RenditionChanged -= OnRenditionChanged;

                _core.Close();
                _core = null;
            }

            UpdateManager.Unsubscribe(this, ref _bufferedToken);
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
            IsLoopingEnabled = video.IsLoopingEnabled;

            if (_core == null)
            {
                _video = video;
                _initialPosition = position;
            }
            else
            {
                Open(video, position);
            }

            UpdateManager.Subscribe(this, video.ClientService, video.File, ref _bufferedToken, UpdateBuffered);
        }

        private void Open(GalleryMedia video, double position)
        {
            _renditions = _renditionsSupported ? CreateRenditions(video) : null;
            _clientService = video.ClientService;
            _duration = video.Duration;
            _levels = null;
            _currentLevel = null;

            // The player is reused across items, and one without qualities reports none.
            OnLevelsChanged(Array.Empty<VideoPlayerLevel>(), null);

            if (_renditions != null)
            {
                var rendition = PickRendition(_renditions);
                _core.Play(new List<IAsyncMediaPlayerRendition>(_renditions), rendition, !AppSettings.Playback.VideoQualityManual, position);
            }
            else
            {
                _core.Play(new RemoteFileSource(video.ClientService, video.File, video.Duration));
                _core.Position = position;
            }
        }

        private static List<RemoteRendition> CreateRenditions(GalleryMedia video)
        {
            if (!video.IsHls)
            {
                return null;
            }

            var renditions = new List<RemoteRendition>();

            foreach (var alternative in video.FindAlternatives("h264"))
            {
                if (alternative.Width > 0 && alternative.Height > 0 && alternative.HlsFile != null)
                {
                    renditions.Add(new RemoteRendition(video.ClientService, alternative.Video, alternative.HlsFile, alternative.Width, alternative.Height, video.Duration));
                }
            }

            if (renditions.Count == 0)
            {
                return null;
            }

            // The original only once it is on disk: then nothing plays better, and nothing more has
            // to be downloaded. Before that it is usually far larger than the best alternative.
            if (video.File.Local.IsDownloadingCompleted && video.Constraint is MessageVideo { Video: { } original })
            {
                renditions.Add(new RemoteRendition(video.ClientService, video.File, null, original.Width, original.Height, video.Duration));
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

        private static int PickRendition(List<RemoteRendition> renditions)
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

        private void OnRenditionChanged(AsyncMediaPlayer sender, object args)
        {
            var index = sender.Rendition;

            if (_renditions == null || index < 0 || index >= _renditions.Count)
            {
                return;
            }

            if (_levels == null)
            {
                var levels = new List<VideoPlayerLevel>(_renditions.Count);

                for (int i = 0; i < _renditions.Count; i++)
                {
                    var rendition = _renditions[i];
                    var bitrate = (int)Math.Min(int.MaxValue, rendition.Size * 8 / Math.Max(1, _duration));

                    levels.Add(new VideoPlayerLevel(i, bitrate, rendition.Width, rendition.Height, rendition.IsOriginal));
                }

                _levels = levels;
            }

            IsCurrentLevelAuto = sender.IsRenditionAutomatic;
            OnLevelsChanged(_levels, _currentLevel = _levels[index]);

            // Where the next video starts. Neither the original nor a quality already on disk, which
            // is picked for being there, says anything about the connection.
            if (IsCurrentLevelAuto && !_renditions[index].IsOriginal && !_renditions[index].File.Local.IsDownloadingCompleted)
            {
                AppSettings.Playback.VideoQuality = _renditions[index].Quality;
            }

            // The buffered bar follows the file actually playing.
            if (_clientService != null)
            {
                UpdateManager.Subscribe(this, _clientService, _renditions[index].File, ref _bufferedToken, UpdateBuffered);
            }
        }

        public override VideoPlayerLevel CurrentLevel
        {
            get => _currentLevel;
            set
            {
                if (_core == null || _renditions == null)
                {
                    return;
                }

                if (value == null)
                {
                    AppSettings.Playback.VideoQualityManual = false;
                    _core.SelectRendition(-1);
                }
                else if (value.Index >= 0 && value.Index < _renditions.Count)
                {
                    var rendition = _renditions[value.Index];

                    AppSettings.Playback.VideoQualityManual = true;

                    if (!rendition.IsOriginal)
                    {
                        AppSettings.Playback.VideoQuality = rendition.Quality;
                    }

                    _core.SelectRendition(value.Index);
                }
            }
        }

        private void UpdateBuffered(File update)
        {
            var offset = update.Local.DownloadOffset + update.Local.DownloadedPrefixSize;
            OnBufferedChanged(_buffered = (double)offset / update.Size);
        }

        public override void Play()
        {
            _core?.Play();
        }

        public override void Pause()
        {
            _core?.Pause();
        }

        public override void Toggle()
        {
            _core?.Toggle();
        }

        public override void Clear()
        {
            _core?.Context.Clear();
        }

        public override void Seek(double value)
        {
            _core?.Seek(value, true);
        }

        public override double Position
        {
            get => _core?.Position ?? 0;
            set
            {
                if (_core != null)
                {
                    _core.Position = value;
                    OnPositionChanged(value);
                }
            }
        }

        private double _buffered;
        public override double Buffered => _buffered;

        public override double Duration
        {
            get => _core?.Duration ?? 0;
        }

        public override bool IsPlaying
        {
            get => _core?.IsPlaying ?? false;
        }

        public override double Volume
        {
            get => _core?.Volume ?? 1;
            set
            {
                if (_core != null)
                {
                    _core.Volume = value;
                    OnVolumeChanged(value);
                }
            }
        }

        public override double Rate
        {
            get => _core?.Rate ?? 1;
            set
            {
                _core?.Rate = value;
            }
        }

        public override bool Mute
        {
            get => _core?.Mute ?? false;
            set
            {
                _core?.Mute = value;
            }
        }

        private void OnVout(AsyncMediaPlayer sender, object args)
        {
            OnFirstFrameReady(true);
        }

        private void OnStopped(AsyncMediaPlayer sender, object args)
        {
            OnIsPlayingChanged(false);

            if (sender.State == AsyncMediaPlayerState.Stopped)
            {
                OnClosed();
            }
        }

        private void OnTimeChanged(AsyncMediaPlayer sender, AsyncMediaPlayerPositionChangedEventArgs args)
        {
            OnPositionChanged(args.Position);
        }

        private void OnLengthChanged(AsyncMediaPlayer sender, AsyncMediaPlayerDurationChangedEventArgs args)
        {
            OnDurationChanged(args.Duration);
        }

        private void OnEndReached(AsyncMediaPlayer sender, object args)
        {
            OnPositionChanged(sender.Duration);

            if (IsLoopingEnabled)
            {
                // Play restarts an ended player by itself. A Stop first would make the ffmpeg
                // backend reopen the file, and leave a gap at every loop.
                sender.Play();
            }
            else
            {
                // An ended player is neither paused nor stopped, so neither of those events says so.
                OnIsPlayingChanged(false);
            }
        }

        private void OnPlaying(AsyncMediaPlayer sender, object args)
        {
            OnIsPlayingChanged(true);
        }

        private void OnPaused(AsyncMediaPlayer sender, object args)
        {
            OnIsPlayingChanged(false);
        }

        private void OnVolumeChanged(AsyncMediaPlayer sender, object args)
        {
            //OnVolumeChanged(args.Volume / 100d);
        }

        private bool _volumeWorkaround = true;

        private void OnEESelected(AsyncMediaPlayer sender, AsyncMediaPlayerStreamSelectedEventArgs args)
        {
            if (args.Type == AsyncMediaPlayerStreamType.Video && args.Id != -1)
            {
                OnTrackChanged(args.Width, args.Height);
            }
            else if (args.Type == AsyncMediaPlayerStreamType.Audio && args.Id != -1)
            {
                //if (_volumeWorkaround)
                //{
                //    _volumeWorkaround = false;
                //    OnReady(true);
                //}
            }
        }
    }
}
