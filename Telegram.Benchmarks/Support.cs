//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Text.Json;
using Telegram.Td;
using Telegram.Td.Api;

namespace Telegram.Benchmarks
{
    public static class Fixtures
    {
        public static byte[] Load(string name)
        {
            var assembly = typeof(Fixtures).Assembly;

            foreach (var resource in assembly.GetManifestResourceNames())
            {
                if (!resource.EndsWith("Fixtures." + name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var memory = new MemoryStream();
                stream.CopyTo(memory);

                // NUL-terminated, the way td_receive hands a payload over and the way
                // TdJsonReader requires it. Corpus payloads already carry one; fixtures are read
                // straight off disk, so it is added here.
                memory.WriteByte(0);
                return memory.ToArray();
            }

            throw new FileNotFoundException(name);
        }
    }

    /// <summary>
    /// Candidate replacement for Utf8JsonExtensions.GetObjectArray. A List&lt;T&gt; filled by Add
    /// allocates the list plus every intermediate backing array on the way to the final capacity;
    /// JSON gives no count up front, so the size is only known once the array has been read.
    /// One scratch buffer shared by every element type, rather than an ArrayPool&lt;T&gt; per type -
    /// there are ~3000 types in the schema and per-type pools would be worse than the problem.
    /// </summary>
    public static class PooledArray
    {
        [ThreadStatic]
        private static object[]? _scratch;

        public static T[] GetObjectArray<T>(ref Utf8JsonReader reader, ClientResultHandler client, Utf8JsonExtensions.GetObjectArrayHandler<T> handler) where T : Telegram.Td.Api.Object
        {
            var scratch = _scratch ??= new object[64];
            var count = 0;

            reader.Read();

            while (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.Null)
            {
                if (count == scratch.Length)
                {
                    Array.Resize(ref scratch, scratch.Length * 2);
                    _scratch = scratch;
                }

                scratch[count++] = reader.TokenType == JsonTokenType.Null ? null! : handler(ref reader, client);
                reader.Read();
            }

            if (count == 0)
            {
                return Array.Empty<T>();
            }

            var result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = (T)scratch[i];
                scratch[i] = null!; // the scratch outlives the call; don't pin the graph alive
            }

            return result;
        }
    }
}
