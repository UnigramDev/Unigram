using System;

namespace Unigram.Common
{
    /**
     * FFT stands for Fast Fourier Transform. It is an efficient way to calculate the Complex
     * Discrete Fourier Transform. There is not much to say about this class other than the fact
     * that when you want to analyze the spectrum of an audio buffer you will almost always use
     * this class. One restriction of this class is that the audio buffers you want to analyzeV
     * must have a length that is a power of two. If you try to construct an FFT with a
     * <code>timeSize</code> that is not a power of two, an IllegalArgumentException will be
     * thrown.
     *
     * @author Damien Di Fede
     * @see FourierTransform
     * @see <a href="http://www.dspguide.com/ch12.htm">The Fast Fourier Transform</a>
     */
    public class FastFourierTransform
    {
        protected int _timeSize;
        protected int _sampleRate;
        protected float[] _real;
        protected float[] _imag;

        /// <summary>
        /// Constructs an FFT that will accept sample buffers that are
        /// <code>timeSize</code> long and have been recorded with a sample rate of
        /// <code>sampleRate</code>. <code>timeSize</code> <em>must</em> be a
        /// power of two. This will throw an exception if it is not.
        /// </summary>
        /// <param name="ts">the length of the sample buffers you will be analyzing</param>
        /// <param name="sr">the sample rate of the audio you will be analyzing</param>
        public FastFourierTransform(int ts, float sr)
        {
            _timeSize = ts;
            _sampleRate = (int)sr;

            AllocateArrays();

            if ((_timeSize & (_timeSize - 1)) != 0)
                throw new ArgumentOutOfRangeException(
                        "FFT: timeSize must be a power of two.");
            BuildReverseTable();
            BuildTrigTables();
        }

        // allocating real, imag, and spectrum are the responsibility of derived
        // classes
        // because the size of the arrays will depend on the implementation being used
        // this enforces that responsibility
        protected void AllocateArrays()
        {
            _real = new float[_timeSize];
            _imag = new float[_timeSize];
        }

        /// <summary>
        /// Get the Real part of the Complex representation of the spectrum.
        /// </summary>
        public float[] SpectrumReal => _real;

        /// <summary>
        /// Get the Imaginary part of the Complex representation of the spectrum.
        /// </summary>
        public float[] SpectrumImaginary => _imag;

        /// <summary>
        /// Performs a forward transform on <code>buffer</code>.
        /// </summary>
        /// <param name="buffer">the buffer to analyze</param>
        /// <param name="length">the buffer length</param>
        public unsafe void Forward(short* buffer, uint length)
        {
            if (length != _timeSize)
            {
                //    Minim.error("FFT.forward: The length of the passed sample buffer must be equal to timeSize().");
                return;
            }
            //  doWindow(buffer);
            // copy samples to real/imag in bit-reversed order
            BitReverseSamples(buffer, 0);
            // perform the fft
            Fft();
        }

        // performs an in-place fft on the data in the real and imag arrays
        // bit reversing is not necessary as the data will already be bit reversed
        private void Fft()
        {
            for (int halfSize = 1; halfSize < _real.Length; halfSize *= 2)
            {
                // float k = -(float)Math.PI/halfSize;
                // phase shift step
                // float phaseShiftStepR = (float)Math.cos(k);
                // float phaseShiftStepI = (float)Math.sin(k);
                // using lookup table
                float phaseShiftStepR = Cos(halfSize);
                float phaseShiftStepI = Sin(halfSize);
                // current phase shift
                float currentPhaseShiftR = 1.0f;
                float currentPhaseShiftI = 0.0f;
                for (int fftStep = 0; fftStep < halfSize; fftStep++)
                {
                    for (int i = fftStep; i < _real.Length; i += 2 * halfSize)
                    {
                        int off = i + halfSize;
                        float tr = (currentPhaseShiftR * _real[off]) - (currentPhaseShiftI * _imag[off]);
                        float ti = (currentPhaseShiftR * _imag[off]) + (currentPhaseShiftI * _real[off]);
                        _real[off] = _real[i] - tr;
                        _imag[off] = _imag[i] - ti;
                        _real[i] += tr;
                        _imag[i] += ti;
                    }
                    float tmpR = currentPhaseShiftR;
                    currentPhaseShiftR = (tmpR * phaseShiftStepR) - (currentPhaseShiftI * phaseShiftStepI);
                    currentPhaseShiftI = (tmpR * phaseShiftStepI) + (currentPhaseShiftI * phaseShiftStepR);
                }
            }
        }

        private int[] _reverse;

        private void BuildReverseTable()
        {
            int N = _timeSize;
            _reverse = new int[N];

            // set up the bit reversing table
            _reverse[0] = 0;
            for (int limit = 1, bit = N / 2; limit < N; limit <<= 1, bit >>= 1)
                for (int i = 0; i < limit; i++)
                    _reverse[i + limit] = _reverse[i] + bit;
        }

        // copies the values in the samples array into the real array
        // in bit reversed order. the imag array is filled with zeros.
        private unsafe void BitReverseSamples(short* samples, int startAt)
        {
            for (int i = 0; i < _timeSize; ++i)
            {
                _real[i] = samples[startAt + _reverse[i]] / 32768.0F;
                _imag[i] = 0.0f;
            }
        }

        // lookup tables

        private float[] _sinLookup;
        private float[] _cosLookup;

        private float Sin(int i)
        {
            return _sinLookup[i];
        }

        private float Cos(int i)
        {
            return _cosLookup[i];
        }

        private void BuildTrigTables()
        {
            int N = _timeSize;
            _sinLookup = new float[N];
            _cosLookup = new float[N];
            for (int i = 0; i < N; i++)
            {
                _sinLookup[i] = MathF.Sin(-(float)Math.PI / i);
                _cosLookup[i] = MathF.Cos(-(float)Math.PI / i);
            }
        }
    }
}
