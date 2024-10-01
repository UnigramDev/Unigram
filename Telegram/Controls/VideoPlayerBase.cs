using System;
using System.Collections.Generic;
using Telegram.ViewModels.Gallery;
using Windows.Foundation;

namespace Telegram.Controls
{
    public record VideoPlayerPositionChangedEventArgs(double Position);

    public record VideoPlayerDurationChangedEventArgs(double Duration);

    public record VideoPlayerIsPlayingChangedEventArgs(bool IsPlaying);

    public record VideoPlayerVolumeChangedEventArgs(double Volume);

    public record VideoPlayerLevel(int Index, int Bitrate, int Width, int Height);

    public record VideoPlayerLevelsChangedEventArgs(IList<VideoPlayerLevel> Levels, VideoPlayerLevel CurrentLevel, bool IsAuto);

    public abstract partial class VideoPlayerBase : UserControlEx
    {
        private double _rate;
        public abstract double Rate { get; set; }

        private bool _mute;
        public abstract bool Mute { get; set; }

        public bool IsLoopingEnabled { get; set; }

        public abstract void Play(GalleryMedia video, double position);

        public abstract void Play();

        public abstract void Pause();

        public abstract void Toggle();

        public abstract void Stop();

        public abstract void Clear();

        public abstract void AddTime(double value);

        protected bool _isFirstFrameReady;

        public event TypedEventHandler<VideoPlayerBase, EventArgs> FirstFrameReady;
        protected void OnFirstFrameReady(bool value)
        {
            if (/*_isFirstFrameReady != value &&*/ value)
            {
                _isFirstFrameReady = value;
                FirstFrameReady?.Invoke(this, EventArgs.Empty);
            }
        }

        private double _position;
        public abstract double Position { get; set; }

        public event TypedEventHandler<VideoPlayerBase, VideoPlayerPositionChangedEventArgs> PositionChanged;
        protected void OnPositionChanged(double value)
        {
            //if (_position != value)
            {
                _position = value;
                PositionChanged?.Invoke(this, new VideoPlayerPositionChangedEventArgs(value));
            }
        }

        private double _duration;
        public abstract double Duration { get; }

        public event TypedEventHandler<VideoPlayerBase, VideoPlayerDurationChangedEventArgs> DurationChanged;
        protected void OnDurationChanged(double value)
        {
            //if (_duration != value)
            {
                _duration = value;
                DurationChanged?.Invoke(this, new VideoPlayerDurationChangedEventArgs(value));
            }
        }

        private bool _isPlaying;
        public abstract bool IsPlaying { get; }

        public event TypedEventHandler<VideoPlayerBase, VideoPlayerIsPlayingChangedEventArgs> IsPlayingChanged;
        protected void OnIsPlayingChanged(bool value)
        {
            //if (_isPlaying != value)
            {
                _isPlaying = value;
                IsPlayingChanged?.Invoke(this, new VideoPlayerIsPlayingChangedEventArgs(value));
            }
        }

        private double _volume;
        public abstract double Volume { get; set; }

        public event TypedEventHandler<VideoPlayerBase, VideoPlayerVolumeChangedEventArgs> VolumeChanged;
        protected void OnVolumeChanged(double value)
        {
            //if (_volume != value)
            {
                _volume = value;
                VolumeChanged?.Invoke(this, new VideoPlayerVolumeChangedEventArgs(value));
            }
        }

        public bool IsCurrentLevelAuto { get; protected set; } = true;

        public abstract VideoPlayerLevel CurrentLevel { get; set; }

        public IList<VideoPlayerLevel> Levels { get; private set; } = Array.Empty<VideoPlayerLevel>();

        public event TypedEventHandler<VideoPlayerBase, VideoPlayerLevelsChangedEventArgs> LevelsChanged;
        protected void OnLevelsChanged(IList<VideoPlayerLevel> levels, VideoPlayerLevel currentLevel)
        {
            Levels = levels;
            LevelsChanged?.Invoke(this, new VideoPlayerLevelsChangedEventArgs(levels, currentLevel, IsCurrentLevelAuto));
        }

        public event TypedEventHandler<VideoPlayerBase, EventArgs> Closed;
        protected void OnClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
