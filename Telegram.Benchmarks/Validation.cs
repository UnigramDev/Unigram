//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
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
            // VirtualAlloc isn't in the app container API set, and one host proving the bounds is
            // enough - the reader is the same code everywhere.
            ok &= StaysInsideItsBuffer(log);
#endif

            return ok;
        }

        private static bool CorpusParses(Action<string> log)
        {
            var ok = true;

            foreach (var payload in Corpus.Load())
            {
                var parsed = ClientJson.FromJson(payload.Bytes.AsSpan(0, payload.Length), BenchmarkResultHandler.Instance);

                switch (parsed)
                {
                    case Error error:
                        ok &= Fail(log, $"{payload}: {error.Code} {error.Message}");
                        break;
                    case UpdateNewMessage { Message: { } message }:
                        ok &= Require(log, payload, "sender", message.SenderId is MessageSenderUser { UserId: 1234567 });
                        ok &= Require(log, payload, "content", message.Content is MessageText { Text.Text.Length: > 20 });
                        // A late field, to catch a parse that gave up halfway through the object.
                        ok &= Require(log, payload, "last field", message.EffectId != 0);
                        break;
                    case UpdateFile { File: { } file }:
                        ok &= Require(log, payload, "local", file.Local != null);
                        ok &= Require(log, payload, "remote", file.Remote != null);
                        break;
                    case Messages messages:
                        ok &= Require(log, payload, "count", messages.MessagesValue?.Count == 50);
                        break;
                    case UpdateUserStatus status:
                        ok &= Require(log, payload, "status", status.Status is UserStatusOnline);
                        break;
                    case UpdateOption option:
                        ok &= Require(log, payload, "value", option.Value is OptionValueString { Value.Length: > 5 });
                        break;
                    default:
                        ok &= Fail(log, $"{payload}: parsed as {parsed?.GetType().Name ?? "null"}");
                        break;
                }
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
