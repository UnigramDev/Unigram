//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Telegram.Td;
using Telegram.Td.Api;

namespace Telegram.Benchmarks
{
    /// <summary>
    /// The measurements themselves, host-independent. Run identically by the desktop console and
    /// by the UWP app, so a .NET Native number can be compared with a JIT one.
    /// </summary>
    public static class Suite
    {
        public static void Run(Harness harness, bool includeRoundTrips)
        {
            Memory(harness);
            NativeBuffer(harness);
            Interop(harness);
            Primitives(harness);
            Tokenize(harness);
            Parse(harness);
            Dispatch(harness);
            NulScan(harness);

            if (includeRoundTrips)
            {
                RoundTrips(harness);
            }
        }

        /// <summary>
        /// Is Span&lt;byte&gt; itself the tax? UWP resolves System.Memory's netstandard2.0 asset, so
        /// spans may not be the runtime's intrinsic ones. Same traversal three ways: if the span
        /// row is far off the other two, a hand-written tokeniser built on spans inherits the whole
        /// problem and the answer is pointers, not a new reader.
        /// </summary>
        private static void Memory(Harness harness)
        {
            var buffer = new byte[4096];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(i | 1);
            }

            harness.Measure("Buffer traversal (4KB)", "byte[] index", () => SumArray(buffer));
            harness.Measure("Buffer traversal (4KB)", "ReadOnlySpan<byte> index", () => SumSpan(buffer));
            harness.Measure("Buffer traversal (4KB)", "byte* pointer", () => SumPointer(buffer));
            harness.Measure("Buffer traversal (4KB)", "span + Unsafe.Add", () => SumSpanUnsafe(buffer));
            harness.Measure("Buffer traversal (4KB)", "ReadOnlySpan<byte> slice x256", () => SliceSpan(buffer));
        }

        private static object SumArray(byte[] buffer)
        {
            var sum = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                sum += buffer[i];
            }

