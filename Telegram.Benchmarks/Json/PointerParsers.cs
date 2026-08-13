//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text.Json;
using Telegram.Td.Api;

namespace Telegram.Benchmarks.Json
{
    /// <summary>
    /// Step one of the tokeniser prototype: enough to answer "is a pointer-backed reader actually
    /// faster on .NET Native", without writing parsers for the whole schema.
    ///
    /// Tokenize* walk every token of a payload doing identical work, which isolates the reader.
    /// ParseLocalFile* build the same object from the same bytes, which shows the end-to-end effect
    /// on a real type - localFile is eight scalars and no nesting, so it measures the reader rather
    /// than a graph of allocations.
    /// </summary>
    internal static unsafe class PointerParsers
    {
        // Touching a property name per token is what the real parser does, and it keeps the loop
        // from being optimised into a pure scan.
        public static int TokenizeUtf8JsonReader(byte[] buffer, int length)
        {
            var reader = new Utf8JsonReader(new ReadOnlySpan<byte>(buffer, 0, length));
            var count = 0;

            while (reader.Read())
            {
                count++;

                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("id"u8))
                {
                    count++;
                }
            }

            return count;
        }

        public static int TokenizeTdJsonReader(byte* buffer, int length)
        {
            var reader = new TdJsonReader(buffer, length);
            var count = 0;

            while (reader.Read())
            {
                count++;

                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("id"u8))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Token counts agreeing is not enough - the hand-written unescaper has to produce the same
        /// strings as Utf8JsonReader, surrogate pairs and all. Walks both in lockstep and returns
        /// the first disagreement, or null.
        /// </summary>
        public static string CompareStrings(byte[] buffer, int length, byte* pointer)
        {
            var expected = new Utf8JsonReader(new ReadOnlySpan<byte>(buffer, 0, length));
            var actual = new TdJsonReader(pointer, length);

            while (true)
            {
                var more = expected.Read();
                if (more != actual.Read())
                {
                    return "readers disagreed on end of input";
                }

                if (!more)
                {
                    return null;
                }

                if (expected.TokenType != actual.TokenType)
                {
                    return $"token {expected.TokenType} != {actual.TokenType}";
                }

                if (expected.TokenType != JsonTokenType.String && expected.TokenType != JsonTokenType.PropertyName)
                {
                    continue;
                }

                var left = expected.GetString();
                var right = actual.GetString();

                if (left != right)
                {
                    return $"string '{left}' != '{right}'";
                }
            }
        }

                public static LocalFile ParseLocalFile(byte* buffer, int length)
        {
            var reader = new TdJsonReader(buffer, length);
            reader.Read(); // {
            reader.Read(); // "@type"
            reader.Read(); // "localFile"
            reader.Read(); // first property name

            return ParseLocalFile(ref reader);
        }

        private static LocalFile ParseLocalFile(ref TdJsonReader reader)
        {
            var obj = new LocalFile();

            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.ValueSpan;
                reader.Read();

                switch (name.Length)
                {
                    case 4:
                        if (name.SequenceEqual("path"u8)) obj.Path = reader.GetString();
                        break;
                    case 15:
                        if (name.SequenceEqual("download_offset"u8)) obj.DownloadOffset = reader.GetInt64();
                        else if (name.SequenceEqual("downloaded_size"u8)) obj.DownloadedSize = reader.GetInt64();
                        break;
                    case 14:
                        if (name.SequenceEqual("can_be_deleted"u8)) obj.CanBeDeleted = reader.GetBoolean();
                        break;
                    case 17:
                        if (name.SequenceEqual("can_be_downloaded"u8)) obj.CanBeDownloaded = reader.GetBoolean();
                        break;
                    case 21:
                        if (name.SequenceEqual("is_downloading_active"u8)) obj.IsDownloadingActive = reader.GetBoolean();
                        break;
                    case 24:
                        if (name.SequenceEqual("is_downloading_completed"u8)) obj.IsDownloadingCompleted = reader.GetBoolean();
                        break;
                    case 22:
                        if (name.SequenceEqual("downloaded_prefix_size"u8)) obj.DownloadedPrefixSize = reader.GetInt64();
                        break;
                }

                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                reader.Read();
            }

            return obj;
        }
    }
}
