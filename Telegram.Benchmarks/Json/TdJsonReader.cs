//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Telegram.Benchmarks.Json
{
    /// <summary>
    /// A pull reader over the pointer td_receive returns, shaped like the part of Utf8JsonReader
    /// the generated parser actually uses.
    ///
    /// It exists because on .NET Native every span element access costs ~10x what the same access
    /// over a byte[] or a byte* costs - the UWP framework resolves System.Memory's netstandard2.0
    /// asset, so Span&lt;T&gt; is not the runtime's own. Utf8JsonReader scans its input through a
    /// span, so it pays that on every byte. This walks raw memory instead, and only hands spans to
    /// library helpers (SequenceEqual, Utf8Parser), which are 1.5-3.6x rather than 10x.
    ///
    /// Reuses System.Text.Json's JsonTokenType so generated code needs no change beyond the reader
    /// type. Only the grammar TDLib emits is accepted: no comments, no trailing commas, no NaN.
    ///
    /// Reading past the buffer would be an out-of-bounds read of *native* memory, so every advance
    /// is bounds-checked and malformed input ends the token stream rather than running off. That
    /// costs one comparison per token, not per byte - see Failed.
    /// </summary>
    internal unsafe ref struct TdJsonReader
    {
        private readonly byte* _buffer;
        private readonly int _length;

        private int _index;
        private int _valueStart;
        private int _valueLength;
        private bool _escaped;

        public JsonTokenType TokenType { get; private set; }

        /// <summary>
        /// Set when the input ran out mid-token or a literal was malformed. The token stream ends;
        /// callers that care about the difference between "done" and "broken" check this.
        /// </summary>
        public bool Failed { get; private set; }

        public TdJsonReader(byte* buffer, int length)
        {
            _buffer = buffer;
            _length = length;
            _index = 0;
            _valueStart = 0;
            _valueLength = 0;
            _escaped = false;
            TokenType = JsonTokenType.None;
            Failed = false;
        }

        /// <summary>
        /// The current token's bytes, quotes excluded. Handed to library helpers only - indexing it
        /// in a loop is the thing this type exists to avoid.
        /// </summary>
        public ReadOnlySpan<byte> ValueSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new ReadOnlySpan<byte>(_buffer + _valueStart, _valueLength);
        }

        public bool Read()
        {
            // Commas and colons carry no information here - the token sequence is unambiguous
            // without them, which keeps this loop to one branch per byte.
            while (_index < _length)
            {
                var c = _buffer[_index];
                if (c == ' ' || c == ',' || c == ':' || c == '\n' || c == '\r' || c == '\t')
                {
                    _index++;
                    continue;
                }

                break;
            }

            if (_index >= _length)
            {
                TokenType = JsonTokenType.None;
                return false;
            }

            switch (_buffer[_index])
            {
                case (byte)'{':
                    _index++;
                    TokenType = JsonTokenType.StartObject;
                    return true;
                case (byte)'}':
                    _index++;
                    TokenType = JsonTokenType.EndObject;
                    return true;
                case (byte)'[':
                    _index++;
                    TokenType = JsonTokenType.StartArray;
                    return true;
                case (byte)']':
                    _index++;
                    TokenType = JsonTokenType.EndArray;
                    return true;
                case (byte)'"':
                    return ReadString();
                case (byte)'t':
                    if (_index + 4 > _length) return Fail();
                    _index += 4;
                    TokenType = JsonTokenType.True;
                    return true;
                case (byte)'f':
                    if (_index + 5 > _length) return Fail();
                    _index += 5;
                    TokenType = JsonTokenType.False;
                    return true;
                case (byte)'n':
                    if (_index + 4 > _length) return Fail();
                    _index += 4;
                    TokenType = JsonTokenType.Null;
                    return true;
                default:
                    return ReadNumber();
            }
        }

        private bool ReadLiteral(int length, JsonTokenType type)
        {
            if (_index + length > _length)
            {
                return Fail();
            }

            _index += length;
            TokenType = type;
            return true;
        }

        private bool Fail()
        {
            Failed = true;
            TokenType = JsonTokenType.None;
            _index = _length;
            return false;
        }

        private bool ReadString()
        {
            _index++; // opening quote
            _valueStart = _index;
            _escaped = false;

            var closed = false;

            while (_index < _length)
            {
                var c = _buffer[_index];

                if (c == (byte)'\\')
                {
                    // The escape and the byte it escapes must both be inside the buffer.
                    if (_index + 1 >= _length)
                    {
                        return Fail();
                    }

                    _escaped = true;
                    _index += 2;
                    continue;
                }

                if (c == (byte)'"')
                {
                    closed = true;
                    break;
                }

                _index++;
            }

            if (!closed)
            {
                return Fail();
            }

            _valueLength = _index - _valueStart;
            _index++; // closing quote

            // A string followed by a colon is a property name. Peeking is cheaper than tracking
            // container state, and the colon itself is skipped by the next Read.
            var peek = _index;
            while (peek < _length)
            {
                var c = _buffer[peek];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t')
                {
                    peek++;
                    continue;
                }

                break;
            }

            TokenType = peek < _length && _buffer[peek] == (byte)':'
                ? JsonTokenType.PropertyName
                : JsonTokenType.String;

            return true;
        }

        private bool ReadNumber()
        {
            _valueStart = _index;

            while (_index < _length)
            {
                var c = _buffer[_index];
                if (c == (byte)',' || c == (byte)'}' || c == (byte)']' ||
                    c == ' ' || c == '\n' || c == '\r' || c == '\t')
                {
                    break;
                }

                _index++;
            }

            _valueLength = _index - _valueStart;

            if (_valueLength == 0)
            {
                return Fail();
            }

            TokenType = JsonTokenType.Number;
            return true;
        }

        /// <summary>
        /// Steps over the current value. Unlike ClientJson.ParseObject this handles arrays as well
        /// as objects, which is the unknown-field case that would otherwise truncate an object.
        /// </summary>
        public void Skip()
        {
            if (TokenType != JsonTokenType.StartObject && TokenType != JsonTokenType.StartArray)
            {
                return;
            }

            var depth = 1;

            while (depth > 0 && Read())
            {
                if (TokenType == JsonTokenType.StartObject || TokenType == JsonTokenType.StartArray)
                {
                    depth++;
                }
                else if (TokenType == JsonTokenType.EndObject || TokenType == JsonTokenType.EndArray)
                {
                    depth--;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ValueTextEquals(ReadOnlySpan<byte> text)
        {
            return _valueLength == text.Length && ValueSpan.SequenceEqual(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetBoolean()
        {
            return TokenType == JsonTokenType.True;
        }

        public int GetInt32()
        {
            return (int)GetInt64();
        }

        /// <summary>int53 arrives as a bare number, int64 as a quoted string; both land here.</summary>
        public long GetInt64()
        {
            if (_valueLength == 0)
            {
                return 0;
            }

            var negative = _buffer[_valueStart] == (byte)'-';
            var i = negative ? _valueStart + 1 : _valueStart;
            var end = _valueStart + _valueLength;
            long value = 0;

            while (i < end)
            {
                var digit = _buffer[i] - (byte)'0';
                if (digit < 0 || digit > 9)
                {
                    // A fraction or an exponent - td_api doesn't emit those for an integer field,
                    // but truncating silently would be the wrong way to find that out.
                    return SlowGetInt64();
                }

                value = value * 10 + digit;
                i++;
            }

            return negative ? -value : value;
        }

        public long GetInt64String()
        {
            return GetInt64();
        }

        private long SlowGetInt64()
        {
            return Utf8Parser.TryParse(ValueSpan, out double value, out _) ? (long)value : 0;
        }

        public double GetDouble()
        {
            Utf8Parser.TryParse(ValueSpan, out double value, out _);
            return value;
        }

        public string GetString()
        {
            if (TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (!_escaped)
            {
                return Encoding.UTF8.GetString(_buffer + _valueStart, _valueLength);
            }

            return Unescape();
        }

        private string Unescape()
        {
            // Unescaping only ever shrinks, so the escaped length is a safe upper bound.
            var decoded = new byte[_valueLength];

            var written = 0;
            var i = _valueStart;
            var end = _valueStart + _valueLength;

            while (i < end)
            {
                var c = _buffer[i];

                if (c != (byte)'\\')
                {
                    decoded[written++] = c;
                    i++;
                    continue;
                }

                i++;
                if (i >= end)
                {
                    break; // ReadString already rejects this, so it can only be a torn value
                }

                switch (_buffer[i])
                {
                    case (byte)'n': decoded[written++] = (byte)'\n'; i++; break;
                    case (byte)'r': decoded[written++] = (byte)'\r'; i++; break;
                    case (byte)'t': decoded[written++] = (byte)'\t'; i++; break;
                    case (byte)'b': decoded[written++] = 8; i++; break;
                    case (byte)'f': decoded[written++] = 12; i++; break;
                    case (byte)'u':
                        if (i + 4 >= end)
                        {
                            i = end;
                            break;
                        }

                        i++;
                        var code = (Hex(_buffer[i]) << 12) | (Hex(_buffer[i + 1]) << 8) |
                                   (Hex(_buffer[i + 2]) << 4) | Hex(_buffer[i + 3]);
                        i += 4;

                        // An astral character arrives as a surrogate pair, i.e. two \u escapes.
                        if (code >= 0xD800 && code <= 0xDBFF && i + 5 < end &&
                            _buffer[i] == (byte)'\\' && _buffer[i + 1] == (byte)'u')
                        {
                            code = ReadSurrogatePair(code, ref i);
                        }

                        written += Encoding.UTF8.GetBytes(char.ConvertFromUtf32(code), 0,
                            code > 0xFFFF ? 2 : 1, decoded, written);
                        break;
                    default:
                        decoded[written++] = _buffer[i]; // \" \\ \/
                        i++;
                        break;
                }
            }

            return Encoding.UTF8.GetString(decoded, 0, written);
        }

        private int ReadSurrogatePair(int high, ref int i)
        {
            i += 2; // \u
            var low = (Hex(_buffer[i]) << 12) | (Hex(_buffer[i + 1]) << 8) |
                      (Hex(_buffer[i + 2]) << 4) | Hex(_buffer[i + 3]);
            i += 4;

            return 0x10000 + ((high - 0xD800) << 10) + (low - 0xDC00);
        }

        private static int Hex(byte c)
        {
            if (c >= (byte)'0' && c <= (byte)'9') return c - (byte)'0';
            if (c >= (byte)'a' && c <= (byte)'f') return c - (byte)'a' + 10;
            if (c >= (byte)'A' && c <= (byte)'F') return c - (byte)'A' + 10;
            return 0;
        }

        /// <summary>
        /// base64 without materialising the string first. Convert.TryFromBase64Chars would be
        /// tidier but is not in netstandard2.0, which is the asset UWP resolves.
        /// </summary>
        public byte[] GetBytesFromBase64()
        {
            if (TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (_valueLength == 0)
            {
                return Array.Empty<byte>();
            }

            var chars = ArrayPool<char>.Shared.Rent(_valueLength);

            try
            {
                // base64 is ASCII by definition, so widening is a byte-to-char copy.
                for (int i = 0; i < _valueLength; i++)
                {
                    chars[i] = (char)_buffer[_valueStart + i];
                }

                return Convert.FromBase64CharArray(chars, 0, _valueLength);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(chars);
            }
        }
    }
}
