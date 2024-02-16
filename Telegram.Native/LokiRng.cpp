#include "pch.h"
#include "LokiRng.h"
#if __has_include("LokiRng.g.cpp")
#include "LokiRng.g.cpp"
#endif

namespace winrt::Telegram::Native::implementation
{
    inline static uint32_t tausStep(const uint32_t z, const int32_t s1, const int32_t s2, const int32_t s3, const uint32_t M)
    {
        uint32_t b = (((z << s1) ^ z) >> s2);
        return (((z & M) << s3) ^ b);
    }

    LokiRng::LokiRng(uint32_t seed0, uint32_t seed1, uint32_t seed2)
    {
        uint32_t seed = ((uint32_t)seed0) * 1099087573U;
        uint32_t seedb = ((uint32_t)seed1) * 1099087573U;
        uint32_t seedc = ((uint32_t)seed2) * 1099087573U;

        // Round 1: Randomise seed
        uint32_t z1 = tausStep(seed, 13, 19, 12, 429496729U);
        uint32_t z2 = tausStep(seed, 2, 25, 4, 4294967288U);
        uint32_t z3 = tausStep(seed, 3, 11, 17, 429496280U);
        uint32_t z4 = (1664525 * seed + 1013904223U);

        // Round 2: Randomise seed again using second seed
        uint32_t r1 = (z1 ^ z2 ^ z3 ^ z4 ^ seedb);

        z1 = tausStep(r1, 13, 19, 12, 429496729U);
        z2 = tausStep(r1, 2, 25, 4, 4294967288U);
        z3 = tausStep(r1, 3, 11, 17, 429496280U);
        z4 = (1664525 * r1 + 1013904223U);

        // Round 3: Randomise seed again using third seed
        r1 = (z1 ^ z2 ^ z3 ^ z4 ^ seedc);

        z1 = tausStep(r1, 13, 19, 12, 429496729U);
        z2 = tausStep(r1, 2, 25, 4, 4294967288U);
        z3 = tausStep(r1, 3, 11, 17, 429496280U);
        z4 = (1664525 * r1 + 1013904223U);

        m_seed = (z1 ^ z2 ^ z3 ^ z4) * 2.3283064365387e-10f;
    }

    float LokiRng::Next()
    {
        uint32_t hashed_seed = m_seed * 1099087573U;

        uint32_t z1 = tausStep(hashed_seed, 13, 19, 12, 429496729U);
        uint32_t z2 = tausStep(hashed_seed, 2, 25, 4, 4294967288U);
        uint32_t z3 = tausStep(hashed_seed, 3, 11, 17, 429496280U);
        uint32_t z4 = (1664525 * hashed_seed + 1013904223U);

        float old_seed = m_seed;
        m_seed = (z1 ^ z2 ^ z3 ^ z4) * 2.3283064365387e-10f;

        return old_seed;
    }
}
