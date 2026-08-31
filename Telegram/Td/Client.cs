//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
#endif
using System.Runtime.InteropServices;
using System.Threading;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Td.Api;
#if TD_READER_PARSER
using System.Text.Json;
#endif

#nullable enable

namespace Telegram.Td
{
    public delegate void LogMessageCallback(int verbosityLevel, string message);

    public interface ClientResultHandler
    {
        void OnResult(Object result);

        // Files are the one place object identity pays - they arrive constantly during a download,
        // always for an id the app already holds - so whichever reader was generated hands them
        // back here to be deduped rather than parsing them into a new instance.
#if TD_READER_PARSER
        UpdateFile ParseUpdateFile(ref Utf8JsonReader reader);
        File ParseFile(ref Utf8JsonReader reader);
#endif
#if TD_POINTER_PARSER
        UpdateFile ParseUpdateFile(ref TdJsonReader reader);
        File ParseFile(ref TdJsonReader reader);
#endif
    }

    public partial class Client
    {
#if NET9_0_OR_GREATER
        // A function pointer, not a delegate: handing a managed delegate to native code is runtime
        // marshalling. The callback below is [UnmanagedCallersOnly] to match, which is why it has
        // to stay static and capture nothing.
        [LibraryImport("tdjson.dll")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static unsafe partial void td_set_log_message_callback(int max_verbosity_level,
            delegate* unmanaged[Cdecl]<int, IntPtr, void> callback);

        [LibraryImport("tdjson.dll")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static partial int td_create_client_id();

        [LibraryImport("tdjson.dll")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static unsafe partial void td_send(int client_id, long request_id, byte* request);

        [LibraryImport("tdjson.dll")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static unsafe partial byte* td_execute(byte* request);

        [LibraryImport("tdjson.dll")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static unsafe partial byte* td_receive(double timeout, out int client_id, out long request_id);
#else
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void TdLogMessageCallback(int verbosity_level, IntPtr message);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void td_set_log_message_callback(int max_verbosity_level, TdLogMessageCallback? callback);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int td_create_client_id();

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe void td_send(int client_id, long request_id, byte* request);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe byte* td_execute(byte* request);

        [SuppressUnmanagedCodeSecurity]
        [DllImport("tdjson.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern unsafe byte* td_receive(double timeout, out int client_id, out long request_id);
#endif

        private static long _currentRequestId = 0;
        private static readonly ReaderWriterDictionary<long, Action<Object>> _handlers = new();
        private static readonly ReaderWriterDictionary<int, ClientResultHandler> _updateHandlers = new();

        private readonly int _clientId;

        public Client(ClientResultHandler updateHandler)
        {
            _clientId = td_create_client_id();

            if (updateHandler != null)
            {
                _updateHandlers[_clientId] = updateHandler;
            }

            Send(new GetOption("version"));
        }

        public unsafe void Send(Function function, Action<Object>? handler = null)
        {
            var requestId = Interlocked.Increment(ref _currentRequestId);
            if (handler != null)
            {
                _handlers[requestId] = handler;
            }

            if (_writer == null)
            {
                _writer = new ArrayPoolBufferWriter();
            }
            else
            {
                _writer.Rent();
            }

            var request = ClientJson.ToJson(_writer, function);
            fixed (byte* bytes = request)
            {
                td_send(_clientId, requestId, bytes);
            }

            _writer.Reset();
        }

        /// <summary>
        /// Synchronously executes a TDLib request. Only a few marked accordingly requests can be executed synchronously.
        /// </summary>
        /// <param name="function">Object representing a query to the TDLib.</param>
        /// <returns>Returns request result.</returns>
        /// <exception cref="NullReferenceException">Thrown when query is null.</exception>
        public static unsafe Object Execute(Function function)
        {
            if (_writer == null)
            {
                _writer = new ArrayPoolBufferWriter();
            }
            else
            {
                _writer.Rent();
            }

            var request = ClientJson.ToJson(_writer, function);
            fixed (byte* source = request)
            {
                var ptr = td_execute(source);
                if (ptr == null || *ptr == 0)
                {
                    return new Error(400, "Can't deserialize");
                }

#if TD_POINTER_PARSER
                try
                {
                    // Parsed where TDLib put it. The buffer belongs to TDLib and stays valid until
                    // the next call on this thread, which is after this has returned.
                    return ClientJson.FromPtr(ptr, TdJsonReader.NulTerminated);
                }
                finally
                {
                    _writer.Reset();
                }
#else
                byte* end = ptr;
                while (*end != 0)
                {
                    end++;
                }

                int length = (int)(end - ptr);

                _writer.Resize(length);

                fixed (byte* dest = _writer.Bytes)
                {
                    Buffer.MemoryCopy(ptr, dest, _writer.Bytes.Length, length);
                }

                var span = new ReadOnlySpan<byte>(_writer.Bytes, 0, length);

                try
                {
                    return ClientJson.FromJson(span);
                }
                finally
                {
                    _writer.Reset();
                }
#endif
            }
        }

        /// <summary>
        /// Launches a cycle which will fetch all results of queries to TDLib and incoming updates from TDLib.
        /// Must be called once on a separate dedicated thread on which all updates and query results from all Clients will be handled.
        /// Never returns.
        /// </summary>
        public static void Run()
        {
            while (true)
            {
                var response = Receive(300.0, out int client_id, out long request_id);
                if (response != null)
                {
                    bool isClosed = response is UpdateAuthorizationState { AuthorizationState: AuthorizationStateClosed } && request_id == 0;

                    if (request_id == 0)
                    {
                        _updateHandlers.TryGetValue(client_id, out ClientResultHandler handler);
                        handler?.OnResult(response);
                    }
                    else if (_handlers.TryRemove(request_id, out Action<Object> action))
                    {
                        action(response);
                    }

                    if (isClosed)
                    {
                        _updateHandlers.TryRemove(client_id, out _);
                    }
                }
            }
        }

        [ThreadStatic]
        private static ArrayPoolBufferWriter? _writer;

#if !TD_POINTER_PARSER
        private static byte[] _buffer = new byte[1 << 18];
#endif

        /// <summary>
        /// With TD_POINTER_PARSER the update is parsed where TDLib put it: no copy into a managed
        /// buffer, and no scan for the terminator to find out how long it is. Both are needed by the
        /// Utf8JsonReader path below, which reads through a Span - and a Span over native memory
        /// costs 4x per byte on .NET Native, which is why removing only the copy once made the
        /// parse slower rather than faster. See Telegram.Benchmarks/README.md.
        /// </summary>
        public static unsafe Object Receive(double timeout, out int clientId, out long requestId)
        {
            clientId = 0;
            requestId = 0;

            var ptr = td_receive(timeout, out clientId, out requestId);
            if (ptr == null || *ptr == 0)
            {
                return new Error(400, "Can't deserialize");
            }

            _updateHandlers.TryGetValue(clientId, out ClientResultHandler handler);

#if TD_POINTER_PARSER
            var started = TdThroughput.Begin();
            var update = ClientJson.FromPtr(ptr, TdJsonReader.NulTerminated, handler);

            // The length costs a scan on this path, so it is measured rather than known - which is
            // why the timestamp closes first, inside Record.
            TdThroughput.Record(started, ptr);
            return update;
#else
            byte* end = ptr;
            while (*end != 0)
            {
                end++;
            }

            int length = (int)(end - ptr);

            if (_buffer.Length < length)
            {
                Array.Resize(ref _buffer, length);
            }

            fixed (byte* dest = _buffer)
            {
                Buffer.MemoryCopy(ptr, dest, _buffer.Length, length);
            }

            var span = new ReadOnlySpan<byte>(_buffer, 0, length);

            var started = TdThroughput.Begin();
            var update = ClientJson.FromJson(span, handler);

            TdThroughput.Record(started, length);
            return update;
#endif
        }

        private static readonly object _logMutex = new();
        private static LogMessageCallback? _logMessageCallback;

        public static void SetLogMessageCallback(int max_verbosity_level, LogMessageCallback callback)
        {
            lock (_logMutex)
            {
                if (callback == null)
                {
                    _logMessageCallback = null;
#if NET9_0_OR_GREATER
                    unsafe { td_set_log_message_callback(max_verbosity_level, null); }
#else
                    td_set_log_message_callback(max_verbosity_level, null);
#endif
                }
                else
                {
                    _logMessageCallback = callback;
#if NET9_0_OR_GREATER
                    unsafe { td_set_log_message_callback(max_verbosity_level, &LogMessageCallbackWrapper); }
#else
                    td_set_log_message_callback(max_verbosity_level, LogMessageCallbackWrapper);
#endif
                }
            }
        }

#if NET9_0_OR_GREATER
        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
        private static void LogMessageCallbackWrapper(int verbosity_level, IntPtr message)
        {
            var callback = _logMessageCallback;
            if (callback != null)
            {
                callback(verbosity_level, Marshal.PtrToStringUTF8(message));
            }
        }
    }
}
