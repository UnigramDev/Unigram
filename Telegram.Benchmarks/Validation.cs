//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Reflection;
using System.Text.Json;
using Telegram.Benchmarks.Json;
using Telegram.Td;
using Telegram.Td.Api;

namespace Telegram.Benchmarks
{
    /// <summary>
    /// Host-independent correctness checks, so the UWP hosts run them too. That matters because
    /// UWP resolves System.Text.Json's netstandard2.0 asset: agreeing with the net10.0 reader on
    /// the desktop says nothing about the one the app actually parses against.
    ///
    /// No reflection here - .NET Native only keeps metadata it is told to keep, and the checks that
    /// need reflection (comparing all 42 fields of a Message) stay in the desktop host where that
    /// is free.
    /// </summary>
    public static unsafe class Validation
    {
        public static bool Run(Action<string> log)
        {
            var ok = true;

            ok &= CorpusParses(log);
            ok &= TokenizerAgrees(log);
            ok &= LocalFileMatches(log);
#if !UWP
            // Both of these are desktop-only for the same reason in different forms: the deep
            // comparison reflects over the generated classes, which .NET Native only keeps metadata
            // for when told to, and VirtualAlloc is not in the app container API set. CorpusParses
            // above covers both readers on every host without needing either.
            ok &= GeneratedParsersAgree(log);
            ok &= StaysInsideItsBuffer(log);
#endif

            return ok;
        }

        /// <summary>
        /// Runs the same assertions over both readers' output. Deliberately free of reflection, so
        /// it works on .NET Native - the deep structural comparison that does need reflection is
        /// desktop-only below.
        /// </summary>
        private static bool CorpusParses(Action<string> log)
        {
            var ok = true;

            foreach (var payload in Corpus.Load())
            {
                ok &= Assert(log, payload, "FromJson",
                    ClientJson.FromJson(payload.Bytes.AsSpan(0, payload.Length), BenchmarkResultHandler.Instance));

                fixed (byte* ptr = payload.Bytes)
                {
                    ok &= Assert(log, payload, "FromPtr",
                        ClientJson.FromPtr(ptr, payload.Length, BenchmarkResultHandler.Instance));
                }
            }

            return ok;
        }

        private static bool Assert(Action<string> log, Payload payload, string reader, Telegram.Td.Api.Object parsed)
        {
            var ok = true;
            var where = new Payload { Name = payload.Name + " via " + reader, Bytes = payload.Bytes };

            switch (parsed)
            {
                case Error error:
                    ok &= Fail(log, $"{where}: {error.Code} {error.Message}");
                    break;
                case UpdateNewMessage { Message: { } message }:
                    ok &= Require(log, where, "sender", message.SenderId is MessageSenderUser { UserId: 1234567 });
                    ok &= Require(log, where, "content", message.Content is MessageText { Text.Text.Length: > 20 });
                    // A late field, to catch a parse that gave up halfway through the object.
                    ok &= Require(log, where, "last field", message.EffectId != 0);
                    break;
                case UpdateFile { File: { } file }:
                    ok &= Require(log, where, "local", file.Local != null);
                    ok &= Require(log, where, "remote", file.Remote != null);
                    break;
                case Messages messages:
                    ok &= Require(log, where, "count", messages.MessagesValue?.Count == 50);
                    break;
                case UpdateUserStatus status:
                    ok &= Require(log, where, "status", status.Status is UserStatusOnline);
                    break;
                case UpdateOption option:
                    ok &= Require(log, where, "value", option.Value is OptionValueString { Value.Length: > 5 });
                    break;
                default:
                    ok &= Fail(log, $"{where}: parsed as {parsed?.GetType().Name ?? "null"}");
                    break;
            }

            return ok;
        }

        /// <summary>
        /// The tokeniser is hand-written, so it has to agree with Utf8JsonReader token for token and
        /// string for string - the corpus carries escapes, a surrogate pair and unknown fields for
        /// exactly this.
        /// </summary>
        private static bool TokenizerAgrees(Action<string> log)
        {
            var ok = true;

            foreach (var payload in Corpus.Load())
            {
                var expected = PointerParsers.TokenizeUtf8JsonReader(payload.Bytes, payload.Length);

                fixed (byte* ptr = payload.Bytes)
                {
                    var actual = PointerParsers.TokenizeTdJsonReader(ptr, payload.Length);
                    if (expected != actual)
                    {
                        ok &= Fail(log, $"{payload}: {expected} tokens via Utf8JsonReader, {actual} via TdJsonReader");
                    }

                    var mismatch = PointerParsers.CompareStrings(payload.Bytes, payload.Length, ptr);
                    if (mismatch != null)
                    {
                        ok &= Fail(log, $"{payload}: {mismatch}");
                    }
                }
            }

            return ok;
        }

