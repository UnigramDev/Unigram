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

#nullable disable

namespace Telegram.Td.Api
{
    /// <summary>
    /// What the generated FromPtr_* parsers call, mirroring the Utf8JsonReader helpers in
    /// ClientJson.cs one for one.
    ///
    /// Nothing here refers to generated code, so it compiles in any TdParsers mode.
    /// The one member that would - the FromPtr(byte*, int) entry point - is emitted beside
    /// DoFromPtr instead.
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

        public static Vector<T> GetObjectArrayPtr<T>(ref TdJsonReader reader, ClientResultHandler handler, PtrParser<T> parser) where T : Object
        {
            reader.Read();

            // The whole point of the exercise: an empty vector - 71% of them, per TdVectorStats -
            // allocates nothing at all instead of a List that will only ever be read as empty.
            if (reader.TokenType != JsonTokenType.StartObject && reader.TokenType != JsonTokenType.Null)
            {
                return Vector<T>.Empty;
            }

            T[] items = null;
            var count = 0;

            do
            {
                if (items == null)
                {
                    items = new T[4];
                }
                else if (count == items.Length)
                {
                    Array.Resize(ref items, count * 2);
                }

                items[count++] = reader.TokenType == JsonTokenType.Null ? null : parser(ref reader, handler);
                reader.Read();
            }
            while (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.Null);

            // Trimmed rather than handed over with slack: the parsed object goes into the ClientService
            // cache and keeps whatever it holds for the session, so up to 2x wasted is worth one copy here.
            if (count != items.Length)
            {
                Array.Resize(ref items, count);
            }

            // The conversion wraps without copying, which is only safe because the parser built this array
            // and nothing else holds it.
            return items;
        }

        public static Vector<Vector<T>> GetObjectArrayArrayPtr<T>(ref TdJsonReader reader, ClientResultHandler handler, PtrParser<T> parser) where T : Object
        {
            reader.Read();

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                return Vector<Vector<T>>.Empty;
            }

            Vector<T>[] items = null;
            var count = 0;

            do
            {
                if (items == null)
                {
                    items = new Vector<T>[4];
                }
                else if (count == items.Length)
                {
                    Array.Resize(ref items, count * 2);
                }

                // Leaves the reader on the inner array's EndArray, which the Read below steps past.
                items[count++] = GetObjectArrayPtr(ref reader, handler, parser);
                reader.Read();
            }
            while (reader.TokenType == JsonTokenType.StartArray);

            if (count != items.Length)
            {
                Array.Resize(ref items, count);
            }

            return items;
        }

        public static Vector<bool> GetBooleanArrayPtr(ref TdJsonReader reader)
        {
            reader.Read();

            if (reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False)
            {
                return Vector<bool>.Empty;
            }

            bool[] items = null;
            var count = 0;

            do
            {
                if (items == null)
                {
                    items = new bool[4];
                }
                else if (count == items.Length)
                {
                    Array.Resize(ref items, count * 2);
                }

                items[count++] = reader.GetBoolean();
                reader.Read();
            }
            while (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False);

            if (count != items.Length)
            {
                Array.Resize(ref items, count);
            }

            return items;
        }

        public static Vector<int> GetInt32ArrayPtr(ref TdJsonReader reader)
        {
            reader.Read();

            if (reader.TokenType != JsonTokenType.Number)
            {
                return Vector<int>.Empty;
            }

            int[] items = null;
            var count = 0;

            do
            {
                if (items == null)
                {
                    items = new int[4];
                }
                else if (count == items.Length)
                {
                    Array.Resize(ref items, count * 2);
                }

                items[count++] = reader.GetInt32();
                reader.Read();
            }
            while (reader.TokenType == JsonTokenType.Number);

            if (count != items.Length)
            {
                Array.Resize(ref items, count);
            }

            return items;
        }

        public static Vector<long> GetInt64ArrayPtr(ref TdJsonReader reader)
        {
            reader.Read();

            if (reader.TokenType != JsonTokenType.Number)
            {
                return Vector<long>.Empty;
            }

            long[] items = null;
            var count = 0;

            do
            {
                if (items == null)
                {
                    items = new long[4];
                }
                else if (count == items.Length)
                {
                    Array.Resize(ref items, count * 2);
                }

                items[count++] = reader.GetInt64();
                reader.Read();
            }
            while (reader.TokenType == JsonTokenType.Number);

            if (count != items.Length)
            {
                Array.Resize(ref items, count);
            }

            return items;
        }

        /// <summary>int64 arrives quoted, so the elements are strings rather than numbers.</summary>
        public static Vector<long> GetInt64StringArrayPtr(ref TdJsonReader reader)
        {
            reader.Read();

            if (reader.TokenType != JsonTokenType.Number && reader.TokenType != JsonTokenType.String)
            {
                return Vector<long>.Empty;
            }

            long[] items = null;
            var count = 0;

            do
            {
                if (items == null)
                {
                    items = new long[4];
                }
                else if (count == items.Length)
                {
                    Array.Resize(ref items, count * 2);
                }

                items[count++] = reader.GetInt64String();
                reader.Read();
            }
            while (reader.TokenType == JsonTokenType.Number || reader.TokenType == JsonTokenType.String);

            if (count != items.Length)
            {
                Array.Resize(ref items, count);
            }

            return items;
        }

        public static Vector<double> GetDoubleArrayPtr(ref TdJsonReader reader)
        {
            reader.Read();

            if (reader.TokenType != JsonTokenType.Number)
            {
                return Vector<double>.Empty;
            }

            double[] items = null;
            var count = 0;

            do
            {
                if (items == null)
                {
                    items = new double[4];
                }
                else if (count == items.Length)
                {
                    Array.Resize(ref items, count * 2);
                }

                items[count++] = reader.GetDouble();
                reader.Read();
            }
            while (reader.TokenType == JsonTokenType.Number);

            if (count != items.Length)
            {
                Array.Resize(ref items, count);
            }

            return items;
        }

        public static Vector<string> GetStringArrayPtr(ref TdJsonReader reader)
        {
            reader.Read();

            if (reader.TokenType != JsonTokenType.String)
            {
                return Vector<string>.Empty;
            }

            string[] items = null;
            var count = 0;

            do
            {
                if (items == null)
                {
                    items = new string[4];
                }
                else if (count == items.Length)
                {
                    Array.Resize(ref items, count * 2);
                }

                items[count++] = reader.GetString();
                reader.Read();
            }
            while (reader.TokenType == JsonTokenType.String);

            if (count != items.Length)
            {
                Array.Resize(ref items, count);
            }

            return items;
        }

        public static Vector<byte[]> GetBase64StringArrayPtr(ref TdJsonReader reader)
        {
            reader.Read();

            if (reader.TokenType != JsonTokenType.String)
            {
                return Vector<byte[]>.Empty;
            }

            byte[][] items = null;
            var count = 0;

            do
            {
                if (items == null)
                {
                    items = new byte[4][];
                }
                else if (count == items.Length)
                {
                    Array.Resize(ref items, count * 2);
                }

                items[count++] = reader.GetBytesFromBase64();
                reader.Read();
            }
            while (reader.TokenType == JsonTokenType.String);

            if (count != items.Length)
            {
                Array.Resize(ref items, count);
            }

            return items;
        }
    }
}
