#pragma once

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <memory>

// Three separable box passes, which converge on a Gaussian by the central limit theorem and are
// the standard cheap way to get one. Each pass carries a running sum, so it costs the same
// whatever its radius: sigma 15 is no more expensive than sigma 3, which is what the fixed-kernel
// classes this replaces could not do - they sampled every tap for every pixel, so the wide blur
// used for spoilers cost several times the narrow one.
//
// The amount is Direct2D's D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION, a *sigma*. The old helpers
// read it as a radius and were built around 7 and 31 taps, roughly sigma 1.3 and sigma 10 -
// visibly sharper than the D2D path they stood in for, on top of a radius-15 weight table that
// was misgenerated: peak off centre, summing to 0.565, so it darkened every pixel to a third.
// Deriving the box widths from sigma removes that whole class of mismatch.
class GaussianBlur
{
public:
    /// <summary>
    /// Blurs BGRA8 premultiplied pixels in place. Rows must be packed at width * 4.
    /// Premultiplied is what makes blurring the four channels independently correct.
    /// </summary>
    static void Apply(uint8_t* pixels, uint32_t width, uint32_t height, float sigma)
    {
        if (pixels == nullptr || width == 0 || height == 0 || sigma <= 0.0f)
        {
            return;
        }

        int sizes[Passes];
        BoxSizes(sigma, sizes);

        const size_t rowBytes = static_cast<size_t>(width) * 4;

        // One scratch buffer for the whole blur, not one per pass. for_overwrite because the
        // plain make_unique value-initializes, and every byte of this is written before it is
        // read - that zero fill is the size of the image, on every blur.
        auto scratch = std::make_unique_for_overwrite<uint8_t[]>(rowBytes * height);

        for (int pass = 0; pass < Passes; ++pass)
        {
            const int radius = (sizes[pass] - 1) / 2;
            if (radius < 1)
            {
                continue;
            }

            // Horizontal out to scratch, vertical back in: an even number of passes leaves the
            // result where it started, so nothing is copied back.
            BoxPass(pixels, scratch.get(), width, height, 4, rowBytes, radius);
            BoxPass(scratch.get(), pixels, height, width, rowBytes, 4, radius);
        }
    }

private:
    static constexpr int Passes = 3;

    // Box widths whose three-fold convolution has the requested standard deviation, split between
    // the two odd widths that bracket the ideal one.
    static void BoxSizes(float sigma, int (&sizes)[Passes])
    {
        const float n = static_cast<float>(Passes);
        const float variance = 12.0f * sigma * sigma;

        int lower = static_cast<int>(std::floor(std::sqrt((variance / n) + 1.0f)));
        if ((lower & 1) == 0)
        {
            lower--;
        }
        if (lower < 1)
        {
            lower = 1;
        }

        const int upper = lower + 2;
        const float ideal = (variance - n * lower * lower - 4.0f * n * lower - 3.0f * n) / (-4.0f * lower - 4.0f);
        const int count = static_cast<int>(std::lround(ideal));

        for (int i = 0; i < Passes; ++i)
        {
            sizes[i] = i < count ? lower : upper;
        }
    }

    // One box pass along a single axis, so horizontal and vertical are the same code with the two
    // strides swapped. Edges clamp, which is what keeps a flat image flat.
    static void BoxPass(const uint8_t* src, uint8_t* dst, uint32_t count, uint32_t lines,
        size_t stepPixels, size_t stepLines, int radius)
    {
        const uint32_t window = 2u * static_cast<uint32_t>(radius) + 1u;

        // Fixed-point reciprocal: the divisor is loop-invariant but not a constant, so dividing
        // would emit a real division four times per pixel.
        const uint32_t reciprocal = (1u << 16) / window;
        const uint32_t last = count - 1;

        for (uint32_t line = 0; line < lines; ++line)
        {
            const uint8_t* source = src + line * stepLines;
            uint8_t* destination = dst + line * stepLines;

            // The window at x = 0 reaches back past the edge, so the first sample stands in for
            // every position before it.
            int32_t accumulator[4];
            for (int c = 0; c < 4; ++c)
            {
                accumulator[c] = source[c] * (radius + 1);
            }

            for (int i = 1; i <= radius; ++i)
            {
                const uint8_t* sample = source + std::min(static_cast<uint32_t>(i), last) * stepPixels;
                for (int c = 0; c < 4; ++c)
                {
                    accumulator[c] += sample[c];
                }
            }

            for (uint32_t x = 0; x < count; ++x)
            {
                uint8_t* out = destination + x * stepPixels;
                for (int c = 0; c < 4; ++c)
                {
                    out[c] = static_cast<uint8_t>((static_cast<uint32_t>(accumulator[c]) * reciprocal + (1u << 15)) >> 16);
                }

                const uint8_t* entering = source + std::min(x + static_cast<uint32_t>(radius) + 1u, last) * stepPixels;
                const uint8_t* leaving = source + (x >= static_cast<uint32_t>(radius) ? x - static_cast<uint32_t>(radius) : 0u) * stepPixels;

                for (int c = 0; c < 4; ++c)
                {
                    accumulator[c] += entering[c] - leaving[c];
                }
            }
        }
    }
};
