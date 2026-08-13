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
            var native = Path.Combine(RepositoryRoot(), "Libraries", "tdjson", arch);
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
    }
}
