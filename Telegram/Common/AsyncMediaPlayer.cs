//
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
using Telegram.Navigation;
using Telegram.Streams;
using Windows.Foundation;
using Windows.Storage;

namespace Telegram.Common
{
    public class AsyncMediaPlayer
    {
        private readonly IDispatcherContext _dispatcherQueue;

        private readonly LibVLC _library;
        private readonly MediaPlayer _player;

        private Media _media;
        private MediaInput _input;

        private readonly object _closeLock = new();
        private bool _closed;

        public AsyncMediaPlayer(params string[] options)
        {
            _dispatcherQueue = WindowContext.Current.Dispatcher;
            
            // This should be not needed
            _dispatcherQueue ??= WindowContext.Main.Dispatcher;

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

            // Music
            //_player.TimeChanged += OnTimeChanged;
            //_player.LengthChanged += OnLengthChanged;
            _player.EncounteredError += OnEncounteredError;
            //_player.EndReached += OnEndReached;
        }

        public void Play(RemoteFileStream input)
        {
            Write(valid => PlayImpl(input, valid));
        }

        private void PlayImpl(MediaInput input, bool play)
        {
            if (play)
            {
                var media = new Media(_library, input);

                _player.Play(media);

                // We need to retain both Media and MediaInput due to the bad (IMHO) design of libvlc API.
                // When creating a Media from a MediaInput, some callbacks are registered to access the stream.
                // The problem is that the library creates a GC handle in MediaInput that is then used by Media
                // to register the aforementioned callbacks. What happens, in my understanding, is that there are
                // some good chances that MediaInput is disposed before Media, and due to that the GC handle is deleted
                // and this causes an access violation in libvlccore when trying to raise the callbacks for the media.
                _media?.Dispose();
                _media = media;

                _input?.Dispose();
                _input = input;
            }
            else
            {
                input.Dispose();
            }
        }

        public void Play()
        {
            Write(() => _player.Play());
        }

        public void Stop()
        {
            Write(() => _player.Stop());
        }

        public void Pause(bool pause = true)
        {
            Write(() => _player.SetPause(pause));
        }

        public VLCState State => Read(() => _player.State);

        public bool IsPlaying => Read(() => _player.IsPlaying);

        public bool CanPause => Read(() => _player.CanPause);

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
            _player.EncounteredError -= OnEncounteredError;
            _player.Stop();
            _player.Media = null;

            if (_media != null)
            {
                _media?.Dispose();
                _media = null;
            }

            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }

            lock (_closeLock)
            {
                _closed = true;

                //_player.Dispose();
                //_library.Dispose();
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
        public event TypedEventHandler<AsyncMediaPlayer, EventArgs> EncounteredError;

        private void OnVout(object sender, MediaPlayerVoutEventArgs e)
        {
            _dispatcherQueue.Dispatch(() => Vout?.Invoke(this, e));
        }

        private void OnESSelected(object sender, MediaPlayerESSelectedEventArgs e)
        {
            _dispatcherQueue.Dispatch(() => ESSelected?.Invoke(this, e));
        }

        private void OnEndReached(object sender, EventArgs e)
        {
            _dispatcherQueue.Dispatch(() => EndReached?.Invoke(this, e));
        }

        private void OnBuffering(object sender, MediaPlayerBufferingEventArgs e)
        {
            _dispatcherQueue.Dispatch(() => Buffering?.Invoke(this, e));
        }

        private void OnTimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        {
            _dispatcherQueue.Dispatch(() => TimeChanged?.Invoke(this, e));
        }

        private void OnLengthChanged(object sender, MediaPlayerLengthChangedEventArgs e)
        {
            _dispatcherQueue.Dispatch(() => LengthChanged?.Invoke(this, e));
        }

        private void OnPlaying(object sender, EventArgs e)
        {
            _dispatcherQueue.Dispatch(() => Playing?.Invoke(this, e));
        }

        private void OnPaused(object sender, EventArgs e)
        {
            _dispatcherQueue.Dispatch(() => Paused?.Invoke(this, e));
        }

        private void OnStopped(object sender, EventArgs e)
        {
            _dispatcherQueue.Dispatch(() => Stopped?.Invoke(this, e));
        }

        private void OnVolumeChanged(object sender, MediaPlayerVolumeChangedEventArgs e)
        {
            _dispatcherQueue.Dispatch(() => VolumeChanged?.Invoke(this, e));
        }

        private void OnEncounteredError(object sender, EventArgs e)
        {
            _dispatcherQueue.Dispatch(() => EncounteredError?.Invoke(this, e));
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

        private void Write(Action action)
        {
            Write(new WorkItem(action));
        }

        private void Write(Action<bool> action)
        {
            Write(new VersionedWorkItem(action, Interlocked.Increment(ref _workVersion)));
        }

        private void Write(object workItem)
        {
            _workQueue.Push(workItem);

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
                    if (work is VersionedWorkItem versioned)
                    {
                        versioned.Action(versioned.Version == Interlocked.Read(ref _workVersion));
                    }
                    else if (work is WorkItem item)
                    {
                        item.Action();
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

        record WorkItem(Action Action);
        record VersionedWorkItem(Action<bool> Action, long Version);

        class WorkQueue
        {
            private readonly object _workAvailable = new();
            private readonly Queue<object> _work = new();

            public void Push(object item)
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

            public object WaitAndPop()
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
