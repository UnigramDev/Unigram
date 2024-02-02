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

        private bool _closed;

        public AsyncMediaPlayer(string[] options)
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // Generating plugins cache requires a breakpoint in bank.c#662
            _library = new LibVLC(options); //"--quiet", "--reset-plugins-cache");
            //_library.Log += _library_Log;

            _player = new MediaPlayer(_library);
            _player.ESSelected += OnESSelected;
            _player.Vout += OnVout;
            _player.Buffering += OnBuffering;
            _player.EndReached += OnEndReached;
            //_player.Stopped += OnStopped;

            //_player.FileCaching = 1;
        }

        public void Play(RemoteFileStream stream)
        {
            Enqueue(() => PlayImpl(stream), true);
        }

        private void PlayImpl(RemoteFileStream stream)
        {
            _player.Play(new Media(_library, stream));
        }

        public void Stop()
        {
            Enqueue(StopImpl);
        }

        private void StopImpl()
        {
            _player.Stop();
        }

        public void Pause(bool pause)
        {
            Enqueue(() => PauseImpl(pause));
        }

        private void PauseImpl(bool pause)
        {
            _player.SetPause(pause);
        }

        public void Mute(bool mute)
        {
            Enqueue(() => MuteImpl(mute));
        }

        private void MuteImpl(bool mute)
        {
            _player.Mute = mute;
        }

        public void Scale(float scale)
        {
            Enqueue(() => ScaleImpl(scale));
        }

        private void ScaleImpl(float scale)
        {
            _player.Scale = scale;
        }

        public void Close()
        {
            Enqueue(CloseImpl);
        }

        private void CloseImpl()
        {
            _closed = true;

            _player.ESSelected -= OnESSelected;
            _player.Vout -= OnVout;
            _player.Buffering -= OnBuffering;
            _player.EndReached -= OnEndReached;
            _player.Stop();

            _player.Dispose();
            _library.Dispose();
        }

        #region Events

        public event TypedEventHandler<AsyncMediaPlayer, MediaPlayerVoutEventArgs> Vout;
        public event TypedEventHandler<AsyncMediaPlayer, MediaPlayerESSelectedEventArgs> ESSelected;
        public event TypedEventHandler<AsyncMediaPlayer, EventArgs> EndReached;
        public event TypedEventHandler<AsyncMediaPlayer, MediaPlayerBufferingEventArgs> Buffering;

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

        private void Enqueue(Action action, bool versioned = false)
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
