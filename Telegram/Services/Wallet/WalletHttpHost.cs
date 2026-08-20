//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletEngine;
using EngineHttpMethod = WalletEngine.HttpMethod;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Executes the engine's HTTP requests.
    /// </summary>
    /// <remarks>
    /// The engine builds absolute provider URLs and owns retry and backoff; this type only moves
    /// bytes, and is deliberately the only place in the wallet that talks to the network.
    ///
    /// Requests go out directly rather than through TDLib. <c>sendTonCenterApiRequest</c> existed
    /// in the 2026-07-31 TDLib layer and is gone from the current one, with nothing replacing it.
    /// That is the better outcome regardless: TDLib rewrote scheme and host, which would have made
    /// the observed URL differ from the requested one and defeated the engine's redirect guard.
    /// </remarks>
    internal sealed class WalletHttpHost : IWalletHttpHost
    {
        // Redirects are refused rather than followed: the engine compares the URL it asked for
        // against FinalUrl and treats any difference as a policy violation, because a redirected
        // provider response can no longer be trusted to come from the configured endpoint.
        private static readonly HttpClient _client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        });

        // Provider responses are JSON of bounded size; anything larger is a misconfigured or
        // hostile endpoint, and the engine has a dedicated error kind for it.
        private const int MaxResponseBytes = 8 * 1024 * 1024;

        // How many cancellations for requests that never arrived we are willing to remember. The
        // engine cancels by id and a cancellation may legitimately precede its request, so these
        // cannot simply be dropped - but they also must not accumulate for the life of the app.
        private const int UnclaimedMemory = 64;

        private sealed class Pending
        {
            public readonly CancellationTokenSource Source = new CancellationTokenSource();
            public bool Claimed;
        }

        private readonly ConcurrentDictionary<ulong, Pending> _pending = new ConcurrentDictionary<ulong, Pending>();
        private readonly Queue<ulong> _unclaimed = new Queue<ulong>();

        public async Task<HttpResponse> ExecuteHttp(HttpRequest request)
        {
            // GetOrAdd, not Add: a cancellation for this id may already have created the entry and
            // cancelled it, in which case the token is born cancelled and the request never runs.
            var pending = _pending.GetOrAdd(request.Id.Value, _ => new Pending());
            lock (pending)
            {
                pending.Claimed = true;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(pending.Source.Token);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(request.TimeoutMs));

            try
            {
                return await SendAsync(request, timeout.Token, pending.Source.Token);
            }
            finally
            {
                if (_pending.TryRemove(request.Id.Value, out _))
                {
                    pending.Source.Dispose();
                }
            }
        }

        public Task CancelHttp(HttpRequestId requestId)
        {
            // Idempotent by construction, and safe before the request exists.
            var pending = _pending.GetOrAdd(requestId.Value, _ => new Pending());

            bool claimed;
            lock (pending)
            {
                claimed = pending.Claimed;
            }

            try
            {
                pending.Source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The request completed and disposed its source between the lookup and here.
            }

            if (!claimed)
            {
                Forget(requestId.Value);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Bounds the entries created by cancellations whose request never arrived, dropping the
        /// oldest once the window is full. A request older than the window would run uncancelled,
        /// which is a leak of one request rather than of memory.
        /// </summary>
        private void Forget(ulong id)
        {
            ulong evicted;

            lock (_unclaimed)
            {
                _unclaimed.Enqueue(id);
                if (_unclaimed.Count <= UnclaimedMemory)
                {
                    return;
                }

                evicted = _unclaimed.Dequeue();
            }

            if (_pending.TryRemove(evicted, out var pending))
            {
                pending.Source.Dispose();
            }
        }

        private async Task<HttpResponse> SendAsync(HttpRequest request, CancellationToken token, CancellationToken cancelled)
        {
            using var message = new HttpRequestMessage(
                request.Method == EngineHttpMethod.Post ? System.Net.Http.HttpMethod.Post : System.Net.Http.HttpMethod.Get,
                request.Url);

            if (request.Method == EngineHttpMethod.Post)
            {
                message.Content = new ByteArrayContent(request.Body ?? Array.Empty<byte>());
            }

            foreach (var header in request.Headers)
            {
                // Content headers are rejected by the request collection and have to go on the
                // body instead, which is why this cannot be a single TryAddWithoutValidation.
                if (!message.Headers.TryAddWithoutValidation(header.Name, header.Value))
                {
                    message.Content?.Headers.TryAddWithoutValidation(header.Name, header.Value);
                }
            }

            try
            {
                using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);

                var headers = new List<HttpHeader>();
                foreach (var header in response.Headers)
                {
                    headers.Add(new HttpHeader(header.Key, string.Join(", ", header.Value)));
                }
                foreach (var header in response.Content.Headers)
                {
                    headers.Add(new HttpHeader(header.Key, string.Join(", ", header.Value)));
                }

                var body = await ReadBoundedAsync(response, token);

                // Redirects are never followed, so the URL we asked for is the one we observed.
                return new HttpResponse((ushort)response.StatusCode, headers.ToArray(), body, request.Url);
            }
            catch (OperationCanceledException) when (cancelled.IsCancellationRequested)
            {
                throw Failed(HttpHostErrorKind.Cancelled, "cancelled by the engine");
            }
            catch (OperationCanceledException)
            {
                // The linked source fired without the engine cancelling, so it was the deadline.
                throw Failed(HttpHostErrorKind.Timeout, "timed out after " + request.TimeoutMs + "ms");
            }
            catch (HttpRequestException ex)
            {
                throw Failed(Classify(ex), Describe(ex));
            }
            catch (IOException ex)
            {
                throw Failed(HttpHostErrorKind.ConnectionLost, Describe(ex));
            }
        }

        private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, CancellationToken token)
        {
            // Content-Length is a hint and may be absent or wrong, so the cap is enforced while
            // reading rather than trusted from the header.
            if (response.Content.Headers.ContentLength > MaxResponseBytes)
            {
                throw Failed(HttpHostErrorKind.ResponseTooLarge, "declared length exceeds the response limit");
            }

            using var stream = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();

            var chunk = new byte[16 * 1024];
            int read;

            while ((read = await stream.ReadAsync(chunk, 0, chunk.Length, token)) > 0)
            {
                if (buffer.Length + read > MaxResponseBytes)
                {
                    throw Failed(HttpHostErrorKind.ResponseTooLarge, "response exceeded the response limit");
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.ToArray();
        }

        private static HttpHostErrorKind Classify(HttpRequestException exception)
        {
            // WinHttp surfaces most transport failures as a nested exception, and the categories
            // the engine wants back are coarse enough that the inner type is the useful signal.
            for (Exception ex = exception; ex != null; ex = ex.InnerException)
            {
                switch (ex)
                {
                    case System.Net.Sockets.SocketException socket:
                        return socket.SocketErrorCode switch
                        {
                            System.Net.Sockets.SocketError.HostNotFound => HttpHostErrorKind.Dns,
                            System.Net.Sockets.SocketError.NetworkDown => HttpHostErrorKind.Offline,
                            System.Net.Sockets.SocketError.NetworkUnreachable => HttpHostErrorKind.Offline,
                            System.Net.Sockets.SocketError.ConnectionReset => HttpHostErrorKind.ConnectionLost,
                            System.Net.Sockets.SocketError.ConnectionAborted => HttpHostErrorKind.ConnectionLost,
                            _ => HttpHostErrorKind.Other
                        };
                    case System.Security.Authentication.AuthenticationException:
                        return HttpHostErrorKind.Tls;
                }
            }

            return HttpHostErrorKind.Other;
        }

        private static HttpHostException Failed(HttpHostErrorKind kind, string diagnostic)
        {
            return new HttpHostException.Failed(kind, diagnostic);
        }

        // Provider URLs can carry an API key in the query, and exception messages quote the URL,
        // so only the exception type and its innermost message are reported.
        private static string Describe(Exception exception)
        {
            var inner = exception;
            while (inner.InnerException != null)
            {
                inner = inner.InnerException;
            }

            return inner.GetType().Name;
        }
    }
}
