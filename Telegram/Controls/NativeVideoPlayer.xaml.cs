//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

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
                    Mute = SettingsService.Current.VolumeMuted,
                    Volume = SettingsService.Current.VolumeLevel,
                    Rate = SettingsService.Current.Playback.VideoSpeed,
                    Debug = SettingsService.Current.VerbosityLevel >= 4,
                };

                _core = new AsyncMediaPlayer(options);
                _core.VideoOut += OnVout;
                _core.Stopped += OnStopped;
                _core.PositionChanged += OnTimeChanged;
                _core.DurationChanged += OnLengthChanged;
                _core.EndReached += OnEndReached;
                _core.Playing += OnPlaying;
                _core.Paused += OnPaused;
                _core.VolumeChanged += OnVolumeChanged;
                _core.StreamSelected += OnEESelected;

                _core.Context.Attach(Video, true);

                if (_video != null)
                {
                    _core.Play(new RemoteFileSource(_video.ClientService, _video.File, _video.Duration));
                    _core.Position = _initialPosition;
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
            if (_core == null)
            {
                _video = video;
                _initialPosition = position;
            }
            else
            {
                _core.Play(new RemoteFileSource(video.ClientService, video.File, video.Duration));
                _core.Position = position;
            }

            UpdateManager.Subscribe(this, video.ClientService, video.File, ref _bufferedToken, UpdateBuffered);
        }

        private void UpdateBuffered(object target, File update)
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
                sender.Stop();
                sender.Play();
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
