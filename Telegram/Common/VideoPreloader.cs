//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Native.Media;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;

namespace Telegram.Common
{
    /// <summary>
    /// Reads the start of a video the chat shows but does not download, the way playing it reads
    /// it, so that playing it starts at once.
    /// </summary>
    public static class VideoPreloader
    {
        // The player starts at the first picture; this much more keeps it from waiting while the
        // rest downloads.
        private const double Seconds = 3;

        // Media read past the open, whatever the seconds come to.
        private const long Limit = 2 * 1024 * 1024;

        // A video the chat only scrolls past is not worth starting for.
        private const int SettleDelay = 300;

        // Each waits on the network on a thread of its own.
        private const int Concurrency = 2;

        private static readonly object _lock = new();

        // Newest last, which is the one started first.
        private static readonly List<Request> _pending = new();

        // Pending or running, by session and file.
        private static readonly Dictionary<long, Request> _keys = new();

        // Read to the end, by session and file.
        private static readonly HashSet<long> _completed = new();

        private static int _running;

        // Held by a caller whose video has nothing to read.
        private static readonly Request _nothing = new(null, 0);

        public sealed class Request
        {
            internal Request(RemoteRendition rendition, long key)
            {
                Rendition = rendition;
                Key = key;
            }

            internal RemoteRendition Rendition { get; }

            internal long Key { get; }

            // The rest is only touched under the lock.
            internal int Owners;
            internal bool IsSettled;
            internal bool IsCanceled;
            internal MediaPreloader Preloader;
        }

        /// <summary>
        /// Starts reading the video in the quality the gallery would play, unless the caller
        /// holds a request already: as with <see cref="UpdateManager"/>, it cancels before asking
        /// for another. Callers asking for the same file share one read.
        /// </summary>
        /// <param name="content">The message's video, which says what qualities there are. Null
        /// for a video played from its own file only.</param>
        public static void Load(IClientService clientService, Video video, MessageVideo content, ref Request request)
        {
            if (request != null)
            {
                return;
            }

            request = _nothing;

            var renditions = content != null
                ? RemoteRendition.CreateRenditions(clientService, content, FileReadRank.Preload)
                : null;

            var rendition = renditions != null
                ? renditions[RemoteRendition.PickRendition(renditions)]
                : new RemoteRendition(clientService, video.VideoValue, null, video.Width, video.Height, video.Duration, FileReadRank.Preload);

            if (rendition.File.Local.IsDownloadingCompleted)
            {
                return;
            }

            var key = ((long)clientService.SessionId << 32) | (uint)rendition.File.Id;
            Request shared;

            // Never waited for: see Cancel. Contended, the video goes without, holding _nothing
            // until its container is recycled.
            if (!Monitor.TryEnter(_lock))
            {
                return;
            }

            try
            {
                if (_completed.Contains(key))
                {
                    return;
                }

                if (_keys.TryGetValue(key, out shared))
                {
                    shared.Owners++;
                    request = shared;
                    return;
                }

                shared = new Request(rendition, key)
                {
                    Owners = 1
                };

                _keys[key] = shared;
                _pending.Add(shared);

                request = shared;
            }
            finally
            {
                Monitor.Exit(_lock);
            }

            Settle(shared);
        }

        /// <remarks>
        /// Called from recycling, which can run inside XAML's handling of a collection reset. A
        /// contended Monitor.Enter on the UI thread waits in CoWaitForMultipleHandles, which
        /// dispatches window messages, XAML's render tick among them, and the layout it runs
        /// re-enters the list halfway through the reset. So the UI thread only tries the lock, and
        /// what it cannot do at once runs on the thread pool. Owners only goes up and down, so the
        /// order it lands in against a Load does not matter.
        /// </remarks>
        public static void Cancel(ref Request request)
        {
            var canceled = request;
            request = null;

            if (canceled == null || canceled == _nothing)
            {
                return;
            }

            if (Monitor.TryEnter(_lock))
            {
                MediaPreloader preloader;

                try
                {
                    preloader = Release(canceled);
                }
                finally
                {
                    Monitor.Exit(_lock);
                }

                // Takes a native mutex and closes the managed source, which locks in turn.
                if (preloader != null)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(static state => ((MediaPreloader)state).Cancel(), preloader);
                }
            }
            else
            {
                ThreadPool.UnsafeQueueUserWorkItem(static state =>
                {
                    MediaPreloader preloader;

                    lock (_lock)
                    {
                        preloader = Release((Request)state);
                    }

                    preloader?.Cancel();
                }, canceled);
            }
        }

        // Under _lock. The preloader to cancel, once the last owner has gone.
        private static MediaPreloader Release(Request request)
        {
            if (--request.Owners != 0)
            {
                return null;
            }

            request.IsCanceled = true;
            _pending.Remove(request);

            if (_keys.TryGetValue(request.Key, out var current) && current == request)
            {
                _keys.Remove(request.Key);
            }

            return request.Preloader;
        }

        private static async void Settle(Request request)
        {
            await Task.Delay(SettleDelay).ConfigureAwait(false);

            lock (_lock)
            {
                request.IsSettled = true;
            }

            Next();
        }

        private static void Next()
        {
            while (true)
            {
                Request request = null;

                lock (_lock)
                {
                    if (_running < Concurrency)
                    {
                        var index = _pending.Count - 1;

                        while (index >= 0 && !_pending[index].IsSettled)
                        {
                            index--;
                        }

                        if (index >= 0)
                        {
                            request = _pending[index];
                            _pending.RemoveAt(index);
                            _running++;
                        }
                    }
                }

                if (request == null)
                {
                    return;
                }

                // Outside the lock: LongRunning creates a thread, the longest the lock was ever held.
                Task.Factory.StartNew(Run, request, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        private static void Run(object state)
        {
            if (state is not Request request)
            {
                return;
            }

            var started = Logger.TickCount;
            var result = default(MediaPreloadResult);

            try
            {
                var preloader = new MediaPreloader(request.Rendition);
                var canceled = false;

                lock (_lock)
                {
                    canceled = request.IsCanceled;
                    request.Preloader = canceled ? null : preloader;
                }

                if (!canceled)
                {
                    result = preloader.Run(Seconds, Limit);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }

            Logger.Info($"File {request.Rendition.File.Id} {(result.Completed ? "preloaded" : "not preloaded")}: {result.MediaSeconds:F1}s, {result.OpenBytes} bytes to open and {result.MediaBytes} past that, in {Logger.TickCount - started}ms");

            lock (_lock)
            {
                request.Preloader = null;

                _running--;

                // Unless it was canceled, and the file asked for again meanwhile.
                if (_keys.TryGetValue(request.Key, out var current) && current == request)
                {
                    _keys.Remove(request.Key);
                }

                if (result.Completed)
                {
                    _completed.Add(request.Key);
                }
            }

            Next();
        }
    }
}
