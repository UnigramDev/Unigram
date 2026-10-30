//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
#if !UWP
using System.IO.Compression;
#endif
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.Json;
using Telegram.Common;
using Telegram.Td;
using Telegram.Td.Api;

namespace Telegram.Benchmarks
{
    /// <summary>
    /// Drives the shipping tdjson.dll from this desktop process, so a round trip covers both
    /// halves of the JSON path: C# serialize, TDLib parse, TDLib serialize, C# parse.
    ///
    /// The DLL is built for the store, so it imports VCRUNTIME140_APP and friends, which aren't
    /// on a desktop search path. They are extracted once from the VCLibs appx that ships in the
    /// Windows SDK - nothing is copied into the repository.
    /// </summary>
    internal static unsafe class NativeTd
    {
        private const string TdJson = "tdjson.dll";

        [SuppressUnmanagedCodeSecurity]
        [DllImport(TdJson, CallingConvention = CallingConvention.Cdecl)]
        private static extern int td_create_client_id();

        [SuppressUnmanagedCodeSecurity]
        [DllImport(TdJson, CallingConvention = CallingConvention.Cdecl)]
        private static extern void td_send(int client_id, long request_id, byte* request);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(TdJson, CallingConvention = CallingConvention.Cdecl)]
        private static extern byte* td_receive(double timeout, out int client_id, out long request_id);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(TdJson, CallingConvention = CallingConvention.Cdecl)]
        private static extern byte* td_execute(byte* request);

#if !UWP
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AddDllDirectory(string path);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint flags);

        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;

        private static IntPtr _module;
#endif
        private static int _clientId;
        private static long _requestId;

        [ThreadStatic]
        private static ArrayPoolBufferWriter? _writer;

        private static byte[] _buffer = new byte[1 << 18];

        public static bool TryInitialize(out string error)
        {
            try
            {
                Load();

                // At its default verbosity TDLib logs every request to stderr, synchronously, and
                // that is what the round trips would measure.
                fixed (byte* request = "{\"@type\":\"setLogVerbosityLevel\",\"new_verbosity_level\":1}\0"u8)
                {
                    td_execute(request);
                }

                _clientId = td_create_client_id();

                // A fresh client emits updateOption/updateAuthorizationState before it goes quiet.
                // Drain them, or the first round trip picks one up instead of its own answer.
                RoundTrip(new GetOption("version"));

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void Load()
        {
#if UWP
            // tdjson.dll is in the package, built for exactly this container. Nothing to arrange.
#else
            if (_module != IntPtr.Zero)
            {
                return;
            }

            var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "ARM64" : "x64";

            // TDJSON_FOLDER names another build to load instead, a saved baseline say.
            var native = Environment.GetEnvironmentVariable("TDJSON_FOLDER")
                ?? Path.Combine(RepositoryRoot(), "Libraries", "tdjson", $"{arch.ToLowerInvariant()}-windows");
            var tdjson = Path.Combine(native, TdJson);

            if (!System.IO.File.Exists(tdjson))
            {
                throw new FileNotFoundException($"{tdjson} not found - build it with Libraries\\tdjson\\build.ps1");
            }

            SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
            AddDllDirectory(ExtractStoreCrt(arch));
            AddDllDirectory(native);

            _module = LoadLibraryExW(tdjson, IntPtr.Zero, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);
            if (_module == IntPtr.Zero)
            {
                throw new DllNotFoundException($"LoadLibraryEx({tdjson}) failed: {Marshal.GetLastWin32Error()}");
            }

            NativeLibrary.SetDllImportResolver(typeof(NativeTd).Assembly,
                (name, _, _) => name == TdJson ? _module : IntPtr.Zero);
#endif
        }

#if !UWP
        private static string ExtractStoreCrt(string arch)
        {
            var target = Path.Combine(Path.GetTempPath(), $"tdjson-storecrt-{arch}");
            Directory.CreateDirectory(target);

            if (System.IO.File.Exists(Path.Combine(target, "vcruntime140_app.dll")))
            {
                return target;
            }

            var appx = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft SDKs", "Windows Kits", "10", "ExtensionSDKs", "Microsoft.VCLibs", "14.0",
                "Appx", "Retail", arch, $"Microsoft.VCLibs.{arch.ToLowerInvariant()}.14.00.appx");

            if (!System.IO.File.Exists(appx))
            {
                throw new FileNotFoundException($"{appx} not found - install the Windows SDK's VCLibs extension");
            }

            using var archive = ZipFile.OpenRead(appx);
            foreach (var entry in archive.Entries)
            {
                if (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(Path.Combine(target, entry.Name), overwrite: true);
                }
            }

            return target;
        }

        private static string RepositoryRoot()
        {
            var dir = AppContext.BaseDirectory;

            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir, "Libraries", "tdjson")))
                {
                    return dir;
                }

                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }

            throw new DirectoryNotFoundException("repository root not found");
        }
