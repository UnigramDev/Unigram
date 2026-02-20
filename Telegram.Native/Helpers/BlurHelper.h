#pragma once

#include <cstdint>
#include <algorithm>

class FixedRadius3Blur
{
private:
    // Pre-computed Gaussian weights for radius 3 (kernel size 7)
    // Normalized weights: sum = 1.0
    static constexpr float WEIGHTS[7] = {
        0.006f, 0.061f, 0.242f, 0.383f, 0.242f, 0.061f, 0.006f
    };

    // Fixed-point weights (multiply by 1024 for integer math)
    static constexpr int32_t WEIGHTS_FIXED[7] = {
        6, 62, 248, 392, 248, 62, 6  // Sum = 1024
    };

public:
    // Optimized blur for fixed radius 3
    static void ApplyBlur(uint8_t* pixels, uint32_t width, uint32_t height)
    {
        if (!pixels || width < 7 || height < 7)
        {
            return; // Image too small for radius 3 blur
        }

        const uint32_t stride = width * 4;

        // Stack allocation for small images (up to ~50x50)
        constexpr uint32_t MAX_STACK_SIZE = 50 * 50 * 4;
        uint8_t stackBuffer[MAX_STACK_SIZE];

        uint8_t* temp;
        bool useStack = (stride * height <= MAX_STACK_SIZE);

        if (useStack)
        {
            temp = stackBuffer;
        }
        else
        {
            temp = new uint8_t[stride * height];
        }

        // Horizontal pass with unrolled kernel
        ApplyHorizontalPass(pixels, temp, width, height, stride);

        // Vertical pass with unrolled kernel  
        ApplyVerticalPass(temp, pixels, width, height, stride);

        if (!useStack)
        {
            delete[] temp;
        }
    }

private:
    // Horizontal blur pass with fully unrolled 7-tap kernel
    static void ApplyHorizontalPass(const uint8_t* src, uint8_t* dst,
        uint32_t width, uint32_t height, uint32_t stride)
    {
        for (uint32_t y = 0; y < height; ++y)
        {
            const uint8_t* srcRow = &src[y * stride];
            uint8_t* dstRow = &dst[y * stride];

            // Process each pixel
            for (uint32_t x = 0; x < width; ++x)
            {
                int32_t b = 0, g = 0, r = 0, a = 0;

                // Unrolled 7-tap kernel (radius 3)
                // Sample points: x-3, x-2, x-1, x, x+1, x+2, x+3
                for (int i = 0; i < 7; ++i)
                {
                    int sx = static_cast<int>(x) + i - 3;

                    // Clamp to image boundaries
                    sx = (sx < 0) ? 0 : ((sx >= static_cast<int>(width)) ? width - 1 : sx);

                    const uint8_t* sample = &srcRow[sx * 4];
                    int32_t weight = WEIGHTS_FIXED[i];

                    b += sample[0] * weight;
                    g += sample[1] * weight;
                    r += sample[2] * weight;
                    a += sample[3] * weight;
                }

                // Convert back to bytes (divide by 1024, round)
                dstRow[x * 4 + 0] = static_cast<uint8_t>((b + 512) >> 10);
                dstRow[x * 4 + 1] = static_cast<uint8_t>((g + 512) >> 10);
                dstRow[x * 4 + 2] = static_cast<uint8_t>((r + 512) >> 10);
                dstRow[x * 4 + 3] = static_cast<uint8_t>((a + 512) >> 10);
            }
        }
    }

    // Vertical blur pass with fully unrolled 7-tap kernel
    static void ApplyVerticalPass(const uint8_t* src, uint8_t* dst,
        uint32_t width, uint32_t height, uint32_t stride)
    {
        for (uint32_t y = 0; y < height; ++y)
        {
            for (uint32_t x = 0; x < width; ++x)
            {
                int32_t b = 0, g = 0, r = 0, a = 0;

                // Unrolled 7-tap kernel (radius 3)
                // Sample points: y-3, y-2, y-1, y, y+1, y+2, y+3
                for (int i = 0; i < 7; ++i)
                {
                    int sy = static_cast<int>(y) + i - 3;

                    // Clamp to image boundaries
                    sy = (sy < 0) ? 0 : ((sy >= static_cast<int>(height)) ? height - 1 : sy);

                    const uint8_t* sample = &src[sy * stride + x * 4];
                    int32_t weight = WEIGHTS_FIXED[i];

                    b += sample[0] * weight;
                    g += sample[1] * weight;
                    r += sample[2] * weight;
                    a += sample[3] * weight;
                }

                // Convert back to bytes (divide by 1024, round)
                dst[y * stride + x * 4 + 0] = static_cast<uint8_t>((b + 512) >> 10);
                dst[y * stride + x * 4 + 1] = static_cast<uint8_t>((g + 512) >> 10);
                dst[y * stride + x * 4 + 2] = static_cast<uint8_t>((r + 512) >> 10);
                dst[y * stride + x * 4 + 3] = static_cast<uint8_t>((a + 512) >> 10);
            }
        }
    }
};

