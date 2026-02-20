//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.ViewModels.Gallery;
using Windows.Data.Json;
using Windows.Foundation;

namespace Telegram.Controls
{
    public record VideoPlayerPositionChangedEventArgs(double Position);

    public record VideoPlayerBufferedChangedEventArgs(double Buffered);

    public record VideoPlayerDurationChangedEventArgs(double Duration);

    public record VideoPlayerIsPlayingChangedEventArgs(bool IsPlaying);

    public record VideoPlayerVolumeChangedEventArgs(double Volume);

    public partial class VideoPlayerTrack
    {
        public VideoPlayerTrack(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }
    }

    // Record somehow fails to compile in release
    public partial class VideoPlayerLevel
    {
        public VideoPlayerLevel(int index, int bitrate, int width, int height)
        {
            Index = index;
            Bitrate = bitrate;
            Width = width;
            Height = height;
        }

        public VideoPlayerLevel(JsonObject level)
        {
            Index = level.GetNamedInt32("index", 0);
            Bitrate = level.GetNamedInt32("bitrate", 100000);
            Width = level.GetNamedInt32("width", 1280);
            Height = level.GetNamedInt32("height", 720);
        }

        public int Index { get; }

        public int Bitrate { get; }

        public int Width { get; }

        public int Height { get; }

        public string ToP()
        {
            return string.Format("{0}p", Math.Min(Width, Height));
        }
    }

    public record VideoPlayerLevelsChangedEventArgs(IList<VideoPlayerLevel> Levels, VideoPlayerLevel CurrentLevel, bool IsAuto);

    public record VideoPlayerTrackChangedEventArgs(int Width, int Height);

    public abstract partial class VideoPlayerBase : UserControlEx
    {
        private double _rate;
        public abstract double Rate { get; set; }

        private bool _mute;
        public abstract bool Mute { get; set; }

        public bool IsLoopingEnabled { get; set; }

        public virtual bool IsUnloadedExpected { get; set; }

        public abstract void Play(GalleryMedia video, double position);

        public abstract void Play();

        public abstract void Pause();

        public abstract void Toggle();

        public abstract void Clear();

        public abstract void Seek(double value);

        protected bool _isReady;

        public event TypedEventHandler<VideoPlayerBase, EventArgs> Ready;
        protected void OnReady(bool value)
        {
            if (/*_isReady != value &&*/ value)
            {
                _isReady = value;
                Ready?.Invoke(this, EventArgs.Empty);
            }
        }

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

        private double _buffered;
        public abstract double Buffered { get; }

        public event TypedEventHandler<VideoPlayerBase, VideoPlayerBufferedChangedEventArgs> BufferedChanged;
        protected void OnBufferedChanged(double value)
        {
            //if (_position != value)
            {
                _buffered = value;
                BufferedChanged?.Invoke(this, new VideoPlayerBufferedChangedEventArgs(value));
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

        public virtual VideoPlayerLevel CurrentLevel { get; set; }

        public IList<VideoPlayerLevel> Levels { get; private set; } = Array.Empty<VideoPlayerLevel>();

        public VideoPlayerTrack Track { get; private set; }

        public event TypedEventHandler<VideoPlayerBase, VideoPlayerLevelsChangedEventArgs> LevelsChanged;
        protected void OnLevelsChanged(IList<VideoPlayerLevel> levels, VideoPlayerLevel currentLevel)
        {
            Levels = levels ?? Array.Empty<VideoPlayerLevel>();
            LevelsChanged?.Invoke(this, new VideoPlayerLevelsChangedEventArgs(levels, currentLevel, IsCurrentLevelAuto));
        }

        public event TypedEventHandler<VideoPlayerBase, VideoPlayerTrackChangedEventArgs> TrackChanged;
        protected void OnTrackChanged(int width, int height)
        {
            Track = new VideoPlayerTrack(width, height);
            TrackChanged?.Invoke(this, new VideoPlayerTrackChangedEventArgs(width, height));
        }

        public event TypedEventHandler<VideoPlayerBase, EventArgs> Closed;
        protected void OnClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public event TypedEventHandler<VideoPlayerBase, EventArgs> Failed;
        protected void OnFailed()
        {
            Failed?.Invoke(this, EventArgs.Empty);
        }

        public event TypedEventHandler<VideoPlayerBase, EventArgs> TreeUpdated;
        protected void OnTreeUpdated()
        {
            TreeUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
