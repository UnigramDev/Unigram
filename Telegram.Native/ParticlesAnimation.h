#pragma once

#include "ParticlesAnimation.g.h"

using namespace winrt::Windows::Storage::Streams;
using namespace winrt::Windows::UI::Xaml::Media::Imaging;

namespace winrt::Telegram::Native::implementation
{
    struct Particle
    {
        Particle(float x, float y, float radius, double opacity, bool adding)
            : X(x)
            , Y(y)
            , Radius(radius)
            , Opacity(opacity)
            , Adding(adding)
        {
        }

        float X;
        float Y;
        float Radius;
        double Opacity;
        bool Adding;
    };

    struct ParticlesAnimation : ParticlesAnimationT<ParticlesAnimation>
    {
        ParticlesAnimation(int32_t width, int32_t height, double rasterizationScale)
            : m_width(width)
            , m_height(height)
            , m_rasterizationScale(rasterizationScale)
        {
            Prepare();
        }

        void RenderSync(IBuffer bitmap);

        int32_t PixelWidth()
        {
            return m_width;
        }

        int32_t PixelHeight()
        {
            return m_height;
        }

    private:
        void Prepare();
        Particle GenerateParticle(int32_t type);

        int32_t m_width;
        int32_t m_height;
        double m_rasterizationScale;

        std::vector<Particle> m_particles;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct ParticlesAnimation : ParticlesAnimationT<ParticlesAnimation, implementation::ParticlesAnimation>
    {
    };
}