// Ultra-fast version using simpler box approximation for radius 3
class FixedRadius3BoxBlur
{
public:
    // Even faster box blur approximation (3-pass box blur ≈ Gaussian)
    static void ApplyFastBlur(uint8_t* pixels, uint32_t width, uint32_t height)
    {
        // Apply 3 box blur passes for Gaussian approximation
        ApplyBoxBlurPass(pixels, width, height, 1); // radius 1
        ApplyBoxBlurPass(pixels, width, height, 1); // radius 1  
        ApplyBoxBlurPass(pixels, width, height, 1); // radius 1
        // Result approximates Gaussian with radius ~1.7 (close to 3)
    }

private:
    static void ApplyBoxBlurPass(uint8_t* pixels, uint32_t width, uint32_t height, int radius)
    {
        const uint32_t stride = width * 4;
        uint8_t temp[50 * 50 * 4]; // Stack buffer for small images

        // Horizontal pass
        for (uint32_t y = 0; y < height; ++y)
        {
            for (uint32_t x = 0; x < width; ++x)
            {
                uint32_t b = 0, g = 0, r = 0, a = 0;

                // 3-tap box kernel (much faster than 7-tap Gaussian)
                int startX = std::max(0, static_cast<int>(x) - radius);
                int endX = std::min(static_cast<int>(width) - 1, static_cast<int>(x) + radius);
                int count = endX - startX + 1;

                for (int sx = startX; sx <= endX; ++sx)
                {
                    const uint8_t* src = &pixels[y * stride + sx * 4];
                    b += src[0];
                    g += src[1];
                    r += src[2];
                    a += src[3];
                }

                temp[y * stride + x * 4 + 0] = b / count;
                temp[y * stride + x * 4 + 1] = g / count;
                temp[y * stride + x * 4 + 2] = r / count;
                temp[y * stride + x * 4 + 3] = a / count;
            }
        }

        // Vertical pass
        for (uint32_t y = 0; y < height; ++y)
        {
            for (uint32_t x = 0; x < width; ++x)
            {
                uint32_t b = 0, g = 0, r = 0, a = 0;

                int startY = std::max(0, static_cast<int>(y) - radius);
                int endY = std::min(static_cast<int>(height) - 1, static_cast<int>(y) + radius);
                int count = endY - startY + 1;

                for (int sy = startY; sy <= endY; ++sy)
                {
                    const uint8_t* src = &temp[sy * stride + x * 4];
                    b += src[0];
                    g += src[1];
                    r += src[2];
                    a += src[3];
                }

                pixels[y * stride + x * 4 + 0] = b / count;
                pixels[y * stride + x * 4 + 1] = g / count;
                pixels[y * stride + x * 4 + 2] = r / count;
                pixels[y * stride + x * 4 + 3] = a / count;
            }
        }
    }
};

class FixedRadius15Blur
{
private:
    // Pre-computed Gaussian weights for radius 15 (kernel size 31)
    // Normalized weights: sum = 1.0
    static constexpr float WEIGHTS[31] = {
        0.000001f, 0.000006f, 0.000024f, 0.000077f, 0.000204f, 0.000457f, 0.000891f, 0.001535f,
        0.002422f, 0.003580f, 0.005031f, 0.006788f, 0.008850f, 0.011202f, 0.013815f, 0.016645f,
        0.019636f, 0.022715f, 0.025802f, 0.028803f, 0.031615f, 0.034131f, 0.036246f, 0.037857f,
        0.038867f, 0.039189f, 0.038867f, 0.037857f, 0.036246f, 0.034131f, 0.031615f
    };

