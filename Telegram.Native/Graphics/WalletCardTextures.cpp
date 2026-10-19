#include "pch.h"
#include "WalletCardTextures.h"

#include <algorithm>
#include <cmath>

namespace winrt::Telegram::Native::Graphics::implementation::WalletCardTextures
{
    namespace
    {
        // The stars are laid out on a 370 point wide card and drawn into a 361 point wide texture.
        constexpr float TextureWidth = 361.0f;
        constexpr float TextureHeight = 220.0f;
        constexpr float SourceWidth = 370.0f;
        constexpr int StarsScale = 3;
        constexpr int QrScale = 8;

        void Put(Texture& texture, uint32_t x, uint32_t y, float r, float g, float b, float a)
        {
            auto pixel = texture.Pixels.data() + (static_cast<size_t>(y) * texture.Width + x) * 4;
            pixel[0] = static_cast<uint8_t>(std::clamp(r, 0.0f, 1.0f) * 255.0f + 0.5f);
            pixel[1] = static_cast<uint8_t>(std::clamp(g, 0.0f, 1.0f) * 255.0f + 0.5f);
            pixel[2] = static_cast<uint8_t>(std::clamp(b, 0.0f, 1.0f) * 255.0f + 0.5f);
            pixel[3] = static_cast<uint8_t>(std::clamp(a, 0.0f, 1.0f) * 255.0f + 0.5f);
        }

