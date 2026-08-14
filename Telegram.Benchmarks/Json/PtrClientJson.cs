//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Telegram.Td;

#nullable disable

namespace Telegram.Td.Api
{
    /// <summary>
    /// What the generated FromPtr_* parsers call, mirroring the Utf8JsonReader helpers in
    /// ClientJson.cs one for one. Lives beside the benchmark for now; when the app adopts the
    /// pointer path this file moves to Telegram/Td/ unchanged.
    /// </summary>
    public partial class ClientJson
    {
        /// <summary>
        /// The same table ClientJson hashes property names with, reached through the partial rather
        /// than copied - the generated switches only work if both sides compute identical values.
        /// </summary>
        /// A property rather than a field: static field initializers run in the order the compiler
        /// happens to put the partial's files in, and as a field this read null.
        internal static uint[] Crc32Table => crc32_table;

        public delegate T PtrParser<T>(ref TdJsonReader reader, ClientResultHandler handler);

        public delegate T PtrDispatch<T>(ref TdJsonReader reader, ClientResultHandler handler, uint hash);

        /// <summary>Entry point: the whole payload, straight off td_receive's pointer.</summary>
        public static unsafe Object FromPtr(byte* buffer, int length, ClientResultHandler handler = null)
        {
            var reader = new TdJsonReader(buffer, length);
            reader.Read();

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                reader.Read();

                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("@type"u8))
                {
                    reader.Read();
                    var hash = reader.ValueCrc32();

                    reader.Read();
                    return DoFromPtr(ref reader, handler, hash);
                }
            }

            return new Error(400, "Can't deserialize");
        }

        /// <summary>
        /// Reads a nested object of an abstract type: its @type decides which parser runs.
        /// </summary>
        public static T FromPtr<T>(ref TdJsonReader reader, ClientResultHandler handler, PtrDispatch<T> dispatch) where T : Object
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            reader.Read();

            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("@type"u8))
            {
                reader.Read();
                var hash = reader.ValueCrc32();

                reader.Read();
                return dispatch(ref reader, handler, hash);
            }

            return null;
        }

        /// <summary>
        /// Steps onto the first field of an object, past the @type the sender always writes.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadStartObjectPtr(ref TdJsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                reader.Read();

                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("@type"u8))
                {
                    reader.Read();
                    reader.Read();
                }
            }
        }

        public static List<T> GetObjectArrayPtr<T>(ref TdJsonReader reader, ClientResultHandler handler, PtrParser<T> parser) where T : Object
        {
            var items = new List<T>();

            reader.Read();

            while (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.Null)
            {
                items.Add(reader.TokenType == JsonTokenType.Null ? null : parser(ref reader, handler));
                reader.Read();
            }

            return items;
        }

        public static List<IList<T>> GetObjectArrayArrayPtr<T>(ref TdJsonReader reader, ClientResultHandler handler, PtrParser<T> parser) where T : Object
        {
            var items = new List<IList<T>>();

            reader.Read();

            while (reader.TokenType == JsonTokenType.StartArray)
            {
                items.Add(GetObjectArrayPtr(ref reader, handler, parser));
                reader.Read();
            }

            return items;
        }

        public static List<bool> GetBooleanArrayPtr(ref TdJsonReader reader)
        {
            var items = new List<bool>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
            {
                items.Add(reader.GetBoolean());
                reader.Read();
            }

            return items;
        }

        public static List<int> GetInt32ArrayPtr(ref TdJsonReader reader)
        {
            var items = new List<int>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.Number)
            {
                items.Add(reader.GetInt32());
                reader.Read();
            }

            return items;
        }

        public static List<long> GetInt64ArrayPtr(ref TdJsonReader reader)
        {
            var items = new List<long>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.Number)
            {
                items.Add(reader.GetInt64());
                reader.Read();
            }

            return items;
        }

        /// <summary>int64 arrives quoted, so the elements are strings rather than numbers.</summary>
        public static List<long> GetInt64StringArrayPtr(ref TdJsonReader reader)
        {
            var items = new List<long>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.Number || reader.TokenType == JsonTokenType.String)
            {
                items.Add(reader.GetInt64String());
                reader.Read();
            }

            return items;
        }

        public static List<double> GetDoubleArrayPtr(ref TdJsonReader reader)
        {
            var items = new List<double>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.Number)
            {
                items.Add(reader.GetDouble());
                reader.Read();
            }

            return items;
        }

        public static List<string> GetStringArrayPtr(ref TdJsonReader reader)
        {
            var items = new List<string>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.String)
            {
                items.Add(reader.GetString());
                reader.Read();
            }

            return items;
        }

        public static List<byte[]> GetBase64StringArrayPtr(ref TdJsonReader reader)
        {
            var items = new List<byte[]>();

            reader.Read();
            while (reader.TokenType == JsonTokenType.String)
            {
                items.Add(reader.GetBytesFromBase64());
                reader.Read();
            }

            return items;
        }
    }
}