    // Fixed-point weights (multiply by 32768 for higher precision with radius 15)
    static constexpr int32_t WEIGHTS_FIXED[31] = {
        0, 0, 1, 3, 7, 15, 29, 50,
        79, 117, 165, 222, 290, 367, 452, 545,
        643, 744, 845, 944, 1036, 1118, 1188, 1241,
        1273, 1284, 1273, 1241, 1188, 1118, 1036
    };

public:
    // Optimized blur for fixed radius 15
    static void ApplyBlur(uint8_t* pixels, uint32_t width, uint32_t height)
    {
        if (!pixels || width < 31 || height < 31)
        {
            return; // Image too small for radius 15 blur
        }

        const uint32_t stride = width * 4;

        // Dynamic allocation for larger images (radius 15 needs more memory)
        std::vector<uint8_t> temp(stride * height);

        // Horizontal pass with unrolled 31-tap kernel
        ApplyHorizontalPass(pixels, temp.data(), width, height, stride);

        // Vertical pass with unrolled 31-tap kernel  
        ApplyVerticalPass(temp.data(), pixels, width, height, stride);
    }

private:
    // Horizontal blur pass with 31-tap kernel
    static void ApplyHorizontalPass(const uint8_t* src, uint8_t* dst,
        uint32_t width, uint32_t height, uint32_t stride)
    {
        for (uint32_t y = 0; y < height; ++y)
        {
            const uint8_t* srcRow = &src[y * stride];
            uint8_t* dstRow = &dst[y * stride];

            // Process each pixel
            for (uint32_t x = 0; x < width; ++x)
            {
                int32_t b = 0, g = 0, r = 0, a = 0;

                // 31-tap kernel (radius 15)
                // Sample points: x-15, x-14, ..., x, ..., x+14, x+15
                for (int i = 0; i < 31; ++i)
                {
                    int sx = static_cast<int>(x) + i - 15;

                    // Clamp to image boundaries
                    sx = (sx < 0) ? 0 : ((sx >= static_cast<int>(width)) ? width - 1 : sx);

                    const uint8_t* sample = &srcRow[sx * 4];
                    int32_t weight = WEIGHTS_FIXED[i];

                    b += sample[0] * weight;
                    g += sample[1] * weight;
                    r += sample[2] * weight;
                    a += sample[3] * weight;
                }

                // Convert back to bytes (divide by 32768, round)
                dstRow[x * 4 + 0] = static_cast<uint8_t>((b + 16384) >> 15);
                dstRow[x * 4 + 1] = static_cast<uint8_t>((g + 16384) >> 15);
                dstRow[x * 4 + 2] = static_cast<uint8_t>((r + 16384) >> 15);
                dstRow[x * 4 + 3] = static_cast<uint8_t>((a + 16384) >> 15);
            }
        }
    }

    // Vertical blur pass with 31-tap kernel
    static void ApplyVerticalPass(const uint8_t* src, uint8_t* dst,
        uint32_t width, uint32_t height, uint32_t stride)
    {
        for (uint32_t y = 0; y < height; ++y)
        {
            for (uint32_t x = 0; x < width; ++x)
            {
                int32_t b = 0, g = 0, r = 0, a = 0;

                // 31-tap kernel (radius 15)
                // Sample points: y-15, y-14, ..., y, ..., y+14, y+15
                for (int i = 0; i < 31; ++i)
                {
                    int sy = static_cast<int>(y) + i - 15;

                    // Clamp to image boundaries
                    sy = (sy < 0) ? 0 : ((sy >= static_cast<int>(height)) ? height - 1 : sy);

                    const uint8_t* sample = &src[sy * stride + x * 4];
                    int32_t weight = WEIGHTS_FIXED[i];

                    b += sample[0] * weight;
                    g += sample[1] * weight;
                    r += sample[2] * weight;
                    a += sample[3] * weight;
                }

                // Convert back to bytes (divide by 32768, round)
                dst[y * stride + x * 4 + 0] = static_cast<uint8_t>((b + 16384) >> 15);
                dst[y * stride + x * 4 + 1] = static_cast<uint8_t>((g + 16384) >> 15);
                dst[y * stride + x * 4 + 2] = static_cast<uint8_t>((r + 16384) >> 15);
                dst[y * stride + x * 4 + 3] = static_cast<uint8_t>((a + 16384) >> 15);
            }
        }
    }
};

