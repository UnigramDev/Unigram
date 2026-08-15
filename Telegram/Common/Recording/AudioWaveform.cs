//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Telegram.Common.Recording
{
    /// <summary>
    /// Accumulates the waveform of a recording, and the microphone level that drives the blob,
    /// from the captured samples as they arrive.
    /// </summary>
    public sealed partial class AudioWaveform
    {
        // Buckets are filled to 200 and then folded in half, doubling the number of samples each
        // one covers, so the accumulator costs the same whether the recording is 1 or 100 seconds.
        private const int Capacity = 200;

        // What Telegram's waveform format holds: 100 buckets of 5 bits each.
        private const int BucketCount = 100;
        private const int BucketMax = 31;

        // 1200 samples is 25ms at 48kHz, so the blob gets a new level roughly once per frame.
        private const int LevelInterval = 1200;

        private readonly float[] _buckets = new float[Capacity];
        private int _count;

        private float _bucketPeak;
        private int _bucketCount;
        private int _bucketSize = 1;

        private float _levelPeak;
        private int _levelCount;

        /// <summary>
        /// The loudest sample since the last time a level was reported.
        /// </summary>
        public float Level { get; private set; }

        /// <summary>
        /// Folds a block of samples into the waveform. Returns true when <see cref="Level"/> has
        /// been refreshed, so the caller knows to notify without keeping a clock of its own.
        /// </summary>
        public bool Add(ReadOnlySpan<float> samples)
        {
            var level = false;

            for (int i = 0; i < samples.Length; i++)
            {
                var sample = MathF.Abs(samples[i]);

                if (_bucketPeak < sample)
                {
                    _bucketPeak = sample;
                }

                if (++_bucketCount == _bucketSize)
                {
                    _buckets[_count++] = _bucketPeak;

                    // Resetting the peak is what makes each bucket the peak of its own window.
                    // Without it the waveform is a running maximum and can only ever climb.
                    _bucketPeak = 0;
                    _bucketCount = 0;

                    if (_count == Capacity)
                    {
                        for (int j = 0; j < Capacity / 2; j++)
                        {
                            _buckets[j] = MathF.Max(_buckets[j * 2 + 0], _buckets[j * 2 + 1]);
                        }

                        _count = Capacity / 2;
                        _bucketSize *= 2;
                    }
                }

                if (_levelPeak < sample)
                {
                    _levelPeak = sample;
                }

                if (++_levelCount >= LevelInterval)
                {
                    Level = _levelPeak;
                    _levelPeak = 0;
                    _levelCount = 0;

                    level = true;
                }
            }

            return level;
        }

        public void Reset()
        {
            Array.Clear(_buckets, 0, _buckets.Length);

            _count = 0;
            _bucketPeak = 0;
            _bucketCount = 0;
            _bucketSize = 1;

            Level = 0;
            _levelPeak = 0;
            _levelCount = 0;
        }

        /// <summary>
        /// Packs the accumulated buckets into the 5-bit-per-sample waveform that voice notes carry.
        /// </summary>
        public unsafe byte[] GetWaveform()
        {
            var count = _count;
            var scaledSamples = new short[BucketCount];

            for (int i = 0; i < count; i++)
            {
                var sample = _buckets[i] * short.MaxValue;
                var index = i * BucketCount / count;
                if (scaledSamples[index] < sample)
                {
                    scaledSamples[index] = (short)sample;
                }
            }

            short peak = 0;
            long sumSamples = 0;
            for (int i = 0; i < BucketCount; i++)
            {
                var sample = scaledSamples[i];
                if (peak < sample)
                {
                    peak = sample;
                }
                sumSamples += sample;
            }

            var calculatedPeak = (ushort)(sumSamples * 1.8 / BucketCount);
            if (calculatedPeak < 2500)
            {
                calculatedPeak = 2500;
            }

            for (int i = 0; i < BucketCount; i++)
            {
                uint sample = (ushort)scaledSamples[i];
                var minPeak = Math.Min(sample, calculatedPeak);
                var resultPeak = minPeak * BucketMax / calculatedPeak;
                scaledSamples[i] = (short)/*clamping:*/ Math.Min(BucketMax, resultPeak);
            }

            var bitstreamLength = scaledSamples.Length * 5 / 8 + 1;
            var result = new byte[bitstreamLength];

            fixed (byte* data = result)
            {
                static void set_bits(byte* bytes, int bitOffset, int value)
                {
                    bytes += bitOffset / 8;
                    bitOffset %= 8;
                    *(int*)bytes |= value << bitOffset;
                }

                for (int i = 0; i < scaledSamples.Length; i++)
                {
                    set_bits(data, i * 5, scaledSamples[i] & BucketMax);
                }
            }

            return result;
        }
    }
}