#endif

        /// <summary>
        /// Serialize, send, wait for the answer to this request, parse. Everything Client does
        /// per request except the handler dictionaries.
        /// </summary>
        public static Telegram.Td.Api.Object RoundTrip(Function function)
        {
            var requestId = ++_requestId;

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

            return Await(requestId);
        }

        /// <summary>
        /// The same round trip with the request JSON written here rather than by ToJson, every
        /// character as \uXXXX - the shape td_send has to unescape in place, and one the shipping
        /// encoder never produces because it emits raw UTF-8 for anything non-ASCII.
        /// </summary>
        public static string? RoundTripFullyEscaped(string value)
        {
            var json = new StringBuilder("{\"@type\":\"testCallString\",\"x\":\"");
            foreach (var c in value)
            {
                json.Append("\\u").Append(((int)c).ToString("x4"));
            }

            json.Append("\"}");

            var requestId = ++_requestId;

            // One past the text, left zero: td_send takes the length with strlen.
            var text = json.ToString();
            var bytes = new byte[Encoding.UTF8.GetByteCount(text) + 1];
            Encoding.UTF8.GetBytes(text, 0, text.Length, bytes, 0);

            fixed (byte* ptr = bytes)
            {
                td_send(_clientId, requestId, ptr);
            }

            return (Await(requestId) as TestString)?.Value;
        }

        private static Telegram.Td.Api.Object Await(long requestId)
        {
            while (true)
            {
                var ptr = td_receive(10.0, out _, out long received);
                if (ptr == null)
                {
                    throw new TimeoutException($"no answer for request {requestId}");
                }

                byte* end = ptr;
                while (*end != 0)
                {
                    end++;
                }

                var length = (int)(end - ptr);
                if (received != requestId)
                {
                    continue; // an update; not what this call asked for
                }

                if (_buffer.Length < length)
                {
                    Array.Resize(ref _buffer, length);
                }

                fixed (byte* dest = _buffer)
                {
                    Buffer.MemoryCopy(ptr, dest, _buffer.Length, length);
                }

                return ClientJson.FromJson(new ReadOnlySpan<byte>(_buffer, 0, length), BenchmarkResultHandler.Instance);
            }
        }

        private static int _pending;

        /// <summary>
        /// What issuing a request costs the thread that issues it - the UI thread, in the app -
        /// with the answer left in TDLib's queue to be collected later. <see cref="Serialize"/>
        /// is the same path stopping short of td_send, so the gap between the two rows is TDLib
        /// parsing back the JSON we just wrote.
        /// </summary>
        public static object? Send(Function function)
        {
            var requestId = ++_requestId;

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
            _pending++;
            return null;
        }

        public static object? Serialize(Function function)
        {
            if (_writer == null)
            {
                _writer = new ArrayPoolBufferWriter();
            }
            else
            {
                _writer.Rent();
            }

            ClientJson.ToJson(_writer, function);
            _writer.Reset();
            return null;
        }

        /// <summary>
        /// Collects everything <see cref="Send"/> left behind, so the answers do not sit in
        /// TDLib's queue for the rest of the run.
        /// </summary>
        public static void DrainPending()
        {
            while (_pending > 0)
            {
                var ptr = td_receive(10.0, out _, out long received);
                if (ptr == null)
                {
                    break;
                }

                if (received != 0)
                {
                    _pending--;
                }
            }

            _pending = 0;
        }
    }
}