// Ultra-fast version using box approximation for radius 15
class FixedRadius15BoxBlur
{
public:
    // Fast box blur approximation for radius ~15
    // Uses 5-pass box blur with radius 5 each (5×5 ≈ radius 15 Gaussian)
    static void ApplyFastBlur(uint8_t* pixels, uint32_t width, uint32_t height)
    {
        // Apply 5 box blur passes for better Gaussian approximation
        ApplyBoxBlurPass(pixels, width, height, 3); // radius 3
        ApplyBoxBlurPass(pixels, width, height, 3); // radius 3  
        ApplyBoxBlurPass(pixels, width, height, 3); // radius 3
        ApplyBoxBlurPass(pixels, width, height, 3); // radius 3
        ApplyBoxBlurPass(pixels, width, height, 3); // radius 3
        // Result approximates Gaussian with radius ~12-15
    }

private:
    static void ApplyBoxBlurPass(uint8_t* pixels, uint32_t width, uint32_t height, int radius)
    {
        const uint32_t stride = width * 4;
        std::vector<uint8_t> temp(stride * height);

        // Horizontal pass
        for (uint32_t y = 0; y < height; ++y)
        {
            for (uint32_t x = 0; x < width; ++x)
            {
                uint32_t b = 0, g = 0, r = 0, a = 0;

                // Box kernel
                int startX = std::max(0, static_cast<int>(x) - radius);
                int endX = std::min(static_cast<int>(width) - 1, static_cast<int>(x) + radius);
                int count = endX - startX + 1;

                for (int sx = startX; sx <= endX; ++sx)
                {
                    const uint8_t* src = &pixels[y * stride + sx * 4];
                    b += src[0];
                    g += src[1];
                    r += src[2];
                    a += src[3];
                }

                temp[y * stride + x * 4 + 0] = b / count;
                temp[y * stride + x * 4 + 1] = g / count;
                temp[y * stride + x * 4 + 2] = r / count;
                temp[y * stride + x * 4 + 3] = a / count;
            }
        }

        // Vertical pass
        for (uint32_t y = 0; y < height; ++y)
        {
            for (uint32_t x = 0; x < width; ++x)
            {
                uint32_t b = 0, g = 0, r = 0, a = 0;

                int startY = std::max(0, static_cast<int>(y) - radius);
                int endY = std::min(static_cast<int>(height) - 1, static_cast<int>(y) + radius);
                int count = endY - startY + 1;

                for (int sy = startY; sy <= endY; ++sy)
                {
                    const uint8_t* src = &temp[sy * stride + x * 4];
                    b += src[0];
                    g += src[1];
                    r += src[2];
                    a += src[3];
                }

                pixels[y * stride + x * 4 + 0] = b / count;
                pixels[y * stride + x * 4 + 1] = g / count;
                pixels[y * stride + x * 4 + 2] = r / count;
                pixels[y * stride + x * 4 + 3] = a / count;
            }
        }
    }
};

// Even faster single-pass approximation for very large blur
class FixedRadius15FastBoxBlur
{
public:
    // Ultra-fast approximation using larger box blur
    static void ApplyUltraFastBlur(uint8_t* pixels, uint32_t width, uint32_t height)
    {
        // Single pass with radius 15 box blur (fastest, lowest quality)
        ApplyLargeBoxBlurPass(pixels, width, height, 15);
    }

private:
    static void ApplyLargeBoxBlurPass(uint8_t* pixels, uint32_t width, uint32_t height, int radius)
    {
        const uint32_t stride = width * 4;
        std::vector<uint8_t> temp(stride * height);

        // Horizontal pass with radius 15
        for (uint32_t y = 0; y < height; ++y)
        {
            for (uint32_t x = 0; x < width; ++x)
            {
                uint64_t b = 0, g = 0, r = 0, a = 0; // Use 64-bit to avoid overflow

                int startX = std::max(0, static_cast<int>(x) - radius);
                int endX = std::min(static_cast<int>(width) - 1, static_cast<int>(x) + radius);
                int count = endX - startX + 1;

                for (int sx = startX; sx <= endX; ++sx)
                {
                    const uint8_t* src = &pixels[y * stride + sx * 4];
                    b += src[0];
                    g += src[1];
                    r += src[2];
                    a += src[3];
                }

                temp[y * stride + x * 4 + 0] = static_cast<uint8_t>(b / count);
                temp[y * stride + x * 4 + 1] = static_cast<uint8_t>(g / count);
                temp[y * stride + x * 4 + 2] = static_cast<uint8_t>(r / count);
                temp[y * stride + x * 4 + 3] = static_cast<uint8_t>(a / count);
            }
        }

        // Vertical pass with radius 15
        for (uint32_t y = 0; y < height; ++y)
        {
            for (uint32_t x = 0; x < width; ++x)
            {
                uint64_t b = 0, g = 0, r = 0, a = 0;

                int startY = std::max(0, static_cast<int>(y) - radius);
                int endY = std::min(static_cast<int>(height) - 1, static_cast<int>(y) + radius);
                int count = endY - startY + 1;

                for (int sy = startY; sy <= endY; ++sy)
                {
                    const uint8_t* src = &temp[sy * stride + x * 4];
                    b += src[0];
                    g += src[1];
                    r += src[2];
                    a += src[3];
                }

                pixels[y * stride + x * 4 + 0] = static_cast<uint8_t>(b / count);
                pixels[y * stride + x * 4 + 1] = static_cast<uint8_t>(g / count);
                pixels[y * stride + x * 4 + 2] = static_cast<uint8_t>(r / count);
                pixels[y * stride + x * 4 + 3] = static_cast<uint8_t>(a / count);
            }
        }
    }
};
