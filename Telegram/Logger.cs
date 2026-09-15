//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Telegram.Native;
using Telegram.Services;
using Windows.Storage;

namespace Telegram
{
    public sealed partial class Logger
    {
        public enum LogLevel
        {
            Assert,
            Error,
            Warning,
            Info,
            Debug,
        }

        public static void Assert(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Assert, message, member, filePath, line);
        }

        public static void Debug(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Debug, message, member, filePath, line);
        }

        public static void Warning(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Warning, message, member, filePath, line);
        }

        public static void Error(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Error, message, member, filePath, line);
        }

        public static void Error(object message, Exception exception, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Error, message + "\n" + exception, member, filePath, line);
        }

        public static void Exception(Exception exception, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            // The exception, not just Environment.StackTrace: that only says where the catch is,
            // which the caller attribution already gives. A caught exception logged without its
            // own message and stack tells you nothing about what went wrong.
            Log(LogLevel.Error, exception, member, filePath, line);

            if (Constants.RELEASE)
            {
                WatchDog.TrackError(exception);
            }
        }

        public static void Info(object message = null, [CallerMemberName] string member = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int line = 0)
        {
            Log(LogLevel.Info, message, member, filePath, line);
        }

        // Ships with every crash report, so the size trades how much history a report
        // carries against how large every report gets.
        private const int TailCapacity = 200;

        private static readonly string[] _lastCalls = new string[TailCapacity];
        private static int _lastCallsHead;
        private static int _lastCallsCount;
        private static readonly object _lock = new();

        public const string LogFileName = "app_log.txt";
        public const string OldLogFileName = LogFileName + ".old";

        // Rotated the way TDLib rotates its own: when the active file passes the threshold it is
        // moved over the previous one and a new one takes its place, so the folder never holds more
        // than two and never more than twice this. A report needs both to carry the whole timeline.
        private const long RotateThreshold = 10 * 1024 * 1024;

        // Holds a whole burst of ordinary entries, so a burst is one write.
        private const int BufferCapacity = 16 * 1024;

        // Most logging happens on the UI thread, where a syscall per entry is a syscall the UI did
        // not spend on anything else. The entries are handed to a writer instead, in the order they
        // were formatted; what a hard kill loses from the tail of the file, the dump still carries.
        private static readonly Channel<(LogLevel Level, string Entry)> _channel
            = Channel.CreateUnbounded<(LogLevel, string)>(new UnboundedChannelOptions
            {
                SingleReader = true,

                // The default, spelled out: with synchronous continuations the writer would resume
                // inline on whichever thread queued the entry, which is the thread this exists to
                // keep out of the file.
                AllowSynchronousContinuations = false
            });

        private static FileStream _log;
        private static string _logPath;
        private static long _logSize;
        private static byte[] _logBuffer;
        private static int _logOffset;

        // One failure is enough: the folder is the app's own, so whatever went wrong once will go
        // wrong for every entry after it.
        private static bool _logFailed;

        // The entry format leaves the level out, which a 200 line dump can take from context but a
        // log file cannot. Prepended to the file copy alone, as bytes, so that neither the dump nor
        // an allocation pays for it.
        private static readonly byte[][] _levels =
        {
            Encoding.UTF8.GetBytes("[Assert]"),
            Encoding.UTF8.GetBytes("[Error]"),
            Encoding.UTF8.GetBytes("[Warning]"),
            Encoding.UTF8.GetBytes("[Info]"),
            Encoding.UTF8.GetBytes("[Debug]"),
        };

#if NET9_0_OR_GREATER
        [LibraryImport("kernel32.dll")]
        private static partial ulong GetTickCount64();
#else
        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();
#endif

        public static ulong TickCount => GetTickCount64();

#if NET9_0_OR_GREATER
        [LibraryImport("kernel32.dll")]
        private unsafe static partial void GetSystemTimeAsFileTime(long* pSystemTimeAsFileTime);
