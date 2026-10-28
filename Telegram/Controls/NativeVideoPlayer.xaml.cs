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
                    CreateSwapChain = true,
                    Mute = AppSettings.VolumeMuted,
                    Volume = AppSettings.VolumeLevel,
                    Rate = AppSettings.Playback.VideoSpeed,
                };

                try
                {
                    _core = new AsyncMediaPlayer(options);
                }
                catch (Exception ex)
                {
                    // Out of memory, most likely. Every member of this class already tolerates a
                    // null core, so leaving it unset degrades to a player that never plays instead
                    // of faulting on the next call into it.
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
            _renditions = video.Constraint is MessageVideo messageVideo
                ? RemoteRendition.CreateRenditions(video.ClientService, messageVideo)
                : null;
            _clientService = video.ClientService;
            _duration = video.Duration;
            _levels = null;
            _currentLevel = null;

            // The player is reused across items, and one without qualities reports none.
            OnLevelsChanged(Array.Empty<VideoPlayerLevel>(), null);

            if (_renditions != null)
            {
                var rendition = RemoteRendition.PickRendition(_renditions);
                _core.Play(new List<IAsyncMediaPlayerRendition>(_renditions), rendition, !AppSettings.Playback.VideoQualityManual, position);
            }
            else
            {
                _core.Play(new RemoteFileSource(video.ClientService, video.File, video.Duration));
                _core.Position = position;
            }
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
                // Play restarts an ended player by itself. A Stop first would make the player
                // reopen the file, and leave a gap at every loop.
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

        private void OnEESelected(AsyncMediaPlayer sender, AsyncMediaPlayerStreamSelectedEventArgs args)
        {
            if (args.Type == AsyncMediaPlayerStreamType.Video && args.Id != -1)
            {
                OnTrackChanged(args.Width, args.Height);
            }
        }
    }
}
