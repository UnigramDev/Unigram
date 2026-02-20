//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Telegram.Native.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;

namespace Telegram.Common
{
    public record VideoPresentation(IClientService ClientService, File File, int Duration);

    public partial class VideoPreloader
    {
        [ThreadStatic]
        private static VideoPreloader _current;
        public static VideoPreloader Current => _current ??= new();

        private readonly DispatcherQueue _dispatcherQueue;
        private readonly WindowContext _window;

        private bool _closed;

        private VideoPreloader()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _window = WindowContext.Current;

            Debug.Assert(_dispatcherQueue != null);
        }

        private bool _workStarted;
        private Thread _workThread;

        private readonly WorkQueue _workQueue = new();
        private readonly object _workLock = new();

        private readonly ConcurrentDictionary<long, VideoPresentation> _presenters = new();
        private readonly object _presentersLock = new();

        public void Load(IClientService clientService, File file, int duration)
        {
            if (file.Local.DownloadedSize > 0)
            {
                return;
            }

            var token = (clientService.SessionId << 16) | file.Id;
            if (_presenters.ContainsKey(token))
            {
                return;
            }

            var presentation = new VideoPresentation(clientService, file, duration);

            _presenters[token] = presentation;
            _workQueue.Push(new WorkItem(token, presentation));

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
                if (work == null)
                {
                    _workStarted = false;
                    return;
                }

                try
                {
                    LoadCachedVideo(work);
                }
                catch
                {
                    // Shit happens...
                    _presenters.TryRemove(work.Token, out _);
                }
            }
        }

        private void LoadCachedVideo(WorkItem work)
        {
            var options = new AsyncMediaPlayerOptions
            {
                Mode = AsyncMediaPlayerMode.Video
            };

            var player = new AsyncMediaPlayer(options);
            void handler(AsyncMediaPlayer sender, AsyncMediaPlayerBufferingEventArgs e)
            {
                if (e.Cache == 100)
                {
                    player.Buffering -= handler;
                    player.Close();

                    _presenters.TryRemove(work.Token, out _);
                }
            }

            player.Buffering += handler;
            player.Play(new RemoteFileSource(work.Presentation.ClientService, work.Presentation.File, work.Presentation.Duration));
        }

        record WorkItem(long Token, VideoPresentation Presentation);

        class WorkQueue
        {
            private readonly object _workAvailable = new();
            private readonly Stack<WorkItem> _work = new();

            public void Push(WorkItem item)
            {
                lock (_workAvailable)
                {
                    var was_empty = _work.Count == 0;

                    _work.Push(item);

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

                    return _work.Pop();
                }
            }
        }
    }
}
