#pragma once

#include <winrt/base.h>

#include "Cache/FrameProducer.h"
#include "VideoAnimation.h"

namespace winrt::Telegram::Native::implementation
{
    // Drives a VideoAnimation for the cache layer, and nothing else. The adapter lives here rather
    // than under Cache/ because it is the video backend's business: the shared layer knows only
    // IFrameProducer.
    //
    // Sequential by nature, which is why IFrameProducer is sequential: ffmpeg cannot seek cheaply,
    // so SupportsRandomAccess is false and the uncached fallback plays forward rather than jumping.
    class VideoFrameProducer : public Cache::IFrameProducer
    {
    public:
        VideoFrameProducer(winrt::com_ptr<VideoAnimation> animation, uint32_t width, uint32_t height)
            : m_animation(std::move(animation))
            , m_width(width)
            , m_height(height)
        {
        }

        uint32_t PixelWidth() const noexcept override { return m_width; }
        uint32_t PixelHeight() const noexcept override { return m_height; }

        float FrameRate() const noexcept override
        {
            return m_animation ? static_cast<float>(m_animation->FrameRate()) : 30.0f;
        }

        /// <summary>Unknown before the file is decoded, and never needed by a build.</summary>
        uint32_t FrameCount() const noexcept override { return 0; }

        bool SupportsRandomAccess() const noexcept override { return false; }

        bool Prepare() noexcept override
        {
            if (m_animation == nullptr)
            {
                return false;
            }

            Reset();
            return true;
        }

        void Reset() noexcept override
        {
            if (m_animation)
            {
                m_animation->SeekToMilliseconds(0, false);
            }

            m_finished = false;
        }

        void Release() noexcept override
        {
            // The decoder is shared with the animation that owns this producer, so it is rewound
            // rather than torn down: the next direct render has to start from the beginning, and
            // the build has just walked it to the end.
            Reset();
        }

        bool NextFrame(uint8_t* pixels, size_t capacity, float& timestamp) noexcept override
        {
            if (m_finished || m_animation == nullptr || pixels == nullptr)
            {
                return false;
            }

            if (capacity < static_cast<size_t>(m_width) * m_height * 4)
            {
                return false;
            }

            double seconds = 0;
            bool completed = false;

            if (!m_animation->RenderSync(pixels, m_width, m_height, false, seconds, completed))
            {
                m_finished = true;
                return false;
            }

            timestamp = static_cast<float>(seconds);

            // completed is raised on the last frame rather than after it, so this frame counts and
            // the next call ends the build. Writing one frame too many would be harmless; dropping
            // the last one would not, which is why the flag is read this way round.
            if (completed)
            {
                m_finished = true;
            }

            return true;
        }

        /// <summary>Whether the decoder has walked off the end, for the direct-render path.</summary>
        bool IsFinished() const noexcept { return m_finished; }

        void SeekToMilliseconds(int64_t milliseconds, bool precise) noexcept
        {
            if (m_animation)
            {
                m_animation->SeekToMilliseconds(milliseconds, precise);
                m_finished = false;
            }
        }

        int32_t Rotation() const noexcept
        {
            return m_animation ? m_animation->Rotation() : 0;
        }

    private:
        winrt::com_ptr<VideoAnimation> m_animation;
        uint32_t m_width;
        uint32_t m_height;
        bool m_finished{ false };
    };
}
