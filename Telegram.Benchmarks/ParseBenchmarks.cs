//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Telegram.Td;
using Telegram.Td.Api;

namespace Telegram.Benchmarks
{
    /// <summary>
    /// End-to-end: what Client.Receive pays per payload, minus the interop.
    /// </summary>
    [MemoryDiagnoser]
    public class ParseBenchmarks
    {
        public IEnumerable<Payload> Payloads => Corpus.Load();

        [ParamsSource(nameof(Payloads))]
        public Payload Payload { get; set; } = null!;

        [Benchmark]
        public Telegram.Td.Api.Object FromJson()
        {
            return ClientJson.FromJson(Payload.Bytes.AsSpan(0, Payload.Length), BenchmarkResultHandler.Instance);
        }
    }

    /// <summary>
    /// Field dispatch, head to head, on the same bytes into the same objects:
    /// CRC32 of the property name into switch(hash), against switch(length) plus an exact compare.
    /// </summary>
    [MemoryDiagnoser]
    public class DispatchBenchmarks
    {
        private byte[] _message = null!;
        private byte[] _localFile = null!;

        [GlobalSetup]
        public void Setup()
        {
            _message = Fixtures.Load("message.json");
            _localFile = Fixtures.Load("localFile.json");
        }

        // Both parsers expect the reader parked on the first property after "@type", which is
        // where DoFromJson leaves it.
        private static Utf8JsonReader Open(byte[] bytes)
        {
            var reader = new Utf8JsonReader(bytes);
            reader.Read(); // {
            reader.Read(); // "@type"
            reader.Read(); // "message"
            reader.Read(); // first property name
            return reader;
        }

        [Benchmark(Baseline = true), BenchmarkCategory("message")]
        public Message Message_Crc32()
        {
            var reader = Open(_message);
            return ClientJson.FromJson_Message_Current(ref reader, BenchmarkResultHandler.Instance);
        }

        [Benchmark, BenchmarkCategory("message")]
        public Message Message_LengthAndCompare()
        {
            var reader = Open(_message);
            return ClientJson.FromJson_Message_Alt(ref reader, BenchmarkResultHandler.Instance);
        }

        [Benchmark, BenchmarkCategory("localFile")]
        public LocalFile LocalFile_Crc32()
        {
            var reader = Open(_localFile);
            return ClientJson.FromJson_LocalFile_Current(ref reader, BenchmarkResultHandler.Instance);
        }

        [Benchmark, BenchmarkCategory("localFile")]
        public LocalFile LocalFile_LengthAndCompare()
        {
            var reader = Open(_localFile);
            return ClientJson.FromJson_LocalFile_Alt(ref reader, BenchmarkResultHandler.Instance);
        }
    }

    /// <summary>
    /// Client.Receive finds the payload length with a byte-at-a-time scan over the whole payload.
    /// td_receive already has the length; the patched ABI could just return it.
    /// </summary>
    public class LengthBenchmarks
    {
        private byte[] _bytes = null!;

        [Params(482, 1401, 68200)]
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _bytes = new byte[Size + 1];
            _bytes.AsSpan(0, Size).Fill((byte)'x');
        }

        [Benchmark(Baseline = true)]
        public unsafe int ScanForNul()
        {
            fixed (byte* ptr = _bytes)
            {
                byte* end = ptr;
                while (*end != 0)
                {
                    end++;
                }

                return (int)(end - ptr);
            }
        }

        [Benchmark]
        public int IndexOfNul()
        {
            return _bytes.AsSpan().IndexOf((byte)0);
        }

        [Benchmark]
        public int LengthFromAbi()
        {
            return Size;
        }
    }

    /// <summary>
    /// Vectors land in a List&lt;T&gt; that doubles its way up from 4. A pooled scratch buffer plus
    /// one exact-sized array is the same parse with one allocation instead of log2(n) of them.
    /// </summary>
    [MemoryDiagnoser]
    public class VectorBenchmarks
    {
        private byte[] _messages = null!;

        [GlobalSetup]
        public void Setup()
        {
            foreach (var payload in Corpus.Load())
            {
                if (payload.Name.EndsWith("messages", StringComparison.Ordinal))
                {
                    _messages = payload.Bytes;
                    return;
                }
            }

            throw new InvalidOperationException("messages payload not found");
        }

        private Utf8JsonReader OpenArray()
        {
            var reader = new Utf8JsonReader(_messages.AsSpan(0, _messages.Length - 1));
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("messages"u8))
                {
                    reader.Read(); // GetObjectArray expects to start on the '[', as ParseObject leaves it
                    return reader;
                }
            }

            throw new InvalidOperationException("messages property not found");
        }

        [Benchmark(Baseline = true)]
        public int GrowingList()
        {
            var reader = OpenArray();
            return reader.GetObjectArray(BenchmarkResultHandler.Instance, ClientJson.FromJson_Message_Current).Count;
        }

        [Benchmark]
        public int PooledExact()
        {
            var reader = OpenArray();
            return PooledArray.GetObjectArray(ref reader, BenchmarkResultHandler.Instance, ClientJson.FromJson_Message_Current).Length;
        }
    }
}
