#include "pch.h"
#include "ParticlesAnimation.h"
#if __has_include("ParticlesAnimation.g.cpp")
#include "ParticlesAnimation.g.cpp"
#endif

#include <random>
#include <algorithm>

#define IS_MOBILE false

namespace winrt::Telegram::Native::implementation
{
    inline int alpha_blend(int pixel, int sa, int sr, int sg, int sb)
    {
        if (sa == 0) return pixel;
        if (pixel == 0) return (sa << 24) | (sr << 16) | (sg << 8) | sb;

        // Alpha blend
        int destPixel = pixel;
        int da = ((destPixel >> 24) & 0xff);
        int dr = ((destPixel >> 16) & 0xff);
        int dg = ((destPixel >> 8) & 0xff);
        int db = ((destPixel) & 0xff);

        destPixel = ((sa + (((da * (255 - sa)) * 0x8081) >> 23)) << 24) |
            ((sr + (((dr * (255 - sa)) * 0x8081) >> 23)) << 16) |
            ((sg + (((dg * (255 - sa)) * 0x8081) >> 23)) << 8) |
            ((sb + (((db * (255 - sa)) * 0x8081) >> 23)));

        return destPixel;
    }

    inline Color premultiply_color(uint8_t r, uint8_t g, uint8_t b, uint8_t opacity)
    {
        // Use bit shifts for faster division (255 ≈ 256)
        uint32_t pr = (r * opacity) >> 8;
        uint32_t pg = (g * opacity) >> 8;
        uint32_t pb = (b * opacity) >> 8;

        return Color(opacity, pr, pg, pb);
    }

    // Bounds checking helper
    inline bool is_valid_pixel(int x, int y, int width, int height)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    // Safe pixel write with bounds check
    inline void set_pixel(int32_t* pixels, int x, int y, int width, int height, int sa, int sr, int sg, int sb)
    {
        if (is_valid_pixel(x, y, width, height))
        {
            if (sa == 255)
            {
                pixels[y * width + x] = (sa << 24) | (sr << 16) | (sg << 8) | sb;
            }
            else
            {
                int32_t* pixel = &pixels[y * width + x];
                *pixel = alpha_blend(*pixel, sa, sr, sg, sb);
            }
        }
    }

    // Safe pixel write with alpha blending
    inline void set_pixel_alpha(int32_t* pixels, int x, int y, int width, int height, int sa, int sr, int sg, int sb, uint8_t alpha)
    {
        if (is_valid_pixel(x, y, width, height))
        {
            uint32_t pa = (sa * alpha) >> 8;
            uint32_t pr = (sr * alpha) >> 8;
            uint32_t pg = (sg * alpha) >> 8;
            uint32_t pb = (sb * alpha) >> 8;

            int32_t* pixel = &pixels[y * width + x];
            *pixel = alpha_blend(*pixel, pa, pr, pg, pb);
        }
    }

#pragma region Circle

