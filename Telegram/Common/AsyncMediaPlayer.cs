﻿//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using Telegram.Streams;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;

namespace Telegram.Common
{
    public class AsyncMediaPlayer
    {
        private readonly DispatcherQueue _dispatcherQueue;

        private readonly LibVLC _library;
        private readonly MediaPlayer _player;

        private readonly object _closeLock = new();
        private bool _closed;

        public AsyncMediaPlayer(params string[] options)
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // Generating plugins cache requires a breakpoint in bank.c#662
            _library = new LibVLC(options); //"--quiet", "--reset-plugins-cache");
            //_library.Log += _library_Log;

            _player = new MediaPlayer(_library);

            // Stories
            _player.ESSelected += OnESSelected;
            _player.Vout += OnVout;
            _player.Buffering += OnBuffering;
            _player.EndReached += OnEndReached;

            // Gallery
            _player.TimeChanged += OnTimeChanged;
            _player.LengthChanged += OnLengthChanged;
            //_player.EndReached += OnEndReached;
            _player.Playing += OnPlaying;
            _player.Paused += OnPaused;
            _player.Stopped += OnStopped;
            _player.VolumeChanged += OnVolumeChanged;

            //_player.FileCaching = 1;
        }

        public void Play(RemoteFileStream stream)
        {
            Write(() => PlayImpl(stream), true);
        }

        private void PlayImpl(RemoteFileStream stream)
        {
            _player.Play(new Media(_library, stream));
        }

        public void Play()
        {
            Write(() => PlayImpl());
        }

        private void PlayImpl()
        {
            _player.Play();
        }

        public void Stop()
        {
            Write(StopImpl);
        }

        private void StopImpl()
        {
            _player.Stop();
        }

        public void Pause(bool pause = true)
        {
            Write(() => PauseImpl(pause));
        }

        private void PauseImpl(bool pause)
        {
            _player.SetPause(pause);
        }

        public VLCState State => Read(() => _player.State);

        public bool IsPlaying => Read(() => _player.IsPlaying);

        public bool Mute
        {
            get => Read(() => _player.Mute);
            set => Write(() => _player.Mute = value);
        }

        public long Length => Read(() => _player.Length);

        public long Time
        {
            get => Read(() => _player.Time);
            set => Write(() => _player.Time = value);
        }

        public void AddTime(long value)
        {
            Write(() => _player.Time += value);
        }

        public float Scale
        {
            get => Read(() => _player.Scale);
            set => Write(() => _player.Scale = value);
        }

        public float Rate
        {
            get => Read(() => _player.Rate);
            set => Write(() => _player.Rate = value);
        }

        public int Volume
        {
            get => Read(() => _player.Volume);
            set => Write(() => _player.Volume = value);
        }

        public void Close()
        {
            _workQueue.Clear();
            Write(CloseImpl);
        }

        private void CloseImpl()
        {
            _player.ESSelected -= OnESSelected;
            _player.Vout -= OnVout;
            _player.Buffering -= OnBuffering;
            _player.EndReached -= OnEndReached;
            _player.TimeChanged -= OnTimeChanged;
            _player.LengthChanged -= OnLengthChanged;
            _player.Playing -= OnPlaying;
            _player.Paused -= OnPaused;
            _player.Stopped -= OnStopped;
            _player.VolumeChanged -= OnVolumeChanged;
            _player.Stop();

            lock (_closeLock)
            {
                _closed = true;

                _player.Dispose();
                _library.Dispose();
            }
        }

        #region Events

        public event TypedEventHandler<AsyncMediaPlayer, MediaPlayerVoutEventArgs> Vout;
        public event TypedEventHandler<AsyncMediaPlayer, MediaPlayerESSelectedEventArgs> ESSelected;
        public event TypedEventHandler<AsyncMediaPlayer, EventArgs> EndReached;
        public event TypedEventHandler<AsyncMediaPlayer, MediaPlayerBufferingEventArgs> Buffering;
        public event TypedEventHandler<AsyncMediaPlayer, MediaPlayerTimeChangedEventArgs> TimeChanged;
        public event TypedEventHandler<AsyncMediaPlayer, MediaPlayerLengthChangedEventArgs> LengthChanged;
        public event TypedEventHandler<AsyncMediaPlayer, EventArgs> Playing;
        public event TypedEventHandler<AsyncMediaPlayer, EventArgs> Paused;
        public event TypedEventHandler<AsyncMediaPlayer, EventArgs> Stopped;
        public event TypedEventHandler<AsyncMediaPlayer, MediaPlayerVolumeChangedEventArgs> VolumeChanged;

