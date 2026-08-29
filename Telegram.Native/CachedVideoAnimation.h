#pragma once

#include "CachedVideoAnimation.g.h"

#include <atomic>
#include <memory>
#include <string>
#include <vector>

#include "Cache/FrameCacheReader.h"
#include "VideoAnimation.h"
#include "VideoFrameProducer.h"

using namespace winrt::Windows::Storage::Streams;

namespace winrt::Telegram::Native::implementation
{
    // A video sticker, played from a cache file when there is one and decoded directly when there
    // is not. Everything about building, compressing and storing that file now lives in
    // Cache/FrameCacheService, shared with every other animation kind - this class holds a reader,
    // a producer and a frame index, and nothing else.
    //
    // What used to be here and is gone: a private compress queue and worker thread, the LZ4 calls,
    // the frame offset and timing tables, the per-instance decompression buffer, the per-key lock,
    // and the partial-file handling. The lock is unnecessary because a cache file that exists is
    // complete; the buffer is unnecessary because scratch belongs to the service.
    struct CachedVideoAnimation : CachedVideoAnimationT<CachedVideoAnimation>
    {
        CachedVideoAnimation() = default;

        virtual ~CachedVideoAnimation()
        {
            Close();
        }

        void Close()
        {
            m_reader.Close();
            m_scratch.clear();
            m_scratch.shrink_to_fit();

            // Only this animation's reference. A build in flight holds its own, and the producer
            // goes when that ends - never underneath it.
            m_producer = nullptr;
            m_file = nullptr;
        }

        static winrt::Telegram::Native::CachedVideoAnimation LoadFromFile(IVideoAnimationSource file, int32_t width, int32_t height, bool fit, bool precache, bool limitFps);

        void RenderSync(IBuffer bitmap, double& seconds, bool& completed);
        void Stop();

        void Seek(double seconds);

        double FrameRate();

        int32_t TotalFrame();

        bool IsCaching();

        int PixelWidth()
        {
            return m_pixelWidth;
        }

        int PixelHeight()
        {
            return m_pixelHeight;
        }

        int Rotation()
        {
            return m_rotation;
        }

    private:
        bool Load(IVideoAnimationSource file, int32_t width, int32_t height, bool fit, bool limitFps);

        /// <summary>
        /// Opens the decoder, if it is not open already. Deferred because a cached animation never
        /// needs one: the header carries everything opening the file would have told us, and
        /// ffmpeg's open-probe-find_stream_info-open_codec was being paid by every cache hit.
        /// </summary>
        bool EnsureProducer();
        void RenderSync(uint8_t* pixels, double& seconds, bool& completed, bool* rendered);

        /// <summary>Queues this animation's own build, once, after it has decoded a frame itself.</summary>
        void RequestCache();

        std::shared_ptr<VideoFrameProducer> m_producer;
        Cache::FrameCacheReader m_reader;

        // Held so a decoder can still be opened later - after a cache hit that turns out to be
        // unreadable, or when caching is off. Opening it up front is what this class stopped doing.
        IVideoAnimationSource m_file{ nullptr };
        int32_t m_requestedWidth{ 0 };
        int32_t m_requestedHeight{ 0 };
        bool m_fit{ false };
        bool m_limitFps{ false };

        // Sized from the reader the first time a frame is read, so an animation that never reads
        // from a cache never allocates it.
        std::vector<uint8_t> m_scratch;

        std::wstring m_cachePath;

        uint32_t m_frameIndex{ 0 };
        // Double, not int: 29.97 and 23.976 are ordinary video frame rates and truncating
        // them costs a frame every few seconds.
        double m_fps{ 30 };
        int32_t m_pixelWidth{ 0 };
        int32_t m_pixelHeight{ 0 };
        int32_t m_rotation{ 0 };
        bool m_precache{ false };

        // Set while this animation's build is queued or running; cleared by the service. An atomic
        // load rather than asking the service, which took a global mutex per animation per frame.
        std::shared_ptr<std::atomic<bool>> m_building;

        // The direct path has just wrapped, so a cache can be adopted without the picture
        // jumping backwards.
        bool m_atLoopBoundary{ false };
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct CachedVideoAnimation : CachedVideoAnimationT<CachedVideoAnimation, implementation::CachedVideoAnimation>
    {
    };
}