            return sum;
        }

        private static object SumSpan(byte[] buffer)
        {
            ReadOnlySpan<byte> span = buffer;
            var sum = 0;
            for (int i = 0; i < span.Length; i++)
            {
                sum += span[i];
            }

            return sum;
        }

        // The indexer is what carries the bounds check and, on a portable Span, the extra
        // indirection. Taking one ref up front and walking it decides whether spans have to be
        // abandoned in hot code or merely accessed differently.
        private static object SumSpanUnsafe(byte[] buffer)
        {
            ReadOnlySpan<byte> span = buffer;
            ref var start = ref MemoryMarshal.GetReference(span);
            var sum = 0;
            for (int i = 0; i < span.Length; i++)
            {
                sum += Unsafe.Add(ref start, i);
            }

            return sum;
        }

        private static unsafe object SumPointer(byte[] buffer)
        {
            var sum = 0;
            fixed (byte* ptr = buffer)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    sum += ptr[i];
                }
            }

            return sum;
        }

        // Slicing is the operation a tokeniser does constantly - once per token.
        private static object SliceSpan(byte[] buffer)
        {
            ReadOnlySpan<byte> span = buffer;
            var sum = 0;
            for (int i = 0; i < 256; i++)
            {
                var slice = span.Slice(i, 16);
                sum += slice[0] + slice[15];
            }

            return sum;
        }

        /// <summary>
        /// td_receive hands back a native pointer. Three ways to read it, on a tokeniser-shaped
        /// workload (find every quote): walk the pointer, wrap it in a span, or copy it into a
        /// managed array first - which is what Client.Receive does today.
        /// </summary>
        private static unsafe void NativeBuffer(Harness harness)
        {
            foreach (var size in new[] { 1401, 68200 })
            {
                var native = Marshal.AllocHGlobal(size);
                var ptr = (byte*)native;

                for (int i = 0; i < size; i++)
                {
                    ptr[i] = (byte)(i % 23 == 0 ? '"' : 'x');
                }

                var managed = new byte[size];
                var label = $"({size:N0}B)";

                harness.Measure("Reading the native payload", $"byte* direct {label}", () => CountQuotesPointer(ptr, size));
                harness.Measure("Reading the native payload", $"span over native {label}", () => CountQuotesSpan(new ReadOnlySpan<byte>(ptr, size)));
                harness.Measure("Reading the native payload", $"copy then byte[] {label}", () => CopyThenCount(ptr, managed, size));

                // Deliberately not freed: the delegates above capture the pointer and the harness
                // keeps them alive for the whole run.
            }
        }

        private static unsafe object CountQuotesPointer(byte* ptr, int length)
        {
            var count = 0;
            for (int i = 0; i < length; i++)
            {
                if (ptr[i] == (byte)'"') count++;
            }

            return count;
        }

        private static object CountQuotesSpan(ReadOnlySpan<byte> span)
        {
            var count = 0;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == (byte)'"') count++;
            }

            return count;
        }

        private static unsafe object CopyThenCount(byte* source, byte[] destination, int length)
        {
            fixed (byte* target = destination)
            {
                Buffer.MemoryCopy(source, target, destination.Length, length);
            }

            var count = 0;
            for (int i = 0; i < length; i++)
            {
                if (destination[i] == (byte)'"') count++;
            }

            return count;
        }

        /// <summary>
        /// WinRT interop, which .NET Native does through MCG's compile-time stubs and .NET 9+ does
        /// through CsWinRT's projections. Thread-agile types only, so this measures the interop
        /// layer rather than XAML's own cost on top of it.
        /// </summary>
        private static void Interop(Harness harness)
        {
#if UWP
            var calendar = new Windows.Globalization.Calendar();
            var values = new Windows.Foundation.Collections.ValueSet { { "count", 1 } };

            harness.Measure("WinRT interop", "property get (int)", () => calendar.Year);
            harness.Measure("WinRT interop", "property get (string)", () => calendar.NumeralSystem);
            harness.Measure("WinRT interop", "method call", () => AddDay(calendar));
            harness.Measure("WinRT interop", "activation (new Calendar)", () => new Windows.Globalization.Calendar());
            harness.Measure("WinRT interop", "map read (boxed int)", () => values["count"]);
            harness.Measure("WinRT interop", "map write (boxed int)", () => SetValue(values));
#endif
        }

#if UWP
        // SetToNow rather than AddDays: a million AddDays(1) calls walk the calendar past the range
        // WinRT will accept, and it throws.
        private static object AddDay(Windows.Globalization.Calendar calendar)
        {
            calendar.SetToNow();
            return calendar;
        }

        private static object SetValue(Windows.Foundation.Collections.ValueSet values)
        {
            values["count"] = 2;
            return values;
        }
