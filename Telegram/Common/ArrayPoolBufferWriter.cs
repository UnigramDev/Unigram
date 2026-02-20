//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Telegram.Common
{
    /// <summary>
    /// A high-performance IBufferWriter implementation that uses ArrayPool for memory management.
    /// Thread-safe for single writer scenarios.
    /// </summary>
    public sealed partial class ArrayPoolBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private const int DefaultInitialCapacity = 16 * 1024;
        private const int MaxArrayLength = 0x7FFFFFC7; // Array.MaxLength

        private readonly ArrayPool<byte> _pool;
        private byte[] _buffer;
        private int _index;
        private bool _disposed;

        /// <summary>
        /// Gets the total number of bytes written to the buffer.
        /// </summary>
        public int WrittenCount => _index;

        /// <summary>
        /// Gets the total capacity of the current buffer.
        /// </summary>
        public int Capacity => _buffer.Length;

        /// <summary>
        /// Gets the available space in the current buffer.
        /// </summary>
        public int FreeCapacity => _buffer.Length - _index;

        /// <summary>
        /// Gets a span over the written data.
        /// </summary>
        public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _index);

        /// <summary>
        /// Gets a memory over the written data.
        /// </summary>
        public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, _index);

        public byte[] Bytes => _buffer;

        /// <summary>
        /// Initializes a new instance of ArrayPoolBufferWriter with default capacity.
        /// </summary>
        public ArrayPoolBufferWriter()
            : this(DefaultInitialCapacity, ArrayPool<byte>.Shared)
        {
        }

        /// <summary>
        /// Initializes a new instance of ArrayPoolBufferWriter with specified initial capacity.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the buffer.</param>
        public ArrayPoolBufferWriter(int initialCapacity)
            : this(initialCapacity, ArrayPool<byte>.Shared)
        {
        }

        /// <summary>
        /// Initializes a new instance of ArrayPoolBufferWriter with specified pool and capacity.
        /// </summary>
        /// <param name="initialCapacity">The initial capacity of the buffer.</param>
        /// <param name="pool">The ArrayPool to use for buffer allocation.</param>
        public ArrayPoolBufferWriter(int initialCapacity, ArrayPool<byte> pool)
        {
            if (initialCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Initial capacity must be positive.");

            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _buffer = _pool.Rent(initialCapacity);
            _index = 0;
            _disposed = false;
        }

        /// <summary>
        /// Notifies the writer that count bytes were written to the buffer returned by GetMemory/GetSpan.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            ThrowIfDisposed();

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");

            if (_index > _buffer.Length - count)
                throw new InvalidOperationException("Cannot advance past the end of the buffer.");

            _index += count;
        }

        /// <summary>
        /// Returns a Memory to write to that is at least the requested size.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            ThrowIfDisposed();
            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsMemory(_index);
        }

        /// <summary>
        /// Returns a Span to write to that is at least the requested size.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            ThrowIfDisposed();
            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsSpan(_index);
        }

        /// <summary>
        /// Writes a span of bytes to the buffer.
        /// </summary>
        public void Write(ReadOnlySpan<byte> source)
        {
            ThrowIfDisposed();

            if (source.IsEmpty)
                return;

            CheckAndResizeBuffer(source.Length);
            source.CopyTo(_buffer.AsSpan(_index));
            _index += source.Length;
        }

        /// <summary>
        /// Resets the writer to reuse the buffer.
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();
            _index = 0;
        }

        /// <summary>
        /// Copies the written data to a new array and returns it.
        /// </summary>
        public byte[] ToArray()
        {
            ThrowIfDisposed();
            return _buffer.AsSpan(0, _index).ToArray();
        }

        /// <summary>
        /// Writes the buffered data to the destination span.
        /// </summary>
        /// <param name="destination">The destination span.</param>
        /// <returns>True if the data was successfully written; otherwise, false.</returns>
        public bool TryWriteTo(Span<byte> destination)
        {
            ThrowIfDisposed();

            if (destination.Length < _index)
                return false;

            WrittenSpan.CopyTo(destination);
            return true;
        }

        public Span<byte> NullTerminated()
        {
            ThrowIfDisposed();

            if (_index == 0)
                throw new InvalidOperationException("Buffer is empty.");

            // Ensure we have enough capacity
            int requiredCapacity = _index + 1;
            if (requiredCapacity > _buffer.Length)
            {
                // Need to resize
                int newSize = Math.Max(_buffer.Length * 2, requiredCapacity);
                if ((uint)newSize > MaxArrayLength)
                {
                    newSize = Math.Max(requiredCapacity, MaxArrayLength);
                    if (newSize > MaxArrayLength)
                        throw new OutOfMemoryException("Buffer size limit exceeded.");
                }

                byte[] newBuffer = _pool.Rent(newSize);
                _buffer.AsSpan(0, _index).CopyTo(newBuffer);
                byte[] oldBuffer = _buffer;
                _buffer = newBuffer;
                _pool.Return(oldBuffer);
            }

            var pos = _index;
            _buffer[pos++] = 0;

            // Return span over the complete data
            return _buffer.AsSpan(0, pos);
        }

        /// <summary>
        /// Appends ,\"@extra\":requestId} to the buffer, replacing the last byte (assumed to be '}'),
        /// and returns a span over the complete data including the appended content.
        /// This is optimized for JSON serialization scenarios.
        /// </summary>
        /// <param name="requestId">The request ID to append.</param>
        /// <returns>A span over the complete buffer including the appended data.</returns>
        public Span<byte> NullTerminated(long requestId)
        {
            ThrowIfDisposed();

            if (_index == 0)
                throw new InvalidOperationException("Buffer is empty.");

            // Calculate how many digits we need for the number
            int digitCount = CountDigits(requestId);

            // ",\"@extra\":" = 11 bytes
            // requestId digits = digitCount bytes  
            // "}" = 1 byte
            // Total = 12 + digitCount bytes
            // We're overwriting the last byte, so we need 11 + digitCount additional bytes
            int extraLength = 11 + digitCount + 1;

            // Ensure we have enough capacity
            int requiredCapacity = _index + extraLength;
            if (requiredCapacity > _buffer.Length)
            {
                // Need to resize
                int newSize = Math.Max(_buffer.Length * 2, requiredCapacity);
                if ((uint)newSize > MaxArrayLength)
                {
                    newSize = Math.Max(requiredCapacity, MaxArrayLength);
                    if (newSize > MaxArrayLength)
                        throw new OutOfMemoryException("Buffer size limit exceeded.");
                }

                byte[] newBuffer = _pool.Rent(newSize);
                _buffer.AsSpan(0, _index).CopyTo(newBuffer);
                byte[] oldBuffer = _buffer;
                _buffer = newBuffer;
                _pool.Return(oldBuffer);
            }

            // Start writing at position _index - 1 (overwriting the last byte)
            int pos = _index - 1;

            // Write: ,\"@extra\":
            _buffer[pos++] = (byte)',';
            _buffer[pos++] = (byte)'"';
            _buffer[pos++] = (byte)'@';
            _buffer[pos++] = (byte)'e';
            _buffer[pos++] = (byte)'x';
            _buffer[pos++] = (byte)'t';
            _buffer[pos++] = (byte)'r';
            _buffer[pos++] = (byte)'a';
            _buffer[pos++] = (byte)'"';
            _buffer[pos++] = (byte)':';

            // Write the number
            pos = WriteInt64(requestId, pos, digitCount);

            // Write closing brace
            _buffer[pos++] = (byte)'}';
            _buffer[pos++] = 0;

            // Return span over the complete data
            return _buffer.AsSpan(0, pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountDigits(long value)
        {
            if (value == 0) return 1;
            if (value == long.MinValue) return 20; // "-9223372036854775808"

            int digits = 0;
            if (value < 0)
            {
                digits = 1; // for the minus sign
                value = -value;
            }

            // Binary search for digit count (faster than loop for most cases)
            if (value < 10) return digits + 1;
            if (value < 100) return digits + 2;
            if (value < 1000) return digits + 3;
            if (value < 10000) return digits + 4;
            if (value < 100000) return digits + 5;
            if (value < 1000000) return digits + 6;
            if (value < 10000000) return digits + 7;
            if (value < 100000000) return digits + 8;
            if (value < 1000000000) return digits + 9;
            if (value < 10000000000) return digits + 10;
            if (value < 100000000000) return digits + 11;
            if (value < 1000000000000) return digits + 12;
            if (value < 10000000000000) return digits + 13;
            if (value < 100000000000000) return digits + 14;
            if (value < 1000000000000000) return digits + 15;
            if (value < 10000000000000000) return digits + 16;
            if (value < 100000000000000000) return digits + 17;
            if (value < 1000000000000000000) return digits + 18;
            return digits + 19;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int WriteInt64(long value, int startPos, int digitCount)
        {
            if (value == 0)
            {
                _buffer[startPos] = (byte)'0';
                return startPos + 1;
            }

            bool isNegative = value < 0;
            int pos = startPos;

            if (isNegative)
            {
                _buffer[pos++] = (byte)'-';

                // Handle long.MinValue specially
                if (value == long.MinValue)
                {
                    ReadOnlySpan<byte> minValue = "-9223372036854775808"u8;
                    minValue.Slice(1).CopyTo(_buffer.AsSpan(pos));
                    return pos + 19;
                }

                value = -value;
            }

            // Write digits in reverse, then reverse them
            int digitStart = pos;
            int digitEnd = pos + (isNegative ? digitCount - 1 : digitCount);

            // Write digits from right to left
            int writePos = digitEnd - 1;
            while (value > 0)
            {
                _buffer[writePos--] = (byte)('0' + (value % 10));
                value /= 10;
            }

            return digitEnd;
        }

        public void Resize(int newSize)
        {
            if (newSize > _buffer.Length)
            {
                byte[] newBuffer = _pool.Rent(newSize);
                byte[] oldBuffer = _buffer;

                _buffer = newBuffer;
                _pool.Return(oldBuffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckAndResizeBuffer(int sizeHint)
        {
            Debug.Assert(sizeHint >= 0);

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }

            if (sizeHint <= FreeCapacity)
            {
                return;
            }

            int currentLength = _buffer.Length;

            // Calculate new size: at least double, but also accommodate the size hint
            int newSize = Math.Max(currentLength * 2, _index + sizeHint);

            // Ensure we don't exceed max array length
            if ((uint)newSize > MaxArrayLength)
            {
                newSize = Math.Max(_index + sizeHint, MaxArrayLength);

                if (newSize > MaxArrayLength || newSize < _index + sizeHint)
                {
                    throw new OutOfMemoryException("Buffer size limit exceeded.");
                }
            }

            // Rent new buffer
            byte[] newBuffer = _pool.Rent(newSize);

            // Copy existing data
            _buffer.AsSpan(0, _index).CopyTo(newBuffer);

            // Return old buffer to pool
            byte[] oldBuffer = _buffer;
            _buffer = newBuffer;
            _pool.Return(oldBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                ThrowDisposedException();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowDisposedException()
        {
            throw new ObjectDisposedException(nameof(ArrayPoolBufferWriter));
        }

        /// <summary>
        /// Disposes the writer and returns the buffer to the pool.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _pool.Return(_buffer);
            _buffer = null!;
            _disposed = true;
        }

        public void Rent()
        {
            if (_buffer != null)
                return;

            _buffer = _pool.Rent(DefaultInitialCapacity);
        }

        public void Reset()
        {
            if (_buffer == null)
                return;

            _pool.Return(_buffer);
            _buffer = null!;
            _index = 0;
        }
    }
}

namespace System.Text.Encodings.Web
{
    public sealed class Arm64SafeEncoder : JavaScriptEncoder
    {
        public new static readonly Arm64SafeEncoder Default = new Arm64SafeEncoder();

        public override int MaxOutputCharactersPerInputCharacter => 6;

        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {
            if (text == null || textLength == 0)
            {
                return -1;
            }

            for (int i = 0; i < textLength; i++)
            {
                if (WillEncode(text[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (unicodeScalar < 0 || unicodeScalar > 0x10FFFF ||
                (unicodeScalar >= 0xD800 && unicodeScalar <= 0xDFFF))
            {
                numberOfCharactersWritten = 0;
                return false;
            }

            if (!WillEncode(unicodeScalar))
            {
                if (bufferLength < 1)
                {
                    numberOfCharactersWritten = 0;
                    return false;
                }

                buffer[0] = (char)unicodeScalar;
                numberOfCharactersWritten = 1;
                return true;
            }

            switch (unicodeScalar)
            {
                case '\b':
                    if (bufferLength < 2)
                    {
                        numberOfCharactersWritten = 0;
                        return false;
                    }
                    buffer[0] = '\\';
                    buffer[1] = 'b';
                    numberOfCharactersWritten = 2;
                    return true;

                case '\t':
                    if (bufferLength < 2)
                    {
                        numberOfCharactersWritten = 0;
                        return false;
                    }
                    buffer[0] = '\\';
                    buffer[1] = 't';
                    numberOfCharactersWritten = 2;
                    return true;

                case '\n':
                    if (bufferLength < 2)
                    {
                        numberOfCharactersWritten = 0;
                        return false;
                    }
                    buffer[0] = '\\';
                    buffer[1] = 'n';
                    numberOfCharactersWritten = 2;
                    return true;

                case '\f':
                    if (bufferLength < 2)
                    {
                        numberOfCharactersWritten = 0;
                        return false;
                    }
                    buffer[0] = '\\';
                    buffer[1] = 'f';
                    numberOfCharactersWritten = 2;
                    return true;

                case '\r':
                    if (bufferLength < 2)
                    {
                        numberOfCharactersWritten = 0;
                        return false;
                    }
                    buffer[0] = '\\';
                    buffer[1] = 'r';
                    numberOfCharactersWritten = 2;
                    return true;

                case '\\':
                    if (bufferLength < 2)
                    {
                        numberOfCharactersWritten = 0;
                        return false;
                    }
                    buffer[0] = '\\';
                    buffer[1] = '\\';
                    numberOfCharactersWritten = 2;
                    return true;

                case '"':
                    if (bufferLength < 2)
                    {
                        numberOfCharactersWritten = 0;
                        return false;
                    }
                    buffer[0] = '\\';
                    buffer[1] = '"';
                    numberOfCharactersWritten = 2;
                    return true;

                case '\'':
                    if (bufferLength < 2)
                    {
                        numberOfCharactersWritten = 0;
                        return false;
                    }
                    buffer[0] = '\\';
                    buffer[1] = '\'';
                    numberOfCharactersWritten = 2;
                    return true;
            }

            if (unicodeScalar <= 0xFFFF)
            {
                if (bufferLength < 6)
                {
                    numberOfCharactersWritten = 0;
                    return false;
                }

                buffer[0] = '\\';
                buffer[1] = 'u';
                WriteHexDigit((unicodeScalar >> 12) & 0xF, buffer + 2);
                WriteHexDigit((unicodeScalar >> 8) & 0xF, buffer + 3);
                WriteHexDigit((unicodeScalar >> 4) & 0xF, buffer + 4);
                WriteHexDigit(unicodeScalar & 0xF, buffer + 5);
                numberOfCharactersWritten = 6;
                return true;
            }

            if (bufferLength < 12)
            {
                numberOfCharactersWritten = 0;
                return false;
            }

            unicodeScalar -= 0x10000;
            int highSurrogate = 0xD800 + ((unicodeScalar >> 10) & 0x3FF);
            int lowSurrogate = 0xDC00 + (unicodeScalar & 0x3FF);

            buffer[0] = '\\';
            buffer[1] = 'u';
            WriteHexDigit((highSurrogate >> 12) & 0xF, buffer + 2);
            WriteHexDigit((highSurrogate >> 8) & 0xF, buffer + 3);
            WriteHexDigit((highSurrogate >> 4) & 0xF, buffer + 4);
            WriteHexDigit(highSurrogate & 0xF, buffer + 5);

            buffer[6] = '\\';
            buffer[7] = 'u';
            WriteHexDigit((lowSurrogate >> 12) & 0xF, buffer + 8);
            WriteHexDigit((lowSurrogate >> 8) & 0xF, buffer + 9);
            WriteHexDigit((lowSurrogate >> 4) & 0xF, buffer + 10);
            WriteHexDigit(lowSurrogate & 0xF, buffer + 11);

            numberOfCharactersWritten = 12;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool WillEncode(int unicodeScalar)
        {
            if (unicodeScalar < 0 || unicodeScalar > 0x10FFFF ||
                (unicodeScalar >= 0xD800 && unicodeScalar <= 0xDFFF))
            {
                return true;
            }

            if (unicodeScalar < 0x20 || unicodeScalar == 0x7F)
            {
                return true;
            }

            if (unicodeScalar == '\\' || unicodeScalar == '"' || unicodeScalar == '\'')
            {
                return true;
            }

            if (unicodeScalar == '<' || unicodeScalar == '>' || unicodeScalar == '&')
            {
                return true;
            }

            if (unicodeScalar == 0x2028 || unicodeScalar == 0x2029)
            {
                return true;
            }

            if (unicodeScalar >= 0x80)
            {
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void WriteHexDigit(int value, char* output)
        {
            *output = (char)(value < 10 ? '0' + value : 'a' + (value - 10));
        }
    }
}