        bool InsidePolygon(float x, float y, float const* points, int count)
        {
            bool inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float ax = points[i * 2], ay = points[i * 2 + 1];
                float bx = points[j * 2], by = points[j * 2 + 1];
                if (((ay > y) != (by > y)) && (x < (bx - ax) * (y - ay) / (by - ay) + ax))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        Texture MakeStars()
        {
            static constexpr float centers[20][2] =
            {
                { 87.12f, 47.0f }, { 312.6f, 156.0f }, { 29.72f, 22.0f }, { 249.06f, 195.0f }, { 121.97f, 62.0f },
                { 227.53f, 138.0f }, { 248.03f, 164.0f }, { 69.69f, 20.0f }, { 127.09f, 36.0f }, { 56.37f, 67.0f },
                { 177.0f, 24.0f }, { 219.0f, 53.0f }, { 284.0f, 28.0f }, { 338.0f, 48.0f }, { 330.0f, 112.0f },
                { 292.0f, 132.0f }, { 181.0f, 151.0f }, { 142.0f, 172.0f }, { 47.0f, 145.0f }, { 95.0f, 112.0f },
            };

            static constexpr float phases[20] =
            {
                0.05f, 0.63f, 0.22f, 0.87f, 0.42f, 0.12f, 0.74f, 0.33f, 0.55f, 0.94f,
                0.18f, 0.69f, 0.38f, 0.81f, 0.27f, 0.58f, 0.96f, 0.47f, 0.07f, 0.76f,
            };

            Texture texture;
            texture.Width = static_cast<uint32_t>(TextureWidth * StarsScale);
            texture.Height = static_cast<uint32_t>(TextureHeight * StarsScale);
            texture.Pixels.assign(static_cast<size_t>(texture.Width) * texture.Height * 4, 0);

            float scaleX = TextureWidth / SourceWidth;
            float radiusScale = std::min(scaleX, 1.0f) * StarsScale;
            float outer = 4.05f * radiusScale;
            float inner = 1.215f * radiusScale;

            for (int star = 0; star < 20; star++)
            {
                float cx = centers[star][0] * scaleX * StarsScale;
                float cy = centers[star][1] * StarsScale;

                float points[16];
                for (int i = 0; i < 8; i++)
                {
                    float radius = i % 2 == 0 ? outer : inner;
                    float angle = -3.14159265f / 2.0f + i * 3.14159265f / 4.0f;
                    points[i * 2] = cx + radius * std::cos(angle);
                    points[i * 2 + 1] = cy + radius * std::sin(angle);
                }

                auto left = static_cast<int>(std::floor(cx - outer)), right = static_cast<int>(std::ceil(cx + outer));
                auto top = static_cast<int>(std::floor(cy - outer)), bottom = static_cast<int>(std::ceil(cy + outer));

                for (int y = std::max(top, 0); y < std::min(bottom, static_cast<int>(texture.Height)); y++)
                {
                    for (int x = std::max(left, 0); x < std::min(right, static_cast<int>(texture.Width)); x++)
                    {
                        int hits = 0;
                        for (int sy = 0; sy < 4; sy++)
                        {
                            for (int sx = 0; sx < 4; sx++)
                            {
                                hits += InsidePolygon(x + (sx + 0.5f) / 4, y + (sy + 0.5f) / 4, points, 8) ? 1 : 0;
                            }
                        }

                        if (hits > 0)
                        {
                            float coverage = hits / 16.0f;
                            Put(texture, x, y, coverage, phases[star] * coverage, 0, coverage);
                        }
                    }
                }
            }

            return texture;
        }

        Texture MakeNoise()
        {
            constexpr int dimension = 256;
            constexpr int radius = 9;
            uint64_t seed = 0x2545F4914F6CDD1Dull;

            auto random = [&seed]
            {
                seed ^= seed << 13;
                seed ^= seed >> 7;
                seed ^= seed << 17;
                return static_cast<float>(seed & 0xffff) / static_cast<float>(0xffff);
            };

            std::vector<float> base(dimension * dimension);
            for (auto& value : base)
            {
                value = random();
            }

            Texture texture;
            texture.Width = dimension;
            texture.Height = dimension;
            texture.Pixels.assign(static_cast<size_t>(dimension) * dimension * 4, 0);

            for (int y = 0; y < dimension; y++)
            {
                for (int x = 0; x < dimension; x++)
                {
                    float accumulator = 0;
                    for (int offset = -radius; offset <= radius; offset++)
                    {
                        accumulator += base[y * dimension + (x + offset + dimension) % dimension];
                    }

                    float value = accumulator / (2 * radius + 1);
                    value = 0.5f + (value - 0.5f) * 3.4f;
                    float combined = 0.68f * value + 0.32f * random();

                    // Truncated rather than rounded, as the grain was designed.
                    auto byte = static_cast<uint8_t>(std::clamp(combined * 255.0f, 0.0f, 255.0f));
                    auto pixel = texture.Pixels.data() + (static_cast<size_t>(y) * dimension + x) * 4;
                    pixel[0] = pixel[1] = pixel[2] = byte;
                    pixel[3] = 255;
                }
            }

            return texture;
        }

        float RoundRect(float x, float y, float left, float top, float width, float height, float radius)
        {
            float qx = std::abs(x - (left + width / 2)) - (width / 2 - radius);
            float qy = std::abs(y - (top + height / 2)) - (height / 2 - radius);
            float outside = std::sqrt(std::max(qx, 0.0f) * std::max(qx, 0.0f) + std::max(qy, 0.0f) * std::max(qy, 0.0f));
            return outside + std::min(std::max(qx, qy), 0.0f) - radius;
        }

        float Coverage(float distance)
        {
            return std::clamp(0.5f - distance * QrScale, 0.0f, 1.0f);
        }

        Texture MakeQr()
        {
            static constexpr float finders[3][2] = { { 284.25f, 91.75f }, { 294.5f, 91.75f }, { 284.25f, 102.0f } };
            static constexpr float dots[5][2] = { { 295.0f, 103.0f }, { 300.12f, 103.0f }, { 297.6f, 105.3f }, { 295.0f, 108.15f }, { 300.12f, 108.15f } };

            Texture texture;
            texture.Width = 22 * QrScale;
            texture.Height = 22 * QrScale;
            texture.Pixels.assign(static_cast<size_t>(texture.Width) * texture.Height * 4, 0);

            for (uint32_t py = 0; py < texture.Height; py++)
            {
                for (uint32_t px = 0; px < texture.Width; px++)
                {
                    float x = (px + 0.5f) / QrScale + 282.5f;
                    float y = (py + 0.5f) / QrScale + 90.0f;
                    float coverage = 0;

                    for (auto const& finder : finders)
                    {
                        float outer = Coverage(RoundRect(x, y, finder[0], finder[1], 8.25f, 8.25f, 2.4f));
                        float inner = Coverage(RoundRect(x, y, finder[0] + 1.375f, finder[1] + 1.375f, 5.5f, 5.5f, 1.1f));
                        coverage = std::max(coverage, outer * (1 - inner));
                    }

                    for (auto const& dot : dots)
                    {
                        coverage = std::max(coverage, Coverage(RoundRect(x, y, dot[0], dot[1], 2.12f, 2.1f, 0.45f)));
                    }

                    if (coverage > 0)
                    {
                        Put(texture, px, py, coverage, coverage, coverage, coverage);
                    }
                }
            }

            return texture;
        }
    }

    Texture const& Stars()
    {
        static Texture const texture = MakeStars();
        return texture;
    }

    Texture const& Noise()
    {
        static Texture const texture = MakeNoise();
        return texture;
    }

    Texture const& Qr()
    {
        static Texture const texture = MakeQr();
        return texture;
    }
}