    // 1px diameter circles (0.5px radius)
    inline void draw_circle_1px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Single pixel
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
    }

    inline void draw_circle_1px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 1.25px effective diameter - center + light edges
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel_alpha(pixels, cx - 1, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy + 1, width, height, sa, sr, sg, sb, 64);
    }

    inline void draw_circle_1px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 1.5px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel_alpha(pixels, cx - 1, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 1, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_circle_1px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
    }

    inline void draw_circle_1px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2.5px effective diameter with anti-aliasing
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Outer ring with anti-aliasing
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_circle_1px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 4px effective diameter with anti-aliasing
        // Solid center
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb);

        // Anti-aliased outer ring
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx - 2, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 2, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 2, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 2, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy - 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy - 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy + 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy + 2, width, height, sa, sr, sg, sb, 128);

        // Corner anti-aliasing
        set_pixel_alpha(pixels, cx - 2, cy - 2, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 2, cy - 2, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx - 2, cy + 2, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 2, cy + 2, width, height, sa, sr, sg, sb, 64);
    }

    // 2px diameter circles (1px radius)
    inline void draw_circle_2px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Classic 2px diameter pattern
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
    }

    inline void draw_circle_2px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2.5px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Light anti-aliasing
        set_pixel_alpha(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb, 96);
    }

    inline void draw_circle_2px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 3px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Diagonal anti-aliasing
        set_pixel_alpha(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb, 160);
    }

    inline void draw_circle_2px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 4px effective diameter
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb);

        // Outer ring with light anti-aliasing
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_circle_2px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 5px effective diameter with good anti-aliasing
        // Solid center 3x3
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb);

        // Anti-aliased outer ring
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx - 2, cy - 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx - 2, cy + 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 2, cy - 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 2, cy + 1, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx - 1, cy - 2, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 1, cy - 2, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx - 1, cy + 2, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 1, cy + 2, width, height, sa, sr, sg, sb, 160);

        // Corner fade
        set_pixel_alpha(pixels, cx - 2, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 2, cy + 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy + 2, width, height, sa, sr, sg, sb, 96);
    }

    inline void draw_circle_2px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 8px effective diameter with full anti-aliasing
        // Solid center 5x5
        for (int dy = -2; dy <= 2; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                if (dx * dx + dy * dy <= 4)
                { // Within 2px radius
                    set_pixel(pixels, cx + dx, cy + dy, width, height, sa, sr, sg, sb);
                }
            }
        }

        // Anti-aliased outer ring (3px radius)
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 180);

        set_pixel_alpha(pixels, cx - 3, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 3, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 3, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 3, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy - 3, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy - 3, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy + 3, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy + 3, width, height, sa, sr, sg, sb, 128);

        set_pixel_alpha(pixels, cx - 3, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 3, cy + 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 3, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 3, cy + 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 2, cy - 3, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy - 3, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx - 2, cy + 3, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy + 3, width, height, sa, sr, sg, sb, 96);

        // Outer corner fade
        set_pixel_alpha(pixels, cx - 3, cy - 3, width, height, sa, sr, sg, sb, 48);
        set_pixel_alpha(pixels, cx + 3, cy - 3, width, height, sa, sr, sg, sb, 48);
        set_pixel_alpha(pixels, cx - 3, cy + 3, width, height, sa, sr, sg, sb, 48);
        set_pixel_alpha(pixels, cx + 3, cy + 3, width, height, sa, sr, sg, sb, 48);
    }

    // Dispatch function for easy usage
    inline void draw_circle_scaled(int32_t* pixels, int width, int height, Particle* particle, Color color, int scale, double rasterizationScale)
    {
        int cx = particle->X;
        int cy = particle->Y;
        float radius = particle->Radius;

        if (radius == 0.5)
        {
            switch (scale)
            {
            case 100: draw_circle_1px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_circle_1px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_circle_1px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_circle_1px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_circle_1px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_circle_1px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
        else if (radius == 1)
        {
            switch (scale)
            {
            case 100: draw_circle_2px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_circle_2px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_circle_2px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_circle_2px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_circle_2px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_circle_2px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
    }

#pragma endregion

#pragma region Plus

    // 1px diameter plus shapes (0.5px radius equivalent)
    inline void draw_plus_1px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Single pixel
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
    }

    inline void draw_plus_1px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 1.25px effective size - center + light arms
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel_alpha(pixels, cx - 1, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy + 1, width, height, sa, sr, sg, sb, 64);
    }

    inline void draw_plus_1px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 1.5px effective size
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel_alpha(pixels, cx - 1, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 1, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_plus_1px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2px effective size - solid plus
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
    }

    inline void draw_plus_1px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2.5px effective size with anti-aliasing
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Extended arms with anti-aliasing
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 96);
    }

    inline void draw_plus_1px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 4px effective size with anti-aliasing
        // Solid center cross
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Extended arms
        set_pixel(pixels, cx - 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 2, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 2, width, height, sa, sr, sg, sb);

        // Anti-aliased outer tips
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 128);
    }

    // 2px diameter plus shapes (1px radius equivalent)
    inline void draw_plus_2px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Classic 2px plus pattern
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);
    }

    inline void draw_plus_2px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 2.5px effective size with light anti-aliasing
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Light anti-aliasing on arm tips
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 64);
    }

    inline void draw_plus_2px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 3px effective size
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Medium anti-aliasing on arm tips
        set_pixel_alpha(pixels, cx - 2, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 2, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 2, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 2, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_plus_2px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 4px effective size - thicker plus
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Extended solid arms
        set_pixel(pixels, cx - 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 2, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 2, width, height, sa, sr, sg, sb);

        // Light anti-aliasing on outer tips
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 96);
    }

    inline void draw_plus_2px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 5px effective size with good anti-aliasing
        // Solid center cross
        set_pixel(pixels, cx, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 1, width, height, sa, sr, sg, sb);

        // Extended solid arms
        set_pixel(pixels, cx - 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 2, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 2, width, height, sa, sr, sg, sb);

        // Anti-aliased outer tips
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 160);
    }

    inline void draw_plus_2px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 8px effective size with full anti-aliasing
        // Solid center cross (3 pixels thick)
        for (int i = -1; i <= 1; i++)
        {
            // Horizontal arm
            set_pixel(pixels, cx - 3, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx - 2, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx - 1, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + 1, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + 2, cy + i, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + 3, cy + i, width, height, sa, sr, sg, sb);

            // Vertical arm
            set_pixel(pixels, cx + i, cy - 3, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + i, cy - 2, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + i, cy + 2, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx + i, cy + 3, width, height, sa, sr, sg, sb);
        }

        // Anti-aliased outer tips
        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 128);

        // Side anti-aliasing for smoother edges
        set_pixel_alpha(pixels, cx - 4, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx - 4, cy + 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 4, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 4, cy + 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx - 1, cy - 4, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy - 4, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx - 1, cy + 4, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy + 4, width, height, sa, sr, sg, sb, 64);
    }

    // 4px diameter star-like plus shapes (thick center, thin arms)
    inline void draw_plus_4px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Star-like 4px plus - thick center, thin arms
        // Thick center (3x3)
        for (int i = -1; i <= 1; i++)
        {
            set_pixel(pixels, cx + i, cy, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
        }

        // Thin arm extensions
        set_pixel(pixels, cx - 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 2, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 2, width, height, sa, sr, sg, sb);

        // Thin arm extensions
        set_pixel_alpha(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb, 64);
    }

    inline void draw_plus_4px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 5px effective size with tapered arms
        draw_plus_4px_100(pixels, width, height, cx, cy, sa, sr, sg, sb);

        // Tapered anti-aliasing on arm tips
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 64);
    }

    inline void draw_plus_4px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 6px effective size with gradual taper
        draw_plus_4px_100(pixels, width, height, cx, cy, sa, sr, sg, sb);

        // Medium taper on arm tips
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_plus_4px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 8px effective size - star-like with longer tapered arms
        // Thick center (3x3)
        for (int i = -1; i <= 1; i++)
        {
            set_pixel(pixels, cx + i, cy, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
        }

        // Medium thickness at arm base
        set_pixel(pixels, cx - 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 2, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 2, width, height, sa, sr, sg, sb);

        // Gradual taper with alpha
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 160);

        // Thin tips
        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 96);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 96);
    }

    inline void draw_plus_4px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 10px effective size with smooth star taper
        // Thick center (3x3)
        for (int i = -1; i <= 1; i++)
        {
            set_pixel(pixels, cx + i, cy, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
        }

        // Medium thickness at arm base
        set_pixel(pixels, cx - 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 2, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 2, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 2, width, height, sa, sr, sg, sb);

        // Smooth gradual taper
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 200);

        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 140);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 140);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 140);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 140);

        // Very thin tips
        set_pixel_alpha(pixels, cx - 5, cy, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx + 5, cy, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx, cy - 5, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx, cy + 5, width, height, sa, sr, sg, sb, 80);
    }

    inline void draw_plus_4px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 16px effective size with dramatic star taper
        // Thick center (5x5)
        for (int i = -2; i <= 2; i++)
        {
            set_pixel(pixels, cx + i, cy, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
        }

        // Add some thickness to center
        set_pixel(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb);

        // Gradual taper with multiple alpha levels
        set_pixel_alpha(pixels, cx - 3, cy, width, height, sa, sr, sg, sb, 220);
        set_pixel_alpha(pixels, cx + 3, cy, width, height, sa, sr, sg, sb, 220);
        set_pixel_alpha(pixels, cx, cy - 3, width, height, sa, sr, sg, sb, 220);
        set_pixel_alpha(pixels, cx, cy + 3, width, height, sa, sr, sg, sb, 220);

        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 180);

        set_pixel_alpha(pixels, cx - 5, cy, width, height, sa, sr, sg, sb, 140);
        set_pixel_alpha(pixels, cx + 5, cy, width, height, sa, sr, sg, sb, 140);
        set_pixel_alpha(pixels, cx, cy - 5, width, height, sa, sr, sg, sb, 140);
        set_pixel_alpha(pixels, cx, cy + 5, width, height, sa, sr, sg, sb, 140);

        set_pixel_alpha(pixels, cx - 6, cy, width, height, sa, sr, sg, sb, 100);
        set_pixel_alpha(pixels, cx + 6, cy, width, height, sa, sr, sg, sb, 100);
        set_pixel_alpha(pixels, cx, cy - 6, width, height, sa, sr, sg, sb, 100);
        set_pixel_alpha(pixels, cx, cy + 6, width, height, sa, sr, sg, sb, 100);

        // Very thin tips
        set_pixel_alpha(pixels, cx - 7, cy, width, height, sa, sr, sg, sb, 60);
        set_pixel_alpha(pixels, cx + 7, cy, width, height, sa, sr, sg, sb, 60);
        set_pixel_alpha(pixels, cx, cy - 7, width, height, sa, sr, sg, sb, 60);
        set_pixel_alpha(pixels, cx, cy + 7, width, height, sa, sr, sg, sb, 60);
    }

    // 6px diameter star-like plus shapes (thick center, thin arms)
    inline void draw_plus_6px_100(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // Star-like 6px plus - thick center, thin arms
        // Thick center (5x5)
        for (int i = -2; i <= 2; i++)
        {
            set_pixel(pixels, cx + i, cy, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
        }

        // Thin arm extensions
        set_pixel(pixels, cx - 3, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 3, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 3, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 3, width, height, sa, sr, sg, sb);

        // Thin arm extensions
        set_pixel_alpha(pixels, cx - 1, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx - 1, cy + 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy - 1, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 1, cy + 1, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_plus_6px_125(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 7.5px effective size with tapered arms
        draw_plus_6px_100(pixels, width, height, cx, cy, sa, sr, sg, sb);

        // Tapered anti-aliasing on arm tips
        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 64);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 64);
    }

    inline void draw_plus_6px_150(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 9px effective size with gradual taper
        draw_plus_6px_100(pixels, width, height, cx, cy, sa, sr, sg, sb);

        // Medium taper on arm tips
        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 128);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 128);
    }

    inline void draw_plus_6px_200(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 12px effective size - star-like with longer tapered arms
        // Thick center (5x5)
        for (int i = -2; i <= 2; i++)
        {
            set_pixel(pixels, cx + i, cy, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
        }

        // Medium thickness at arm base
        set_pixel(pixels, cx - 3, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 3, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 3, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 3, width, height, sa, sr, sg, sb);

        // Gradual taper with alpha
        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 180);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 180);

        // Thin tips
        set_pixel_alpha(pixels, cx - 5, cy, width, height, sa, sr, sg, sb, 120);
        set_pixel_alpha(pixels, cx + 5, cy, width, height, sa, sr, sg, sb, 120);
        set_pixel_alpha(pixels, cx, cy - 5, width, height, sa, sr, sg, sb, 120);
        set_pixel_alpha(pixels, cx, cy + 5, width, height, sa, sr, sg, sb, 120);

        set_pixel_alpha(pixels, cx - 6, cy, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx + 6, cy, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx, cy - 6, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx, cy + 6, width, height, sa, sr, sg, sb, 80);
    }

    inline void draw_plus_6px_250(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 15px effective size with smooth star taper
        // Thick center (5x5)
        for (int i = -2; i <= 2; i++)
        {
            set_pixel(pixels, cx + i, cy, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
        }

        // Medium thickness at arm base
        set_pixel(pixels, cx - 3, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx + 3, cy, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy - 3, width, height, sa, sr, sg, sb);
        set_pixel(pixels, cx, cy + 3, width, height, sa, sr, sg, sb);

        // Smooth gradual taper
        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 200);

        set_pixel_alpha(pixels, cx - 5, cy, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx + 5, cy, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx, cy - 5, width, height, sa, sr, sg, sb, 160);
        set_pixel_alpha(pixels, cx, cy + 5, width, height, sa, sr, sg, sb, 160);

        set_pixel_alpha(pixels, cx - 6, cy, width, height, sa, sr, sg, sb, 120);
        set_pixel_alpha(pixels, cx + 6, cy, width, height, sa, sr, sg, sb, 120);
        set_pixel_alpha(pixels, cx, cy - 6, width, height, sa, sr, sg, sb, 120);
        set_pixel_alpha(pixels, cx, cy + 6, width, height, sa, sr, sg, sb, 120);

        // Very thin tips
        set_pixel_alpha(pixels, cx - 7, cy, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx + 7, cy, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx, cy - 7, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx, cy + 7, width, height, sa, sr, sg, sb, 80);
    }

    inline void draw_plus_6px_400(int32_t* pixels, int width, int height,
        int cx, int cy, int sa, int sr, int sg, int sb)
    {
        // 24px effective size with dramatic star taper
        // Thick center (7x7)
        for (int i = -3; i <= 3; i++)
        {
            set_pixel(pixels, cx + i, cy, width, height, sa, sr, sg, sb);
            set_pixel(pixels, cx, cy + i, width, height, sa, sr, sg, sb);
        }

        // Add thickness to center
        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                set_pixel(pixels, cx + i, cy + j, width, height, sa, sr, sg, sb);
            }
        }

        // Gradual taper with multiple alpha levels
        set_pixel_alpha(pixels, cx - 4, cy, width, height, sa, sr, sg, sb, 230);
        set_pixel_alpha(pixels, cx + 4, cy, width, height, sa, sr, sg, sb, 230);
        set_pixel_alpha(pixels, cx, cy - 4, width, height, sa, sr, sg, sb, 230);
        set_pixel_alpha(pixels, cx, cy + 4, width, height, sa, sr, sg, sb, 230);

        set_pixel_alpha(pixels, cx - 5, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx + 5, cy, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy - 5, width, height, sa, sr, sg, sb, 200);
        set_pixel_alpha(pixels, cx, cy + 5, width, height, sa, sr, sg, sb, 200);

        set_pixel_alpha(pixels, cx - 6, cy, width, height, sa, sr, sg, sb, 170);
        set_pixel_alpha(pixels, cx + 6, cy, width, height, sa, sr, sg, sb, 170);
        set_pixel_alpha(pixels, cx, cy - 6, width, height, sa, sr, sg, sb, 170);
        set_pixel_alpha(pixels, cx, cy + 6, width, height, sa, sr, sg, sb, 170);

        set_pixel_alpha(pixels, cx - 7, cy, width, height, sa, sr, sg, sb, 140);
        set_pixel_alpha(pixels, cx + 7, cy, width, height, sa, sr, sg, sb, 140);
        set_pixel_alpha(pixels, cx, cy - 7, width, height, sa, sr, sg, sb, 140);
        set_pixel_alpha(pixels, cx, cy + 7, width, height, sa, sr, sg, sb, 140);

        set_pixel_alpha(pixels, cx - 8, cy, width, height, sa, sr, sg, sb, 110);
        set_pixel_alpha(pixels, cx + 8, cy, width, height, sa, sr, sg, sb, 110);
        set_pixel_alpha(pixels, cx, cy - 8, width, height, sa, sr, sg, sb, 110);
        set_pixel_alpha(pixels, cx, cy + 8, width, height, sa, sr, sg, sb, 110);

        set_pixel_alpha(pixels, cx - 9, cy, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx + 9, cy, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx, cy - 9, width, height, sa, sr, sg, sb, 80);
        set_pixel_alpha(pixels, cx, cy + 9, width, height, sa, sr, sg, sb, 80);

        // Very thin tips
        set_pixel_alpha(pixels, cx - 10, cy, width, height, sa, sr, sg, sb, 50);
        set_pixel_alpha(pixels, cx + 10, cy, width, height, sa, sr, sg, sb, 50);
        set_pixel_alpha(pixels, cx, cy - 10, width, height, sa, sr, sg, sb, 50);
        set_pixel_alpha(pixels, cx, cy + 10, width, height, sa, sr, sg, sb, 50);

        set_pixel_alpha(pixels, cx - 11, cy, width, height, sa, sr, sg, sb, 30);
        set_pixel_alpha(pixels, cx + 11, cy, width, height, sa, sr, sg, sb, 30);
        set_pixel_alpha(pixels, cx, cy - 11, width, height, sa, sr, sg, sb, 30);
        set_pixel_alpha(pixels, cx, cy + 11, width, height, sa, sr, sg, sb, 30);
    }

    // Dispatch function for easy usage
    inline void draw_plus_scaled(int32_t* pixels, int width, int height, Particle* particle, Color color, int scale, double rasterizationScale)
    {
        int cx = particle->X;
        int cy = particle->Y;
        float radius = particle->Radius;

        if (radius == 0.5)
        {
            switch (scale)
            {
            case 100: draw_plus_1px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_plus_1px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_plus_1px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_plus_1px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_plus_1px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_plus_1px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
        else if (radius == 1)
        {
            switch (scale)
            {
            case 100: draw_plus_2px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_plus_2px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_plus_2px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_plus_2px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_plus_2px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_plus_2px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
        else if (radius == 2)
        {
            switch (scale)
            {
            case 100: draw_plus_4px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_plus_4px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_plus_4px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_plus_4px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_plus_4px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_plus_4px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
        else if (radius == 3)
        {
            switch (scale)
            {
            case 100: draw_plus_6px_100(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 125: draw_plus_6px_125(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 150: draw_plus_6px_150(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 200: draw_plus_6px_200(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 250: draw_plus_6px_250(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            case 400: draw_plus_6px_400(pixels, width, height, cx, cy, color.A, color.R, color.G, color.B); break;
            }
        }
    }

#pragma endregion

    void ParticlesAnimation::RenderSync(IBuffer bitmap)
    {
        const float fps = 30;
        const float lsec = 1.2f;

        auto add = 0.04;
        auto pixels = (int32_t*)bitmap.data();

        std::fill_n(pixels, m_width * m_height, m_background);

        for (int i = 0, length = m_particles.size(); i < length; ++i)
        {
            auto dot = &m_particles[i];
            auto easedOpacity = (byte)(std::clamp(dot->Opacity, 0., 1.) * m_foreground.A);
            auto color = premultiply_color(m_foreground.R, m_foreground.G, m_foreground.B, easedOpacity);

            if (m_type == ParticlesType::Status || m_type == ParticlesType::Premium)
            {
                draw_plus_scaled(pixels, m_width, m_height, dot, color, m_scalePercent, m_rasterizationScale);
            }
            else
            {
                draw_circle_scaled(pixels, m_width, m_height, dot, color, m_scalePercent, m_rasterizationScale);
            }

            if (++dot->t >= fps * lsec)
            {
                dot->t = 0;
                ResetPoint(*dot);
            }
            UpdatePoint(*dot);
        }
    }

    inline double min(double x, double y)
    {
        return x > y ? y : x;
    }

    inline double max(double x, double y)
    {
        return x > y ? x : y;
    }

    constexpr float PI = 3.14159265358979323846f;

    std::vector<Point> ParticlesAnimation::NextPoints(int count, float width, float height, float noiseFactor)
    {
        std::random_device rd;
        std::mt19937 gen(rd());

        // Pre-calculate constants
        const float centerX = width * 0.5f;
        const float centerY = height * 0.5f;
        const float semiMajor = width * 0.5f;
        const float semiMinor = height * 0.5f;
        const float invTwoPi = 1.0f / (2.0f * PI);

        std::vector<Point> particles;
        particles.reserve(count);

        // Generate all random numbers at once for better cache performance
        std::uniform_real_distribution<float> uniformDist(0.0f, 1.0f);
        std::uniform_real_distribution<float> noiseDist(-noiseFactor, noiseFactor);

        for (int i = 0; i < count; ++i)
        {
            if (m_type == ParticlesType::Status)
            {
                // Generate angle and radius
                float angle = uniformDist(gen) * 2.0f * PI;
                float r = std::sqrt(uniformDist(gen)); // sqrt for uniform area distribution

                // Fast trigonometry
                float cosAngle = std::cos(angle);
                float sinAngle = std::sin(angle);

                // Calculate base position
                float baseX = r * semiMajor * cosAngle;
                float baseY = r * semiMinor * sinAngle;

                // Add noise
                float noiseX = noiseDist(gen) * semiMajor;
                float noiseY = noiseDist(gen) * semiMinor;

                // Final position
                float x = centerX + baseX + noiseX;
                float y = centerY + baseY + noiseY;

                // Clamp to bounds (branchless)
                x = std::max(0.0f, std::min(width, x));
                y = std::max(0.0f, std::min(height, y));

                particles.emplace_back(Point{ x, y });
            }
            else
            {
                particles.emplace_back(Point{ uniformDist(gen) * m_width, uniformDist(gen) * m_height });
            }
        }

        return particles;
    }

    Point ParticlesAnimation::NextPoint(float width, float height, float noiseFactor)
    {
        static std::random_device rd;
        static std::mt19937 gen(rd());

        if (m_type == ParticlesType::Status)
        {
            // Pre-calculate constants
            const float centerX = width * 0.5f;
            const float centerY = height * 0.5f;
            const float semiMajor = width * 0.5f;
            const float semiMinor = height * 0.5f;

            std::uniform_real_distribution<float> uniformDist(0.0f, 1.0f);
            std::uniform_real_distribution<float> noiseDist(-noiseFactor, noiseFactor);

            // Generate angle and radius
            float angle = uniformDist(gen) * 2.0f * PI;
            float r = std::sqrt(uniformDist(gen)); // sqrt for uniform area distribution

            // Fast trigonometry
            float cosAngle = std::cos(angle);
            float sinAngle = std::sin(angle);

            // Calculate base position
            float baseX = r * semiMajor * cosAngle;
            float baseY = r * semiMinor * sinAngle;

            // Add noise
            float noiseX = noiseDist(gen) * semiMajor;
            float noiseY = noiseDist(gen) * semiMinor;

            // Final position
            float x = centerX + baseX + noiseX;
            float y = centerY + baseY + noiseY;

            // Clamp to bounds
            x = std::max(0.0f, std::min(width, x));
            y = std::max(0.0f, std::min(height, y));

            return { x, y };
        }
        else
        {
            static std::uniform_real_distribution<float> dis(0.0, 1.0);
            return { dis(gen) * m_width, dis(gen) * m_height };
        }
    }

    inline double NextDouble()
    {
        static std::random_device rd;  // Will be used to obtain a seed for the random number engine
        static std::mt19937 gen(rd()); // Standard mersenne_twister_engine seeded with rd()
        static std::uniform_real_distribution<> dis(0.0, 1.0);
        return dis(gen);
    }

    void ParticlesAnimation::Prepare()
    {
        auto w = m_width * (1 / m_rasterizationScale);
        auto h = m_height * (1 / m_rasterizationScale);

        auto count = round(w * h / (35 * (IS_MOBILE ? 2 : 1)));
        count *= m_type == ParticlesType::Text ? 4 : m_type == ParticlesType::Premium ? 0.5 : 1;
        count = min(/*!liteMode.isAvailable('chat_spoilers') ? 400 :*/ IS_MOBILE ? 1000 : 2200, count);

        m_particles.reserve(count);

        auto el_w = m_width;
        auto el_h = m_height;
        const float max_d = 5;
        const float fps = 30;
        const float lsec = 1.2f;

        const auto threshold = m_type == ParticlesType::Status || m_type == ParticlesType::Premium ? .2f : .8f;
        const auto small = m_type == ParticlesType::Premium ? 2 : 0.5f;
        const auto large = m_type == ParticlesType::Premium ? 3 : 1.0f;

        for (int i = 0; i < count; i++)
        {
            Particle point;
            point.mx = el_w;
            point.my = el_h;
            point.md = max_d;
            point.cnt = count;
            point.fps = fps;
            point.lsec = lsec;
            point.t = random(0, fps * lsec);
            point.Radius = (NextDouble() >= threshold ? large : small);
            ResetPoint(point);
            UpdatePoint(point);

            m_particles.emplace_back(point);
        }
    }

    Particle ParticlesAnimation::GenerateParticle(int32_t type, const Point& position)
    {
        const auto threshold = m_type == ParticlesType::Status || m_type == ParticlesType::Premium ? .2f : .8f;
        const auto small = m_type == ParticlesType::Premium ? 2 : 0.5f;
        const auto large = m_type == ParticlesType::Premium ? 3 : 1.0f;

        auto opacity = type == 1 ? 0 : NextDouble();
        auto radius = (NextDouble() >= threshold ? large : small);
        auto adding = type == -1
            ? NextDouble() >= .5
            : type;

        auto padding = ceil(radius * m_rasterizationScale / 2);

        return Particle(
            max(padding, min(m_width - padding - 1, round(position.X))),
            max(padding, min(m_height - padding - 1, round(position.Y))),
            radius,
            opacity,
            adding);
    }

    void ParticlesAnimation::ResetPoint(Particle& particle)
    {
        auto v = GenerateVector(particle.cnt);
        particle.x = random(particle.md, particle.mx - particle.md);
        particle.y = random(particle.md, particle.my - particle.md);
        particle.dx = v.X;
        particle.dy = v.Y;
        particle.s = random(60, 80) * particle.my / 3600;
    }

    void ParticlesAnimation::UpdatePoint(Particle& particle)
    {
        float t = particle.t;
        float d = particle.fps * particle.lsec / 3;
        float k = 360 / particle.lsec / particle.fps;
        particle.X = particle.x + k * t * particle.dx;
        particle.Y = particle.y + k * t * particle.dy;
        particle.Opacity = (t < d ? (t / d) : (t < d * 2 ? 1 : (d * 3 - t) / d)) * 0.95;
    }

    Point ParticlesAnimation::GenerateVector(int count)
    {
        float speedMax = 8;
        float speedMin = 4;
        float lifetime = 600;
        float value = random(0, 2 * count + 2);
        float negative = (value < count + 1);
        float mod = (negative ? value : (value - count - 1));
        float speed = speedMin + (((speedMax - speedMin) * mod) / count);
        float max = std::ceilf(speedMax * lifetime);
        float k = speed / lifetime;
        float x = (random(0, 2 * max + 1) - max) / max;
        float y = std::sqrtf(1 - x * x) * (negative ? -1 : 1);
        return {
            k * x,
            k * y,
        };
    }

    float ParticlesAnimation::random(float x, float y)
    {
        static std::random_device rd;  // Will be used to obtain a seed for the random number engine
        static std::mt19937 gen(rd()); // Standard mersenne_twister_engine seeded with rd()
        static std::uniform_real_distribution<> dis(0.0f, 1.0f);

        return x + std::floorf(dis(gen) * (y + 1 - x));
    }
}
