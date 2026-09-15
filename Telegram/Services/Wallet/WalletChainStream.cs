//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Watches the chain for the account's wallet, and says when something happened.
    /// </summary>
    /// <remarks>
    /// The socket is the app's, not the engine's: wallet-engine states plainly that it has no
    /// streaming API and that the host owns the stream, its reconnect policy, and what to do with
    /// an event - which here is a refresh, since nothing in the payload is parsed.
    ///
    /// This is the one piece of wallet traffic that does not go through TDLib, so it does not
    /// follow the account's proxy either. Everything the engine asks for is carried over
    /// <c>sendTonCenterApiRequest</c>; this opens a socket of its own.
    ///
    /// <see cref="ClientWebSocket"/> rather than <c>MessageWebSocket</c>: the WinRT one goes
    /// through WinINet, which answered a perfectly resolvable host with
    /// <c>ERROR_INTERNET_NAME_NOT_RESOLVED</c>. Nothing else in the app uses that stack - TDLib
    /// reaches its datacentres by address - so this is the only place it would have been found.
    /// </remarks>
    internal sealed class WalletChainStream
    {
        // Waiting after a connection fails, and the longest that wait grows to. A stream is a
        // convenience - the balance still arrives by update - so it retreats rather than insisting.
        private static readonly TimeSpan ReconnectFirstDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan ReconnectMaximumDelay = TimeSpan.FromSeconds(30);

        // How long before the URL expires to go and get another. The server issues one per
        // request, so reconnecting early costs a request and reconnecting late costs the gap.
        private static readonly TimeSpan ExpiryMargin = TimeSpan.FromSeconds(30);

        // Frames are small, and one that is not is one this does not need in full: only the
        // control replies are read, and those are two fields long.
        private const int FrameBuffer = 4096;

        // What the service asks to hear about. Transactions alone: everything else this shows -
        // the balance, the peer, the fee - comes from the account afterwards.
        //
        // Confirmed rather than the default finalized, which waits for a masterchain block, or
        // pending, which fires before the account could possibly have the transaction.

        private const string Ping = "{\"operation\":\"ping\",\"id\":\"1\"}";

        // The documented keepalive. A connection is an hour long in production and five minutes on
        // test, so without this the server is entitled to decide nobody is there.
        private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

        // How many frames of each connection are written to the log. The protocol is not one we
        // have documentation for, and the first few frames are how it becomes known; after that
        // they are noise.
        private const int LoggedFrames = 3;

        private readonly IClientService _clientService;
        private readonly Func<string> _address;
        private readonly Action _changed;

        private CancellationTokenSource _cancellation;
        private Task _loop;

        private int _watchers;

        /// <param name="address">
        /// Read at every connection rather than held: a wallet can be replaced between two of them.
        /// </param>
        public WalletChainStream(IClientService clientService, Func<string> address, Action changed)
        {
            _clientService = clientService;
            _address = address;
            _changed = changed;
        }

        /// <summary>
        /// Starts watching, or notes another watcher. The stream runs while anything is looking at
        /// the wallet and stops when nothing is: it is a socket held open, and a wallet nobody has
        /// on screen has nothing to tell.
        /// </summary>
        public void Start()
        {
            if (Interlocked.Increment(ref _watchers) > 1)
            {
                return;
            }

            _cancellation = new CancellationTokenSource();
            _loop = RunAsync(_cancellation.Token);
        }

        public void Stop()
        {
            if (Interlocked.Decrement(ref _watchers) > 0)
            {
                return;
            }

            _cancellation?.Cancel();
            _cancellation = null;
            _loop = null;
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            var delay = ReconnectFirstDelay;

            while (!cancellationToken.IsCancellationRequested)
            {
                var connected = false;

                try
                {
                    connected = await ConnectAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Error("wallet stream failed: " + ex.Message);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (connected)
                {
                    // It ran and ended, which is what an expiring URL does. The next one is asked
                    // for immediately; only a failure backs off.
                    delay = ReconnectFirstDelay;
                    continue;
                }

                try
                {
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                delay = delay + delay < ReconnectMaximumDelay
                    ? delay + delay
                    : ReconnectMaximumDelay;
            }
        }

        /// <summary>
        /// Holds one connection for as long as its URL is good. Returns whether it was established
        /// at all, which is what tells a lost connection from a refused one.
        /// </summary>
        private async Task<bool> ConnectAsync(CancellationToken cancellationToken)
        {
            var address = _address();
            if (string.IsNullOrEmpty(address) || address.IndexOf('"') >= 0)
            {
                // No wallet yet, or an address that cannot be put in a JSON string. Neither is
                // worth a socket.
                return false;
            }

            var response = await _clientService.SendAsync(new GetTonCenterStreamingApiUrl());
            if (response is not TonCenterStreamingApiUrl streaming || !Uri.TryCreate(streaming.Url, UriKind.Absolute, out var uri))
            {
                Logger.Error("wallet stream url refused: " + (response as Error)?.Message);
                return false;
            }

            // The URL carries its own lifetime, so the connection is given up before the server has
            // to do it: a reconnect in hand is cheaper than a gap.
            var lifetime = TimeSpan.FromSeconds(Math.Max(streaming.ExpiresIn, 0)) - ExpiryMargin;
            if (lifetime < ReconnectFirstDelay)
            {
                lifetime = ReconnectFirstDelay;
            }

            using var expiry = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            expiry.CancelAfter(lifetime);

            using var socket = new ClientWebSocket();

            await socket.ConnectAsync(uri, expiry.Token);
            Logger.Info("wallet stream open");

            // One at a time: two sends on one socket at once is not allowed, and the keepalive
            // below runs beside the subscription and the reading.
            using var sending = new SemaphoreSlim(1, 1);

            await SendAsync(socket, sending, Subscribe(address), expiry.Token);

            var ping = PingAsync(socket, sending, expiry.Token);

            try
            {
                await ReceiveAsync(socket, expiry.Token);
            }
            finally
            {
                // Ends the keepalive with the connection it belongs to, and waits for it, so the
                // socket is not disposed from under a send.
                expiry.Cancel();

                try
                {
                    await ping;
                }
                catch (OperationCanceledException)
                {
                }
            }

            return true;
        }

        /// <summary>
        /// What the stream asks to hear about: transactions for this wallet alone. Everything else
        /// shown - the balance, the peer, the fee - comes from the account afterwards.
        /// </summary>
        /// <remarks>
        /// Confirmed rather than the default finalized, which waits for a masterchain block, or
        /// pending, which fires before the account could possibly have the transaction.
        ///
        /// Concatenated rather than formatted: the payload is made of braces, and every one of them
        /// would have to be doubled to survive string.Format.
        /// </remarks>
        private static string Subscribe(string address)
        {
            return "{\"operation\":\"subscribe\",\"types\":[\"transactions\"],\"addresses\":[\""
                + address
                + "\"],\"min_finality\":\"confirmed\",\"id\":\"1\"}";
        }

        private static async Task SendAsync(ClientWebSocket socket, SemaphoreSlim sending, string payload, CancellationToken cancellationToken)
        {
            await sending.WaitAsync(cancellationToken);

            try
            {
                var bytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(payload));
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                sending.Release();
            }
        }

        private static async Task PingAsync(ClientWebSocket socket, SemaphoreSlim sending, CancellationToken cancellationToken)
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(PingInterval, cancellationToken);
                await SendAsync(socket, sending, Ping, cancellationToken);
            }
        }

        /// <summary>
        /// Whether a frame is something that happened, rather than the server answering something
        /// this asked. A reply carries a status - subscribed, pong - and an event does not.
        /// </summary>
        private static bool IsEvent(string text)
        {
            try
            {
                using var document = JsonDocument.Parse(text);

                return document.RootElement.ValueKind == JsonValueKind.Object
                    && !document.RootElement.TryGetProperty("status", out _);
            }
            catch
            {
                // Unreadable is still something that arrived, and the cost of being wrong is one
                // refresh.
                return true;
            }
        }

        /// <summary>
        /// Reads until the server closes or the URL runs out. What a frame says is not read past
        /// the first few: that something arrived is the whole message, and the account is then
        /// asked what actually changed.
        /// </summary>
        private async Task ReceiveAsync(ClientWebSocket socket, CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[FrameBuffer]);
            var logged = 0;

            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                WebSocketReceiveResult result;

                try
                {
                    result = await socket.ReceiveAsync(buffer, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Logger.Info(string.Format("wallet stream closed: {0} {1}", result.CloseStatus, result.CloseStatusDescription));
                    return;
                }

                if (result.Count == 0)
                {
                    continue;
                }

                var text = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);

                if (logged < LoggedFrames)
                {
                    logged++;
                    Logger.Info("wallet stream: " + (text.Length > 200 ? text.Substring(0, 200) : text));
                }

                // Only on the end of a message, so a frame split across reads asks once - and only
                // for an event, since the subscription's own answer and every pong come back on
                // this same socket and would otherwise be four refreshes a minute, forever.
                if (result.EndOfMessage && IsEvent(text))
                {
                    _changed();
                }
            }
        }
    }
}
