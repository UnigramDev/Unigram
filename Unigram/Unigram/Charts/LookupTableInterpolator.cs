using System;

namespace Unigram.Charts
{
    public abstract class LookupTableInterpolator
    {
        private readonly float[] mValues;
        private readonly float mStepSize;
        protected LookupTableInterpolator(float[] values)
        {
            mValues = values;
            mStepSize = 1f / (mValues.Length - 1);
        }

        //@Override
        public float getInterpolation(float input)
        {
            if (input >= 1.0f)
            {
                return 1.0f;
            }
            if (input <= 0f)
            {
                return 0f;
            }
            // Calculate index - We use min with length - 2 to avoid IndexOutOfBoundsException when
            // we lerp (linearly interpolate) in the return statement
            int position = Math.Min((int)(input * (mValues.Length - 1)), mValues.Length - 2);
            // Calculate values to account for small offsets as the lookup table has discrete values
            float quantized = position * mStepSize;
            float diff = input - quantized;
            float weight = diff / mStepSize;
            // Linearly interpolate between the table values
            return mValues[position] + weight * (mValues[position + 1] - mValues[position]);
        }
    }
}
