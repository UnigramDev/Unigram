//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Native.Opus;
using Windows.Media;

namespace Telegram.Common.Recording
{
    /// <summary>
    /// Encodes captured samples to Ogg/Opus as they arrive, so a voice note needs no transcoding
    /// once the recording stops.
    /// </summary>
    public sealed partial class VoiceSink : IDisposable
    {
        // The encoder is built for 48kHz mono and nothing about that is negotiable: the sample
        // rate is baked into the Opus header and the granule positions.
        public const uint SampleRate = 48000;
        public const uint ChannelCount = 1;

        // TG_OPUS_FRAME_SIZE. OpusOutput.WriteFrame consumes whole frames and silently drops
        // whatever is left over, so it must only ever be handed exact multiples of this.
        private const int FrameSize = 960;
        private const int FrameBytes = FrameSize * sizeof(float);

        private readonly OpusOutput _output;

        // One frame, locked and refilled for every 20ms of audio rather than allocated per write.
        private readonly AudioFrame _frame = new(FrameBytes);
        private readonly float[] _pending = new float[FrameSize];
        private int _pendingCount;

        private long _samples;

        // Samples arrive on a capture thread while the recording is stopped from another, and the
        // encoder underneath is native: a write racing the dispose would be a use-after-free.
        private readonly object _lock = new();
        private bool _disposed;

        public VoiceSink(string path)
        {
            _output = new OpusOutput(path);
        }

        public bool IsValid => _output.IsValid;

        /// <summary>
        /// How much audio has been encoded. Counted from the samples themselves, so it is exact
        /// and it excludes everything that happened before the first frame arrived.
        /// </summary>
        public TimeSpan Duration => TimeSpan.FromSeconds(_samples / (double)SampleRate);

        public void Write(ReadOnlySpan<float> samples)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                while (samples.Length > 0)
                {
                    var count = Math.Min(FrameSize - _pendingCount, samples.Length);

                    samples.Slice(0, count).CopyTo(_pending.AsSpan(_pendingCount));
                    samples = samples.Slice(count);

                    _pendingCount += count;

                    if (_pendingCount == FrameSize)
                    {
                        WriteFrame();
                    }
                }
            }
        }

        /// <summary>
        /// Encodes whatever is left in the buffer, padded with silence to a whole frame.
        /// </summary>
        public void Complete()
        {
            lock (_lock)
            {
                if (_disposed || _pendingCount == 0)
                {
                    return;
                }

                Array.Clear(_pending, _pendingCount, FrameSize - _pendingCount);
                WriteFrame();
            }
        }

        private unsafe void WriteFrame()
        {
            // Reset first: every path out of here has consumed the buffer, and leaving it full
            // would spin the caller's loop forever.
            _pendingCount = 0;

            using (var buffer = _frame.LockBuffer(AudioBufferAccessMode.Write))
            using (var reference = buffer.CreateReference())
            {
                reference.Buffer(out byte* data, out uint capacity);

                if (capacity < FrameBytes)
                {
                    return;
                }

                _pending.AsSpan(0, FrameSize).CopyTo(new Span<float>(data, FrameSize));
                buffer.Length = FrameBytes;
            }

            _output.WriteFrame(_frame);
            _samples += FrameSize;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                _output.Dispose();
                _frame.Dispose();
            }
        }
    }
}
