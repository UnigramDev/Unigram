//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text.Json;
using Telegram.Td.Api;

#nullable disable

namespace Telegram.Td.Api
{
    // The generated FromJson_* methods are private, so the head-to-head benchmarks reach them
    // through a partial of the same class rather than through a copy that would drift.
    public partial class ClientJson
    {
        internal static Message FromJson_Message_Current(ref Utf8JsonReader reader, ClientResultHandler handler)
        {
            return FromJson_Message(ref reader, handler);
        }

        internal static LocalFile FromJson_LocalFile_Current(ref Utf8JsonReader reader, ClientResultHandler handler)
        {
            return FromJson_LocalFile(ref reader, handler);
        }

        // FromJson_File delegates straight back to ClientResultHandler.ParseFile, so File has to be
        // assembled here or the two would call each other forever. The app does the same thing,
        // minus whatever caching its handler adds.
        internal static File FromJson_File_Bench(ref Utf8JsonReader reader, ClientResultHandler handler)
        {
            var obj = new File();
            reader.ReadStartObject();

            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.ValueSpan;
                reader.Read();

                switch (name.Length)
                {
                    case 2:
                        if (name.SequenceEqual("id"u8)) obj.Id = reader.GetInt32();
                        break;
                    case 4:
                        if (name.SequenceEqual("size"u8)) obj.Size = reader.GetInt64();
                        break;
                    case 5:
                        if (name.SequenceEqual("local"u8)) obj.Local = FromJson_LocalFile(ref reader, handler);
                        break;
                    case 6:
                        if (name.SequenceEqual("remote"u8)) obj.Remote = FromJson_RemoteFile(ref reader, handler);
                        break;
                    case 13:
                        if (name.SequenceEqual("expected_size"u8)) obj.ExpectedSize = reader.GetInt64();
                        break;
                }

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                reader.Read();
            }

            return obj;
        }

        // The pointer twin of the above, for the same reason: FromPtr_File hands File back to the
        // handler, so the handler cannot ask the generated parser for one.
        internal static File FromPtr_File_Bench(ref TdJsonReader reader, ClientResultHandler handler)
        {
            var obj = new File();
            ReadStartObjectPtr(ref reader);

            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.ValueSpan;
                reader.Read();

                switch (name.Length)
                {
                    case 2:
                        if (name.SequenceEqual("id"u8)) obj.Id = reader.GetInt32();
                        break;
                    case 4:
                        if (name.SequenceEqual("size"u8)) obj.Size = reader.GetInt64();
                        break;
                    case 5:
                        if (name.SequenceEqual("local"u8)) obj.Local = FromPtr_LocalFile(ref reader, handler);
                        break;
                    case 6:
                        if (name.SequenceEqual("remote"u8)) obj.Remote = FromPtr_RemoteFile(ref reader, handler);
                        break;
                    case 13:
                        if (name.SequenceEqual("expected_size"u8)) obj.ExpectedSize = reader.GetInt64();
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

namespace Telegram.Td
{
    // Declared in Client.cs in the app, which isn't linked here because it is all P/Invoke.
    // Must stay in Telegram.Td: the generated code refers to it unqualified from Telegram.Td.Api.
    public interface ClientResultHandler
    {
        void OnResult(Telegram.Td.Api.Object result);

        UpdateFile ParseUpdateFile(ref Utf8JsonReader reader);
        Telegram.Td.Api.File ParseFile(ref Utf8JsonReader reader);

        UpdateFile ParseUpdateFile(ref TdJsonReader reader);
        Telegram.Td.Api.File ParseFile(ref TdJsonReader reader);
    }

    // FromJson_UpdateFile delegates to the handler, so the corpus can't be parsed without one.
    // The app's implementation dedupes; this one just parses, which is the conservative choice
    // for a benchmark - it measures the parse, not someone's cache hit rate.
    internal sealed class BenchmarkResultHandler : ClientResultHandler
    {
        public static readonly BenchmarkResultHandler Instance = new();

        public void OnResult(Telegram.Td.Api.Object result)
        {
        }

        public UpdateFile ParseUpdateFile(ref Utf8JsonReader reader)
        {
            var obj = new UpdateFile();
            reader.ReadStartObject();

            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.ValueSpan;
                reader.Read();

                if (name.SequenceEqual("file"u8))
                {
                    obj.File = ParseFile(ref reader);
                }

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                reader.Read();
            }

            return obj;
        }

        public Telegram.Td.Api.File ParseFile(ref Utf8JsonReader reader)
        {
            return ClientJson.FromJson_File_Bench(ref reader, this);
        }

        public UpdateFile ParseUpdateFile(ref TdJsonReader reader)
        {
            var obj = new UpdateFile();
            ClientJson.ReadStartObjectPtr(ref reader);

            while (reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.ValueSpan;
                reader.Read();

                if (name.SequenceEqual("file"u8))
                {
                    obj.File = ParseFile(ref reader);
                }

                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    reader.Skip();
                }

                reader.Read();
            }

            return obj;
        }

        public Telegram.Td.Api.File ParseFile(ref TdJsonReader reader)
        {
            return ClientJson.FromPtr_File_Bench(ref reader, this);
        }
    }
}
