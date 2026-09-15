//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using WalletEngine;
using EngineHttpMethod = WalletEngine.HttpMethod;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Carries the engine's provider requests over TDLib.
    /// </summary>
    /// <remarks>
    /// The statusless transport, not the HTTP one: TDLib returns the provider's JSON body and says
    /// nothing about status, headers or final URL, and it reaches Toncenter its own way. That is
    /// exactly the claim this trait makes, and it is why the engine's HTTP host - which has to
    /// report an observed URL for the redirect guard to compare - could not be used here.
    ///
    /// Nothing in the wallet opens a socket of its own as a result: the wallet's traffic goes where
    /// the rest of the app's traffic goes, including through whatever proxy the account is using.
    /// </remarks>
    internal sealed class WalletStatuslessHost : IWalletStatuslessHost
    {
        // How many cancellations for requests that never arrived we are willing to remember. The
        // engine cancels by id and a cancellation may legitimately precede its request, so these
        // cannot simply be dropped - but they also must not accumulate for the life of the app.
        private const int UnclaimedMemory = 64;

        private readonly IClientService _clientService;

        private readonly ConcurrentDictionary<ulong, CancellationTokenSource> _pending = new ConcurrentDictionary<ulong, CancellationTokenSource>();
        private readonly Queue<ulong> _unclaimed = new Queue<ulong>();

        public WalletStatuslessHost(IClientService clientService)
        {
            _clientService = clientService;
        }

        public async Task<byte[]> ExecuteStatusless(HttpRequest request)
        {
            var source = _pending.GetOrAdd(request.Id.Value, _ => new CancellationTokenSource());

            try
            {
                if (source.IsCancellationRequested)
                {
                    throw Failed(StatuslessHostErrorKind.Cancelled, "cancelled before dispatch");
                }

                var response = await _clientService.SendAsync(new SendTonCenterApiRequest(Endpoint(request), Type(request)));

                // TDLib cannot be told to abandon a request in flight, so a cancellation that
                // arrives while one is running is honoured by discarding the answer rather than by
                // stopping the work. The engine only requires that the call end in Cancelled.
                if (source.IsCancellationRequested)
                {
                    throw Failed(StatuslessHostErrorKind.Cancelled, "cancelled while in flight");
                }

                if (response is Text text)
                {
                    return Encoding.UTF8.GetBytes(text.TextValue);
                }

                throw Failed(Classify(response as Error), Describe(response as Error));
            }
            catch (StatuslessHostException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw Failed(StatuslessHostErrorKind.Other, ex.GetType().Name);
            }
            finally
            {
                if (_pending.TryRemove(request.Id.Value, out var removed))
                {
                    removed.Dispose();
                }
            }
        }

        public Task CancelStatusless(HttpRequestId requestId)
        {
            // Idempotent by construction, and safe before the request exists.
            var source = _pending.GetOrAdd(requestId.Value, _ => new CancellationTokenSource());
            var claimed = false;

            try
            {
                claimed = source.IsCancellationRequested;
                source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The request completed and disposed its source between the lookup and here.
            }

            if (!claimed)
            {
                Remember(requestId.Value);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Bounds the entries created by cancellations whose request never arrived, dropping the
        /// oldest once the window is full. A request older than the window would run uncancelled,
        /// which is a leak of one request rather than of memory.
        /// </summary>
        private void Remember(ulong id)
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

            if (_pending.TryRemove(evicted, out var source))
            {
                source.Dispose();
            }
        }

        /// <summary>
        /// The endpoint path TDLib expects, taken out of the absolute URL the engine built.
        /// </summary>
        /// <remarks>
        /// The engine still needs a base URL to build against and to keep mainnet and testnet
        /// apart; TDLib decides which host actually answers, so only the path survives the trip.
        /// </remarks>
        private static string Endpoint(HttpRequest request)
        {
            return Uri.TryCreate(request.Url, UriKind.Absolute, out var uri)
                ? uri.AbsolutePath
                : request.Url;
        }

        private static TonCenterApiRequestType Type(HttpRequest request)
        {
            if (request.Method == EngineHttpMethod.Post)
            {
                return new TonCenterApiRequestTypePost(Encoding.UTF8.GetString(request.Body));
            }

            // The query arrives separately from the path, without its leading '?'.
            var query = Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) && uri.Query.Length > 0
                ? uri.Query.Substring(1)
                : string.Empty;

            return new TonCenterApiRequestTypeGet(query);
        }

        private static StatuslessHostErrorKind Classify(Error error)
        {
            if (error == null)
            {
                return StatuslessHostErrorKind.Other;
            }

            // 429 and 5xx are worth retrying and the engine decides that from the kind, so they
            // must not all collapse into Other.
            return error.Code switch
            {
                420 or 429 => StatuslessHostErrorKind.Timeout,
                >= 500 => StatuslessHostErrorKind.ConnectionLost,
                _ => StatuslessHostErrorKind.Other
            };
        }

        // TDLib error messages name the request, never a credential, so they are safe to pass on.
        private static string Describe(Error error)
        {
            return error != null
                ? error.Code + ": " + error.Message
                : "no response";
        }

        private static StatuslessHostException Failed(StatuslessHostErrorKind kind, string diagnostic)
        {
            return new StatuslessHostException.Failed(kind, diagnostic);
        }
    }
}
