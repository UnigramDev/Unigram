using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using System;
using Telegram.Common;
using Telegram.Streams;
using Telegram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace Telegram.Controls
{
    public sealed partial class NativeVideoPlayer : VideoPlayerBase
    {
        private AsyncMediaPlayer _player;
        private GalleryMedia _video;

        private long _initialPosition;

        public NativeVideoPlayer()
        {
            InitializeComponent();
            Disconnected += OnDisconnected;
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            if (_player != null)
            {
                _player.Vout -= OnVout;
                _player.Stopped -= OnStopped;
                _player.TimeChanged -= OnTimeChanged;
                _player.LengthChanged -= OnLengthChanged;
                _player.EndReached -= OnEndReached;
                _player.Playing -= OnPlaying;
                _player.Paused -= OnPaused;
                _player.VolumeChanged -= OnVolumeChanged;

                _player.Close();
                _player = null;
            }
        }

        public override void Play(GalleryMedia video, double position)
        {
            if (_player == null)
            {
                _video = video;
                _initialPosition = (long)position;
            }
            else
            {
                _player.Play(new RemoteFileStream(video.ClientService, video.GetFile()));
                _player.Time = (long)position;
            }
        }

        public override void Play()
        {
            //_player?.Play();
            switch (_player.State)
            {
                case VLCState.Ended:
                    _player.Stop();
                    goto case VLCState.Stopped;
                case VLCState.Paused:
                case VLCState.Stopped:
                case VLCState.Error:
                    _player.Play();
                    break;
            }
        }

        public override void Pause()
        {
            _player?.Pause();
        }

        public override void Toggle()
        {
            if (_player == null)
            {
                return;
            }

            switch (_player.State)
            {
                case VLCState.Ended:
                    _player.Stop();
                    goto case VLCState.Stopped;
                case VLCState.Paused:
                case VLCState.Stopped:
                case VLCState.Error:
                    _player.Play();
                    break;
                default:
                    _player.Pause();
                    break;
            }
        }

        public override void Stop()
        {
            _player?.Stop();
        }

        public override void Clear()
        {
            Video.Clear();
        }

        public override void AddTime(double value)
        {
            _player?.AddTime((long)value);
        }

        public override double Position
        {
            get => _player?.Time ?? 0;
            set
            {
                if (_player != null)
                {
                    _player.Time = (long)value;
                    OnPositionChanged(value);
                }
            }
        }

        public override double Duration
        {
            get => _player?.Length ?? 0;
        }

        public override bool IsPlaying
        {
            get => _player?.IsPlaying ?? false;
        }

        public override double Volume
        {
            get => _player?.Volume ?? 1;
            set
            {
                if (_player != null)
                {
                    _player.Volume = (int)(value * 100);
                    OnVolumeChanged(value);
                }
            }
        }

        public override double Rate
        {
            get => _player?.Rate ?? 1;
            set
            {
                if (_player != null)
                {
                    _player.Rate = (float)(value);
                    //OnRateChanged(value);
                }
            }
        }

        public override bool Mute
        {
            get => _player?.Mute ?? false;
            set
            {
                if (_player != null)
                {
                    _player.Mute = value;
                    //OnMuteChanged(value);
                }
            }
        }

        private void OnInitialized(object sender, InitializedEventArgs e)
        {
            _player = new AsyncMediaPlayer(e.SwapChainOptions);
            _player.Vout += OnVout;
            _player.Stopped += OnStopped;
            _player.TimeChanged += OnTimeChanged;
            _player.LengthChanged += OnLengthChanged;
            _player.EndReached += OnEndReached;
            _player.Playing += OnPlaying;
            _player.Paused += OnPaused;
            _player.VolumeChanged += OnVolumeChanged;

            if (_video != null)
            {
                _player.Play(new RemoteFileStream(_video.ClientService, _video.GetFile()));
                _player.Time = _initialPosition;
            }

            _video = null;
            _initialPosition = 0;
        }

        private void OnVout(AsyncMediaPlayer sender, EventArgs args)
        {
            OnFirstFrameReady();
        }

        private void OnStopped(AsyncMediaPlayer sender, EventArgs args)
        {
            OnIsPlayingChanged(false);

            if (sender.State == VLCState.Stopped)
            {
                OnClosed();
            }
        }

        private void OnTimeChanged(AsyncMediaPlayer sender, MediaPlayerTimeChangedEventArgs args)
        {
            OnPositionChanged(args.Time);
        }

        private void OnLengthChanged(AsyncMediaPlayer sender, MediaPlayerLengthChangedEventArgs args)
        {
            OnDurationChanged(args.Length);
        }

        private void OnEndReached(AsyncMediaPlayer sender, EventArgs args)
        {
            OnPositionChanged(sender.Length);

            if (IsLoopingEnabled)
            {
                sender.Stop();
                sender.Play();
            }
        }

        private void OnPlaying(AsyncMediaPlayer sender, EventArgs args)
        {
            OnIsPlayingChanged(true);
        }

        private void OnPaused(AsyncMediaPlayer sender, EventArgs args)
        {
            OnIsPlayingChanged(false);
        }

        private void OnVolumeChanged(AsyncMediaPlayer sender, MediaPlayerVolumeChangedEventArgs args)
        {
            OnVolumeChanged(args.Volume);
        }
    }
}