        private void OnVout(object sender, MediaPlayerVoutEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => Vout?.Invoke(this, e));
        }

        private void OnESSelected(object sender, MediaPlayerESSelectedEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => ESSelected?.Invoke(this, e));
        }

        private void OnEndReached(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => EndReached?.Invoke(this, e));
        }

        private void OnBuffering(object sender, MediaPlayerBufferingEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => Buffering?.Invoke(this, e));
        }

        private void OnTimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => TimeChanged?.Invoke(this, e));
        }

        private void OnLengthChanged(object sender, MediaPlayerLengthChangedEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => LengthChanged?.Invoke(this, e));
        }

        private void OnPlaying(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => Playing?.Invoke(this, e));
        }

        private void OnPaused(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => Paused?.Invoke(this, e));
        }

        private void OnStopped(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => Stopped?.Invoke(this, e));
        }

        private void OnVolumeChanged(object sender, MediaPlayerVolumeChangedEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => VolumeChanged?.Invoke(this, e));
        }

        #endregion

        #region Logs

        private static readonly Regex _videoLooking = new("using (.*?) module \"(.*?)\" from (.*?)$", RegexOptions.Compiled);
        private static readonly object _syncObject = new();

        private void _library_Log(object sender, LogEventArgs e)
        {
            Debug.WriteLine(e.FormattedLog);

            lock (_syncObject)
            {
                var match = _videoLooking.Match(e.FormattedLog);
                if (match.Success)
                {
                    System.IO.File.AppendAllText(ApplicationData.Current.LocalFolder.Path + "\\vlc.txt", string.Format("{2}\n", match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
                }
            }
        }

        #endregion

        private bool _workStarted;
        private Thread _workThread;

        private readonly WorkQueue _workQueue = new();
        private readonly object _workLock = new();

        private long _workVersion;

        private T Read<T>(Func<T> value)
        {
            lock (_closeLock)
            {
                if (_closed)
                {
                    return default;
                }

                return value();
            }
        }

        private void Write(Action action, bool versioned = false)
        {
            var version = versioned
                ? Interlocked.Increment(ref _workVersion)
                : 0;

            _workQueue.Push(new WorkItem(action, version));

            lock (_workLock)
            {
                if (_workStarted is false)
                {
                    if (_workThread?.IsAlive is false)
                    {
                        _workThread.Join();
                    }

                    _workStarted = true;
                    _workThread = new Thread(Work);
                    _workThread.Start();
                }
            }
        }

        private void Work()
        {
            while (_workStarted)
            {
                var work = _workQueue.WaitAndPop();
                if (work == null || _closed)
                {
                    _workStarted = false;
                    return;
                }

                try
                {
                    if (work.Version == 0 || work.Version == Interlocked.Read(ref _workVersion))
                    {
                        work.Action();
                    }
                }
                catch
                {
                    // Shit happens...
                }

                if (_closed)
                {
                    _workStarted = false;
                    return;
                }
            }
        }

        record WorkItem(Action Action, long Version);

        class WorkQueue
        {
            private readonly object _workAvailable = new();
            private readonly Queue<WorkItem> _work = new();

            public void Push(WorkItem item)
            {
                lock (_workAvailable)
                {
                    var was_empty = _work.Count == 0;

                    _work.Enqueue(item);

                    if (was_empty)
                    {
                        Monitor.Pulse(_workAvailable);
                    }
                }
            }

            public WorkItem WaitAndPop()
            {
                lock (_workAvailable)
                {
                    while (_work.Count == 0)
                    {
                        var timeout = Monitor.Wait(_workAvailable, 3000);
                        if (timeout is false)
                        {
                            return null;
                        }
                    }

                    return _work.Dequeue();
                }
            }

            public void Clear()
            {
                lock (_workAvailable)
                {
                    _work.Clear();
                }
            }
        }
    }
}
