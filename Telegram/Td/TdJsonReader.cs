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

namespace Telegram.Td.Api
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
    /// **The buffer must be NUL-terminated at [length].** td_receive guarantees this - it returns
    /// a C string - and the scanning loops rely on it: they test only for content, and the
    /// terminator stops them. A raw NUL cannot appear inside JSON (it has to be written \u0000),
    /// so it is unambiguous as an end marker, and reading past the buffer becomes impossible rather
    /// than merely checked for. It also takes a comparison per byte out of every scan loop: 4.1x
    /// against Utf8JsonReader on .NET Native, up from 3.7x when every byte was bounds-checked.
    ///
    /// On the desktop JIT the reader is slower than Utf8JsonReader and slower than this same code
    /// was before the hardening pass. The cause is not understood - two hypotheses have been
    /// measured and refuted. See the README; it does not affect the shipping toolchain.
    ///
    /// The one invariant to maintain is _index &lt;= _length, checked once per token in Read().
    /// </summary>
    public unsafe ref struct TdJsonReader
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
            // The only bounds test in the reader. Everything below is safe once this holds, because
            // the terminator at [length] stops every scan.
            if (_index > _length)
            {
                return Fail();
            }

            // Commas and colons carry no information here - the token sequence is unambiguous
            // without them, which keeps this loop to one branch per byte.
            while (true)
            {
                var c = _buffer[_index];
                if (c == ' ' || c == ',' || c == ':' || c == '\n' || c == '\r' || c == '\t')
                {
                    _index++;
                    continue;
                }

                if (c == 0)
                {
                    TokenType = JsonTokenType.None;
                    return false;
                }

                break;
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
                // A truncated literal can overshoot the terminator, which the check at the top of
                // the next Read turns into a clean stop rather than a read past the end.
                case (byte)'t':
                    _index += 4;
                    TokenType = JsonTokenType.True;
                    return true;
                case (byte)'f':
                    _index += 5;
                    TokenType = JsonTokenType.False;
                    return true;
                case (byte)'n':
                    _index += 4;
                    TokenType = JsonTokenType.Null;
                    return true;
                default:
                    return ReadNumber();
            }
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

            while (true)
            {
                var c = _buffer[_index];

                if (c == (byte)'"')
                {
                    break;
                }

                if (c == (byte)'\\')
                {
                    // Skipping the escaped byte blind would step over a terminator that is the very
                    // next byte, so this is the one place the sentinel needs help.
                    if (_buffer[_index + 1] == 0)
                    {
                        return Fail();
                    }

                    _escaped = true;
                    _index += 2;
                    continue;
                }

                if (c == 0)
                {
                    return Fail(); // unterminated string: the buffer ended first
                }

                _index++;
            }

            _valueLength = _index - _valueStart;
            _index++; // closing quote

            // A string followed by a colon is a property name. Peeking is cheaper than tracking
            // container state, and the colon itself is skipped by the next Read.
            var peek = _index;
            while (true)
            {
                var c = _buffer[peek];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t')
                {
                    peek++;
                    continue;
                }

                break;
            }

            TokenType = _buffer[peek] == (byte)':'
                ? JsonTokenType.PropertyName
                : JsonTokenType.String;

            return true;
        }

        private bool ReadNumber()
        {
            _valueStart = _index;

            while (true)
            {
                var c = _buffer[_index];
                if (c == 0 || c == (byte)',' || c == (byte)'}' || c == (byte)']' ||
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

        /// <summary>
        /// CRC32 of the current token, walking raw memory. Field dispatch compares names exactly -
        /// there are few enough per class for that to be cheap and it cannot collide - but a @type
        /// switch covers up to eighty constructors, where a hash is the right shape. Must agree
        /// with the values SchemaGenerator bakes into the switch.
        /// </summary>
        public uint ValueCrc32()
        {
            var table = ClientJson.Crc32Table;
            var crc = 0xFFFFFFFF;

            for (int i = 0; i < _valueLength; i++)
            {
                crc = table[(crc ^ _buffer[_valueStart + i]) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFF;
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
