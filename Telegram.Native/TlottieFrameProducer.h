#pragma once

#ifdef HAS_TLOTTIE

#include <cstdint>

#include <tlottie.h>

#include "Cache/FrameProducer.h"

namespace winrt::Telegram::Native::implementation
{
    // The tlottie counterpart of LottieFrameProducer. Same contract, same pixels, and that second
    // part is load-bearing:
    //
    // **tlottie must be constructed with TLOTTIE_CHANNEL_BGRA.** Its default is
    // TLOTTIE_CHANNEL_RGBA - 0xAABBGGRR words, [R, G, B, A] bytes - which would put red where blue
    // belongs. rlottie always produces BGRA, and both renderers now write to the *same* cache file,
    // so getting this wrong no longer means one wrong-looking frame: it means channel-swapped
    // frames persisted to disk and then served to the other renderer. The order is chosen once at
    // parse time by pre-swapping the model's colours, so it costs nothing per frame and there is no
    // reason to leave it to the default.
    //
    // The instance arrives already parsed, as rlottie's does, because the first frame has to be on
    // screen before a cache build is ever queued.
    class TlottieFrameProducer : public Cache::IFrameProducer
    {
    public:
        TlottieFrameProducer(TLottieInstance* instance, uint32_t width, uint32_t height)
            : m_instance(instance)
            , m_width(width)
            , m_height(height)
        {
        }

        ~TlottieFrameProducer() override
        {
            if (m_instance != nullptr)
            {
                tlottie_drop(m_instance);
                m_instance = nullptr;
            }
        }

        TlottieFrameProducer(const TlottieFrameProducer&) = delete;
        TlottieFrameProducer& operator=(const TlottieFrameProducer&) = delete;

        uint32_t PixelWidth() const noexcept override { return m_width; }
        uint32_t PixelHeight() const noexcept override { return m_height; }

        float FrameRate() const noexcept override
        {
            return m_instance ? tlottie_frame_rate(m_instance) : 60.0f;
        }

        uint32_t FrameCount() const noexcept override
        {
            return m_instance ? tlottie_frame_count(m_instance) : 0;
        }

        bool SupportsRandomAccess() const noexcept override { return true; }

        bool Prepare() noexcept override { return m_instance != nullptr; }

        void Reset() noexcept override { m_index = 0; }

        void Release() noexcept override { m_index = 0; }

        bool NextFrame(uint8_t* pixels, size_t capacity, float& timestamp) noexcept override
        {
            auto total = FrameCount();
            if (m_instance == nullptr || m_index >= total || !Render(m_index, pixels, capacity))
            {
                return false;
            }

            auto rate = FrameRate();
            timestamp = rate > 0 ? m_index / rate : 0;

            m_index++;
            return true;
        }

        bool RenderFrame(uint32_t index, uint8_t* pixels, size_t capacity, bool clear = true) noexcept override
        {
            return index < FrameCount() && Render(index, pixels, capacity, clear);
        }

    private:
        // RenderOptions::default().curve_tolerance, which is what tlottie_render passes and the
        // header does not name. Needed because compositing has to go through
        // tlottie_render_with_options, and that takes the tolerance explicitly - so the two paths
        // would otherwise flatten curves differently.
        static constexpr float CurveTolerance = 0.125f;

        bool Render(uint32_t index, uint8_t* pixels, size_t capacity, bool clear = true) noexcept
        {
            auto required = static_cast<size_t>(m_width) * m_height * 4;
            if (pixels == nullptr || capacity < required)
            {
                return false;
            }

            auto status = clear
                ? tlottie_render(
                    m_instance,
                    static_cast<float>(index),
                    m_width,
                    m_height,
                    reinterpret_cast<uint32_t*>(pixels),
                    static_cast<size_t>(m_width) * m_height,
                    1)
                : tlottie_render_with_options(
                    m_instance,
                    static_cast<float>(index),
                    m_width,
                    m_height,
                    reinterpret_cast<uint32_t*>(pixels),
                    static_cast<size_t>(m_width) * m_height,
                    1,
                    CurveTolerance,
                    0);

            return status == TLOTTIE_OK;
        }

        TLottieInstance* m_instance;
        uint32_t m_width;
        uint32_t m_height;
        uint32_t m_index{ 0 };
    };
}

#endif