        /// <summary>
        /// The generated pointer parsers against the generated Utf8JsonReader ones, over the whole
        /// corpus. Both come from the same schema, so any disagreement is an emitter bug - which is
        /// the only way to find one across ~3000 types without reading the generated code.
        /// </summary>
        private static bool GeneratedParsersAgree(Action<string> log)
        {
            var ok = true;

            foreach (var payload in Corpus.Load())
            {
                var expected = ClientJson.FromJson(payload.Bytes.AsSpan(0, payload.Length), BenchmarkResultHandler.Instance);

                fixed (byte* ptr = payload.Bytes)
                {
                    var actual = ClientJson.FromPtr(ptr, payload.Length, BenchmarkResultHandler.Instance);

                    if (expected?.GetType() != actual?.GetType())
                    {
                        ok &= Fail(log, $"{payload}: FromJson gave {expected?.GetType().Name ?? "null"}, FromPtr gave {actual?.GetType().Name ?? "null"}");
                        continue;
                    }

                    // ToString on the generated classes is not structural, so the comparison goes
                    // through the one serializer both paths share: re-emitting each object as JSON
                    // and comparing the text catches a difference at any depth.
                    var left = Describe(expected);
                    var right = Describe(actual);

                    if (left != right)
                    {
                        ok &= Fail(log, $"{payload}: parsers disagree{Environment.NewLine}  json: {Truncate(left)}{Environment.NewLine}  ptr:  {Truncate(right)}");
                    }
                }
            }

            return ok;
        }

        /// <summary>
        /// A structural rendering of a parsed object, built by walking it rather than by
        /// serializing - only input types have a ToJson, and these are results.
        /// </summary>
        private static string Describe(object value)
        {
            var builder = new System.Text.StringBuilder();
            Describe(builder, value, 0);
            return builder.ToString();
        }

        private static void Describe(System.Text.StringBuilder builder, object value, int depth)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (depth > 12)
            {
                builder.Append("...");
                return;
            }

            switch (value)
            {
                case string text:
                    builder.Append('"').Append(text).Append('"');
                    return;
                case byte[] bytes:
                    builder.Append("bytes(").Append(bytes.Length).Append(')');
                    return;
                case System.Collections.IEnumerable items when !(value is string):
                    builder.Append('[');
                    var first = true;
                    foreach (var item in items)
                    {
                        if (!first) builder.Append(',');
                        first = false;
                        Describe(builder, item, depth + 1);
                    }
                    builder.Append(']');
                    return;
            }

            var type = value.GetType();
            if (type.IsPrimitive)
            {
                builder.Append(value);
                return;
            }

            builder.Append(type.Name).Append('{');
            var comma = false;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (comma) builder.Append(',');
                comma = true;
                builder.Append(property.Name).Append('=');
                Describe(builder, property.GetValue(value), depth + 1);
            }
            builder.Append('}');
        }

        private static string Truncate(string value)
        {
            return value.Length <= 300 ? value : value.Substring(0, 300) + "...";
        }

        private static bool LocalFileMatches(Action<string> log)
        {
            var fixture = Fixtures.Load("localFile.json");

            var reader = new Utf8JsonReader(fixture);
            reader.Read();
            reader.Read();
            reader.Read();
            reader.Read();
            var generated = ClientJson.FromJson_LocalFile_Current(ref reader, BenchmarkResultHandler.Instance);

            fixed (byte* ptr = fixture)
            {
                var pointer = PointerParsers.ParseLocalFile(ptr, fixture.Length);
                var ok = true;

                ok &= Same(log, "Path", generated.Path, pointer.Path);
                ok &= Same(log, "CanBeDownloaded", generated.CanBeDownloaded, pointer.CanBeDownloaded);
                ok &= Same(log, "CanBeDeleted", generated.CanBeDeleted, pointer.CanBeDeleted);
                ok &= Same(log, "IsDownloadingActive", generated.IsDownloadingActive, pointer.IsDownloadingActive);
                ok &= Same(log, "IsDownloadingCompleted", generated.IsDownloadingCompleted, pointer.IsDownloadingCompleted);
                ok &= Same(log, "DownloadOffset", generated.DownloadOffset, pointer.DownloadOffset);
                ok &= Same(log, "DownloadedPrefixSize", generated.DownloadedPrefixSize, pointer.DownloadedPrefixSize);
                ok &= Same(log, "DownloadedSize", generated.DownloadedSize, pointer.DownloadedSize);

                return ok;
            }
        }

#if !UWP
        /// <summary>
        /// Every prefix of every payload, with the byte after the last one on a guard page. A reader
        /// that runs off the end access-violates here instead of silently reading whatever came next
        /// in native memory - which is the failure mode that matters, because td_receive's buffer is
        /// not ours.
        /// </summary>
        private static bool StaysInsideItsBuffer(Action<string> log)
        {
            var truncations = 0;

            foreach (var payload in Corpus.Load())
            {
                using var guarded = new GuardedBuffer(payload.Length);

                for (int length = 0; length <= payload.Length; length++)
                {
                    var reader = new TdJsonReader(guarded.Place(payload.Bytes, length), length);

                    while (reader.Read())
                    {
                        // Force every accessor that touches the buffer, not just the scan.
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.PropertyName:
                            case JsonTokenType.String:
                                GC.KeepAlive(reader.GetString());
                                GC.KeepAlive(reader.ValueTextEquals("id"u8));
                                break;
                            case JsonTokenType.Number:
                                GC.KeepAlive(reader.GetInt64());
                                GC.KeepAlive(reader.GetDouble());
                                break;
                        }
                    }

                    truncations++;
                }
            }

            log($"{truncations:N0} truncations stayed inside the buffer");
            return true;
        }
#endif

        private static bool Same<T>(Action<string> log, string what, T expected, T actual)
        {
            return Equals(expected, actual) || Fail(log, $"localFile: {what} {expected} != {actual}");
        }

        private static bool Require(Action<string> log, Payload payload, string what, bool condition)
        {
            return condition || Fail(log, $"{payload}: {what}");
        }

        private static bool Fail(Action<string> log, string message)
        {
            log("FAIL " + message);
            return false;
        }
    }
}