#endif

        /// <summary>
        /// The corelib primitives the parser leans on, so the netstandard2.0 penalty can be read
        /// per operation instead of guessed at from the total.
        /// </summary>
        private static void Primitives(Harness harness)
        {
            var name = Encoding.UTF8.GetBytes("last_message_id");
            var other = Encoding.UTF8.GetBytes("last_message_ie");
            var haystack = new byte[4096];
            haystack[4000] = (byte)'"';

            var number = Encoding.UTF8.GetBytes("7146138731234567890");
            var ascii = Encoding.UTF8.GetBytes("Hey, are we still on for tomorrow? I can move things around");
            var base64 = Convert.ToBase64String(new byte[1024]);

            harness.Measure("Corelib primitives", "SequenceEqual (15B, equal)", () => name.AsSpan().SequenceEqual(name));
            harness.Measure("Corelib primitives", "SequenceEqual (15B, differs)", () => name.AsSpan().SequenceEqual(other));
            harness.Measure("Corelib primitives", "MemoryExtensions.IndexOf (4KB)", () => haystack.AsSpan().IndexOf((byte)'"'));
            harness.Measure("Corelib primitives", "Array.IndexOf (4KB)", () => Array.IndexOf(haystack, (byte)'"'));
            harness.Measure("Corelib primitives", "Utf8Parser int64", () => ParseInt64(number));
            harness.Measure("Corelib primitives", "Encoding.UTF8.GetString (59B)", () => Encoding.UTF8.GetString(ascii, 0, ascii.Length));
            harness.Measure("Corelib primitives", "Convert.FromBase64String (1KB)", () => Convert.FromBase64String(base64));
        }

        private static object ParseInt64(byte[] bytes)
        {
            System.Buffers.Text.Utf8Parser.TryParse(bytes, out long value, out _);
            return value;
        }

        /// <summary>
        /// The tokeniser prototype, head to head with Utf8JsonReader over the same payloads doing
        /// the same work. This is the number that decides whether the rest of the idea is worth
        /// building - everything downstream inherits it.
        /// </summary>
        private static unsafe void Tokenize(Harness harness)
        {
            foreach (var payload in Corpus.Load())
            {
                var bytes = payload.Bytes;
                var length = payload.Length;

                // Native memory, because that is where td_receive's payload actually lives.
                var native = Marshal.AllocHGlobal(length);
                var ptr = (byte*)native;
                Marshal.Copy(bytes, 0, native, length);

                harness.Measure("Tokenize", $"{payload.Name} Utf8JsonReader",
                    () => Json.PointerParsers.TokenizeUtf8JsonReader(bytes, length));
                harness.Measure("Tokenize", $"{payload.Name} TdJsonReader",
                    () => Json.PointerParsers.TokenizeTdJsonReader(ptr, length));
            }

            var localFile = Fixtures.Load("localFile.json");
            var localFileNative = Marshal.AllocHGlobal(localFile.Length);
            Marshal.Copy(localFile, 0, localFileNative, localFile.Length);
            var localFilePtr = (byte*)localFileNative;

            harness.Measure("Parse localFile", "Utf8JsonReader (generated)",
                () => Open(localFile, ClientJson.FromJson_LocalFile_Current));
            harness.Measure("Parse localFile", "TdJsonReader (pointer)",
                () => Json.PointerParsers.ParseLocalFile(localFilePtr, localFile.Length));
        }

        /// <summary>
        /// The whole parse, both generated readers, same payloads and same objects out. This is the
        /// number the tokeniser work is ultimately for.
        /// </summary>
        private static unsafe void Parse(Harness harness)
        {
            foreach (var payload in Corpus.Load())
            {
                var bytes = payload.Bytes;
                var length = payload.Length;

                var native = Marshal.AllocHGlobal(length + 1);
                Marshal.Copy(bytes, 0, native, length);
                ((byte*)native)[length] = 0; // the terminator TdJsonReader scans to
                var ptr = (byte*)native;

                harness.Measure("FromJson", payload.Name,
                    () => ClientJson.FromJson(new ReadOnlySpan<byte>(bytes, 0, length), BenchmarkResultHandler.Instance));
                _native[payload.Name] = (IntPtr)ptr;
            }

            // A second pass rather than interleaved, so the report keeps each reader's rows
            // together.
            foreach (var payload in Corpus.Load())
            {
                var ptr = (byte*)_native[payload.Name];
                var length = payload.Length;

                harness.Measure("FromPtr", payload.Name,
                    () => ClientJson.FromPtr(ptr, length, BenchmarkResultHandler.Instance));
            }
        }

        private static readonly Dictionary<string, IntPtr> _native = new();

        private static void Dispatch(Harness harness)
        {
            var message = Fixtures.Load("message.json");
            var localFile = Fixtures.Load("localFile.json");

            harness.Measure("Field dispatch", "message crc32", () => Open(message, ClientJson.FromJson_Message_Current));
            harness.Measure("Field dispatch", "message length+compare", () => Open(message, ClientJson.FromJson_Message_Alt));
            harness.Measure("Field dispatch", "localFile crc32", () => Open(localFile, ClientJson.FromJson_LocalFile_Current));
            harness.Measure("Field dispatch", "localFile length+compare", () => Open(localFile, ClientJson.FromJson_LocalFile_Alt));
        }

        // Both parsers expect the reader parked on the first property after "@type", which is
        // where DoFromJson leaves it.
        private static object Open<T>(byte[] bytes, Parser<T> parser)
        {
            var reader = new Utf8JsonReader(bytes);
            reader.Read();
            reader.Read();
            reader.Read();
            reader.Read();
            return parser(ref reader, BenchmarkResultHandler.Instance)!;
        }

        private static void NulScan(Harness harness)
        {
            foreach (var size in new[] { 482, 1401, 68200 })
            {
                var bytes = new byte[size + 1];
                for (int i = 0; i < size; i++)
                {
                    bytes[i] = (byte)'x';
                }

                harness.Measure("Length of the payload", $"scan for NUL ({size:N0}B)", () => ScanForNul(bytes));
                harness.Measure("Length of the payload", $"IndexOf ({size:N0}B)", () => IndexOfNul(bytes));
            }
        }

        private static unsafe object ScanForNul(byte[] bytes)
        {
            fixed (byte* ptr = bytes)
            {
                byte* end = ptr;
                while (*end != 0)
                {
                    end++;
                }

                return (int)(end - ptr);
            }
        }

        private static object IndexOfNul(byte[] bytes)
        {
            return Array.IndexOf(bytes, (byte)0);
        }

        /// <summary>
        /// Full round trips through the real tdjson.dll using TDLib's own offline test methods:
        /// C# serialize, TDLib parse, TDLib serialize, C# parse. testCallEmpty is the floor that
        /// every other row sits on - subtract it to see what the payload itself costs.
        /// </summary>
        private static void RoundTrips(Harness harness)
        {
            // Initialized here rather than up front: a live TDLib client keeps background threads
            // busy, and on a loaded machine that lands squarely in every measurement above it.
            if (!NativeTd.TryInitialize(out var error))
            {
                harness.Note($"tdjson round trips skipped: {error}");
                return;
            }

            harness.Measure("Round trip (tdjson)", "testCallEmpty", () => NativeTd.RoundTrip(new TestCallEmpty()));
            harness.Measure("Round trip (tdjson)", "testSquareInt", () => NativeTd.RoundTrip(new TestSquareInt(7)));

            foreach (var size in new[] { 1024, 65536 })
            {
                var text = new string('x', size);
                var blob = new byte[size];

                // string and bytes differ only in that bytes is base64 on the wire, so the gap
                // between these two rows is the base64 cost, both directions, at that size.
                harness.Measure("Round trip (tdjson)", $"testCallString ({size / 1024}KB)", () => NativeTd.RoundTrip(new TestCallString(text)));
                harness.Measure("Round trip (tdjson)", $"testCallBytes ({size / 1024}KB)", () => NativeTd.RoundTrip(new TestCallBytes(blob)));
            }

            foreach (var count in new[] { 100, 1000 })
            {
                var numbers = new int[count];
                for (int i = 0; i < count; i++)
                {
                    numbers[i] = 1700000000 + i;
                }

                // Numbers formatted to ASCII and parsed back, against the same count of objects
                // each carrying one short string: number handling against per-object dispatch.
                harness.Measure("Round trip (tdjson)", $"testCallVectorInt (x{count})", () => NativeTd.RoundTrip(new TestCallVectorInt(numbers)));

                var objects = new TestString[count];
                for (int i = 0; i < count; i++)
                {
                    objects[i] = new TestString("value");
                }

                harness.Measure("Round trip (tdjson)", $"testCallVectorStringObject (x{count})", () => NativeTd.RoundTrip(new TestCallVectorStringObject(objects)));
            }
        }
    }

    public delegate T Parser<T>(ref Utf8JsonReader reader, ClientResultHandler handler);
}
