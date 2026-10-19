#pragma once

#include <cstdint>
#include <vector>

namespace winrt::Telegram::Native::Graphics::implementation::WalletCardTextures
{
    // RGBA, 8 bits a channel, premultiplied, made once and shared. Generated rather than shipped,
    // because they are fixed by the numbers that make them.
    struct Texture
    {
        uint32_t Width;
        uint32_t Height;
        std::vector<uint8_t> Pixels;
    };

    // 20 eight-point stars at 3x the card's 361 x 220 points. Red and alpha are the coverage and
    // green is the coverage times the star's twinkle phase, so the shader reads the phase as g / a.
    Texture const& Stars();

    // 256 x 256 grain, the same in every channel: white noise blurred along x, then contrasted
    // and mixed with fresh noise. Sampled wrapping.
    Texture const& Noise();

    // The QR glyph on the chip, 22 x 22 points at 8x, in alpha.
    Texture const& Qr();
}