#else
        [System.Security.SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32.dll")]
        private unsafe static extern void GetSystemTimeAsFileTime(long* pSystemTimeAsFileTime);
#endif

        static Logger()
        {
            NativeUtils.SetLogCallback(LogCallback);

            // Unawaited on purpose: it ends when the channel does, which is never.
            _ = Task.Run(WriteAsync);
        }

        private static void LogCallback(int level, string message, string member, string filePath, int line)
        {
            Log((LogLevel)level, message, member, filePath, line);
        }

        private static unsafe void Log(LogLevel level, object message, string member, string filePath, int line)
        {
            // We use UtcNow instead of Now because Now is expensive.
            long diff = 116444736000000000;
            long time = 0;

            GetSystemTimeAsFileTime(&time);

            string entry;
            if (message != null)
            {
                entry = string.Format(FormatWithMessage, (time - diff) / 10_000_000d, level, Path.GetFileName(filePath), line, member, message);
            }
            else
            {
                entry = string.Format(FormatWithoutMessage, (time - diff) / 10_000_000d, level, Path.GetFileName(filePath), line, member);
            }

            var persist = (int)level <= AppSettings.VerbosityLevel && (level != LogLevel.Debug || message != null);

            // Queued inside the same lock as the ring rather than after it, so that the file ends
            // up in the order the entries were formatted and not the order the threads got out.
            lock (_lock)
            {
                // Overwrite the oldest slot instead of shifting the window down, which
                // copied every retained entry on each call once the window was full.
                _lastCalls[_lastCallsHead] = entry;
                _lastCallsHead = (_lastCallsHead + 1) % TailCapacity;

                if (_lastCallsCount < TailCapacity)
                {
                    _lastCallsCount++;
                }

                if (persist)
                {
                    _channel.Writer.TryWrite((level, entry));
                }
            }

            if (level != LogLevel.Debug || message != null)
            {
                System.Diagnostics.Debug.WriteLine(entry);
            }
        }

        //private const string FormatWithMessage = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}][{1}][{2}:{3}] {4}";
        //private const string FormatWithoutMessage = "[{0:yyyy-MM-dd HH\\:mm\\:ss\\:ffff}][{1}][{2}:{3}]";

        private const string FormatWithMessage = "[{0:F3}][{2}:{3}][{4}] {5}";
        private const string FormatWithoutMessage = "[{0:F3}][{2}:{3}][{4}]";

        /// <summary>
        /// Drains the queue onto the file. Everything below this runs here and nowhere else, so
        /// none of it is synchronized: the stream, the buffer and the size are the writer's alone.
        /// </summary>
        private static async Task WriteAsync()
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync())
                {
                    // Everything already queued goes into one buffer and one write. A burst of
                    // entries is what logging actually looks like, and it is the syscall that costs.
                    while (_channel.Reader.TryRead(out var item))
                    {
                        Append(item.Level, item.Entry);
                    }

                    Flush();
                }
            }
            catch
            {
                // Whatever got out of Append is not worth losing the process over, but nothing else
                // drains this queue: closing it is what keeps an unbounded queue from growing for
                // the rest of the session.
                _logFailed = true;
                _channel.Writer.TryComplete();

                while (_channel.Reader.TryRead(out _))
                {
                }
            }
        }

        private static void Append(LogLevel level, string entry)
        {
            if (_logFailed)
            {
                return;
            }

            // The native callback hands us whatever int it was given, so the level is not
            // necessarily one of ours. An unknown one goes in unlabelled rather than mislabelled.
            var prefix = (uint)level < (uint)_levels.Length ? _levels[(int)level] : Array.Empty<byte>();

            // Exact rather than GetMaxByteCount: the pass over the entry is cheap here, and a
            // three-times-the-length guess would flush a buffer that is nowhere near full.
            var length = prefix.Length + Encoding.UTF8.GetByteCount(entry) + 1;

            _logBuffer ??= new byte[BufferCapacity];

            if (_logOffset + length > _logBuffer.Length)
            {
                Flush();

                // Flushing is where writing fails, and failing drops the buffer.
                if (_logFailed)
                {
                    return;
                }
            }

            if (length > _logBuffer.Length)
            {
                // One entry longer than the whole buffer - a serialized exception, say - gets an
                // array of its own rather than growing the one every other entry shares.
                var buffer = new byte[length];

                Encode(prefix, entry, buffer, 0);
                Write(buffer, length);
            }
            else
            {
                _logOffset += Encode(prefix, entry, _logBuffer, _logOffset);
            }
        }

        private static int Encode(byte[] prefix, string entry, byte[] destination, int offset)
        {
            var start = offset;

            Buffer.BlockCopy(prefix, 0, destination, offset, prefix.Length);
            offset += prefix.Length;

            offset += Encoding.UTF8.GetBytes(entry, 0, entry.Length, destination, offset);
            destination[offset++] = (byte)'\n';

            return offset - start;
        }

        private static void Flush()
        {
            if (_logOffset > 0)
            {
                Write(_logBuffer, _logOffset);
                _logOffset = 0;
            }
        }

        private static void Write(byte[] buffer, int count)
        {
            try
            {
                if (_log == null)
                {
                    Open();
                }

                // Checked before the write rather than after, as TDLib does, so the threshold is
                // what the file is allowed to reach and not what it is allowed to start from.
                if (_logSize > RotateThreshold)
                {
                    Rotate();
                }

                _log.Write(buffer, 0, count);
                _logSize += count;
            }
            catch
            {
                _logFailed = true;

                // Dropped rather than disposed: nothing will use it again, and disposing is one
                // more thing that could throw. The handle goes with the stream when it is collected.
                _log = null;
                _logBuffer = null;
            }
        }

        private static void Open()
        {
            _logPath ??= Path.Combine(ApplicationData.Current.LocalFolder.Path, LogFileName);

            // Shared so that the diagnostics page can read the file, and TDLib upload it, while it
            // is still being written to. bufferSize 1 asks for no buffering of our own.
            _log = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 1);
            _logSize = _log.Length;
        }

        private static void Rotate()
        {
            // Closed before the move rather than moved from under the handle: the rename only has
            // to be legal once, and this way it does not depend on the share flags.
            _log.Dispose();
            _log = null;

            // Move refuses an existing destination, so the previous log goes first.
            var old = Path.Combine(ApplicationData.Current.LocalFolder.Path, OldLogFileName);

            File.Delete(old);
            File.Move(_logPath, old);

            Open();
        }

        public static unsafe string Dump()
        {
            // We use UtcNow instead of Now because Now is expensive.
            long diff = 116444736000000000;
            long time = 0;

            GetSystemTimeAsFileTime(&time);

            var builder = new StringBuilder();

            lock (_lock)
            {
                // Once the window has wrapped, the slot due to be written next is the oldest.
                var start = _lastCallsCount < TailCapacity ? 0 : _lastCallsHead;

                for (int i = 0; i < _lastCallsCount; i++)
                {
                    builder.Append(_lastCalls[(start + i) % TailCapacity]);
                    builder.Append('\n');
                }
            }

            // Marks when the report was taken. Appended to the output rather than stored as
            // an entry, so that dumping neither evicts a line nor leaves a trail of markers
            // in the next dump.
            builder.AppendFormat("[{0:F3}] Bump", (time - diff) / 10_000_000d);
            return builder.ToString();
        }
    }

    public partial class RuntimeException : Exception
    {
        public RuntimeException(Exception innerException)
            : base(innerException.Message, innerException)
        {

        }
    }
}
