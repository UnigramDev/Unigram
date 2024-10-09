using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using System;
using Telegram.Common;
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

        private long _initialPosition;

        public NativeVideoPlayer()
        {
            InitializeComponent();
        }

        private bool _isUnloadedExpected;
        public override bool IsUnloadedExpected
        {
            get => Video.IsUnloadedExpected;
            set => Video.IsUnloadedExpected = value;
        }

        private void OnConnected(object sender, RoutedEventArgs e)
        {
            IsUnloadedExpected = false;
        }

        private void OnDisconnected(object sender, RoutedEventArgs e)
        {
            if (IsUnloadedExpected)
            {
                return;
            }

            if (_core != null)
            {
                _core.Vout -= OnVout;
                _core.Stopped -= OnStopped;
                _core.TimeChanged -= OnTimeChanged;
                _core.LengthChanged -= OnLengthChanged;
                _core.EndReached -= OnEndReached;
                _core.Playing -= OnPlaying;
                _core.Paused -= OnPaused;
                _core.VolumeChanged -= OnVolumeChanged;

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
                _initialPosition = (long)position;
            }
            else
            {
                _core.Play(new RemoteFileStream(video.ClientService, video.File));
                _core.Time = (long)position;
            }

            UpdateManager.Subscribe(this, video.ClientService, video.File, ref _bufferedToken, UpdateBuffered);
        }

        private void UpdateBuffered(object target, File update)
        {
            var offset = update.Local.DownloadOffset + update.Local.DownloadedPrefixSize;
            OnBufferedChanged(_buffered = update.Local.IsDownloadingCompleted || offset == update.Size ? 0 : offset / update.Size);
        }

        public override void Play()
        {
            //_player?.Play();
            switch (_core.State)
            {
                case VLCState.Ended:
                    _core.Stop();
                    goto case VLCState.Stopped;
                case VLCState.Paused:
                case VLCState.Stopped:
                case VLCState.Error:
                    _core.Play();
                    break;
            }
        }

        public override void Pause()
        {
            _core?.Pause();
        }

        public override void Toggle()
        {
            if (_core == null)
            {
                return;
            }

            switch (_core.State)
            {
                case VLCState.Ended:
                    _core.Stop();
                    goto case VLCState.Stopped;
                case VLCState.Paused:
                case VLCState.Stopped:
                case VLCState.Error:
                    _core.Play();
                    break;
                default:
                    _core.Pause();
                    break;
            }
        }

        public override void Stop()
        {
            _core?.Stop();
        }

        public override void Clear()
        {
            Video.Clear();
        }

        public override void AddTime(double value)
        {
            _core?.AddTime((long)value);
        }

        public override double Position
        {
            get => _core?.Time / 1000d ?? 0;
            set
            {
                if (_core != null)
                {
                    _core.Time = (long)(value * 1000);
                    OnPositionChanged(value);
                }
            }
        }

        private double _buffered;
        public override double Buffered => _buffered;

        public override double Duration
        {
            get => _core?.Length / 1000d ?? 0;
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
                    _core.Volume = (int)(value * 100);
                    OnVolumeChanged(value);
                }
            }
        }

        public override double Rate
        {
            get => _core?.Rate ?? 1;
            set
            {
                if (_core != null)
                {
                    _core.Rate = (float)(value);
                    //OnRateChanged(value);
                }
            }
        }

        public override bool Mute
        {
            get => _core?.Mute ?? false;
            set
            {
                if (_core != null)
                {
                    _core.Mute = value;
                    //OnMuteChanged(value);
                }
            }
        }

        private void OnInitialized(object sender, InitializedEventArgs e)
        {
            _core = new AsyncMediaPlayer(e.SwapChainOptions);
            _core.Vout += OnVout;
            _core.Stopped += OnStopped;
            _core.TimeChanged += OnTimeChanged;
            _core.LengthChanged += OnLengthChanged;
            _core.EndReached += OnEndReached;
            _core.Playing += OnPlaying;
            _core.Paused += OnPaused;
            _core.VolumeChanged += OnVolumeChanged;

            if (_video != null)
            {
                _core.Play(new RemoteFileStream(_video.ClientService, _video.File));
                _core.Time = _initialPosition;
            }

            _video = null;
            _initialPosition = 0;
        }

        private void OnVout(AsyncMediaPlayer sender, EventArgs args)
        {
            OnFirstFrameReady(true);
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
            OnPositionChanged(args.Time / 1000d);
        }

        private void OnLengthChanged(AsyncMediaPlayer sender, MediaPlayerLengthChangedEventArgs args)
        {
            OnDurationChanged(args.Length / 1000d);
        }

        private void OnEndReached(AsyncMediaPlayer sender, EventArgs args)
        {
            OnPositionChanged(sender.Length / 1000d);

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

        private bool _volumeWorkaround = true;

        private void OnVolumeChanged(AsyncMediaPlayer sender, MediaPlayerVolumeChangedEventArgs args)
        {
            if (_volumeWorkaround)
            {
                OnReady(true);
            }
            else
            {
                OnVolumeChanged(args.Volume / 100d);
            }
        }
    }
}
