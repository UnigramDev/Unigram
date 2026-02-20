#pragma once

#include "LokiRng.g.h"

namespace winrt::Telegram::Native::implementation
{
    struct LokiRng : LokiRngT<LokiRng>
    {
        LokiRng(uint32_t seed0, uint32_t seed1, uint32_t seed2);

        float Next();

        static float Random(uint32_t withSeed0, uint32_t seed1, uint32_t seed2);

    private:
        float m_seed;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct LokiRng : LokiRngT<LokiRng, implementation::LokiRng>
    {
    };
}
