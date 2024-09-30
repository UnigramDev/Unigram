using System;
using Telegram.ViewModels.Gallery;
using Windows.Foundation;

namespace Telegram.Controls
{
    public record VideoPlayerPositionChanged(double Position);

    public record VideoPlayerDurationChanged(double Duration);

    public record VideoPlayerIsPlayingChanged(bool IsPlaying);

    public record VideoPlayerVolumeChanged(double Volume);

    public abstract partial class VideoPlayerBase : UserControlEx
    {
        private double _position;
        public abstract double Position { get; set; }

        private double _duration;
        public abstract double Duration { get; }

        private bool _isPlaying;
        public abstract bool IsPlaying { get; }

        private double _volume;
        public abstract double Volume { get; set; }

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

        public event TypedEventHandler<VideoPlayerBase, EventArgs> FirstFrameReady;
        public event TypedEventHandler<VideoPlayerBase, VideoPlayerPositionChanged> PositionChanged;
        public event TypedEventHandler<VideoPlayerBase, VideoPlayerDurationChanged> DurationChanged;
        public event TypedEventHandler<VideoPlayerBase, VideoPlayerIsPlayingChanged> IsPlayingChanged;
        public event TypedEventHandler<VideoPlayerBase, VideoPlayerVolumeChanged> VolumeChanged;
        public event TypedEventHandler<VideoPlayerBase, EventArgs> Closed;

        protected void OnFirstFrameReady()
        {
            FirstFrameReady?.Invoke(this, EventArgs.Empty);
        }

        protected void OnPositionChanged(double value)
        {
            //if (_position != value)
            {
                _position = value;
                PositionChanged?.Invoke(this, new VideoPlayerPositionChanged(value));
            }
        }

        protected void OnDurationChanged(double value)
        {
            //if (_duration != value)
            {
                _duration = value;
                DurationChanged?.Invoke(this, new VideoPlayerDurationChanged(value));
            }
        }

        protected void OnIsPlayingChanged(bool value)
        {
            //if (_isPlaying != value)
            {
                _isPlaying = value;
                IsPlayingChanged?.Invoke(this, new VideoPlayerIsPlayingChanged(value));
            }
        }

        protected void OnVolumeChanged(double value)
        {
            //if (_volume != value)
            {
                _volume = value;
                VolumeChanged?.Invoke(this, new VideoPlayerVolumeChanged(value));
            }
        }

        protected void OnClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
