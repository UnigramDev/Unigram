#pragma once

#include <memory>

#include <rlottie.h>

#include "Cache/FrameProducer.h"

namespace winrt::Telegram::Native::implementation
{
    // Drives an rlottie animation for the cache layer. The counterpart of VideoFrameProducer, and
    // the reason IFrameProducer declares random access rather than assuming it either way:
    // rlottie::Animation::renderSync takes a frame number, so a vector animation can render any
    // frame on demand, which is what keeps a sticker visible while its cache is still building.
    // ffmpeg cannot, and says so.
    class LottieFrameProducer : public Cache::IFrameProducer
    {
    public:
        LottieFrameProducer(std::unique_ptr<rlottie::Animation> animation, uint32_t width, uint32_t height)
            : m_animation(std::move(animation))
            , m_width(width)
            , m_height(height)
        {
        }

        uint32_t PixelWidth() const noexcept override { return m_width; }
        uint32_t PixelHeight() const noexcept override { return m_height; }

        float FrameRate() const noexcept override
        {
            return m_animation ? static_cast<float>(m_animation->frameRate()) : 60.0f;
        }

        uint32_t FrameCount() const noexcept override
        {
            return m_animation ? static_cast<uint32_t>(m_animation->totalFrame()) : 0;
        }

        bool SupportsRandomAccess() const noexcept override { return true; }

        bool Prepare() noexcept override
        {
            // Nothing to open: the animation is parsed when it is loaded, because the first frame
            // has to be on screen before a build is ever queued.
            return m_animation != nullptr;
        }

        void Reset() noexcept override
        {
            m_index = 0;
        }

        void Release() noexcept override
        {
            m_index = 0;
        }

        bool NextFrame(uint8_t* pixels, size_t capacity, float& timestamp) noexcept override
        {
            auto total = FrameCount();
            if (m_animation == nullptr || m_index >= total)
            {
                return false;
            }

            if (!Render(m_index, pixels, capacity))
            {
                return false;
            }

            // Evenly spaced, so the file stores no timestamps and the reader falls back to the
            // frame rate. Written anyway: it costs four bytes and makes the two backends produce
            // the same shape of file.
            auto rate = FrameRate();
            timestamp = rate > 0 ? m_index / rate : 0;

            m_index++;
            return true;
        }

        bool RenderFrame(uint32_t index, uint8_t* pixels, size_t capacity) noexcept override
        {
            return index < FrameCount() && Render(index, pixels, capacity);
        }

    private:
        bool Render(uint32_t index, uint8_t* pixels, size_t capacity) noexcept
        {
            if (pixels == nullptr || capacity < static_cast<size_t>(m_width) * m_height * 4)
            {
                return false;
            }

            try
            {
                // keepAspectRatio false: the caller has already worked the aspect into the pixel
                // size it asked for, and letting rlottie letterbox on top of that would leave
                // transparent bands where the sticker should be.
                rlottie::Surface surface(
                    reinterpret_cast<uint32_t*>(pixels),
                    m_width,
                    m_height,
                    static_cast<size_t>(m_width) * 4);

                m_animation->renderSync(index, std::move(surface), false);
                return true;
            }
            catch (...)
            {
                return false;
            }
        }

        std::unique_ptr<rlottie::Animation> m_animation;
        uint32_t m_width;
        uint32_t m_height;
        uint32_t m_index{ 0 };
    };
}
