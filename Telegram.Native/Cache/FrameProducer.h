#pragma once

#include <cstdint>

namespace winrt::Telegram::Native::Cache
{
    // What a backend has to be able to do for the cache layer to build a file out of it. Everything
    // else - the format, the codec, the queue, the buffers, the file protocol - is shared.
    //
    // The contract is deliberately **sequential**. An index-based RenderFrame fits a vector
    // animation and lies about ffmpeg, which cannot seek cheaply; a producer that only ever goes
    // forward is honest for both, and random access is a property of the finished cache file's
    // index rather than of the backend. Android's BitmapsCache.Cacheable has the same shape for the
    // same reason.
    //
    // The asymmetry that survives is the uncached fallback: a vector animation can render an
    // arbitrary frame on demand while the cache builds, a video cannot. That is what
    // SupportsRandomAccess is for - not for the build, which never needs it.
    struct IFrameProducer
    {
        virtual ~IFrameProducer() = default;

        virtual uint32_t PixelWidth() const noexcept = 0;
        virtual uint32_t PixelHeight() const noexcept = 0;
        virtual float FrameRate() const noexcept = 0;

        /// <summary>Zero when the backend cannot know before producing them, as video cannot.</summary>
        virtual uint32_t FrameCount() const noexcept = 0;

        /// <summary>
        /// Degrees the frames want turning by when drawn. Stored in the cache file, because a
        /// backend that is never opened cannot be asked.
        /// </summary>
        virtual int32_t Rotation() const noexcept
        {
            return 0;
        }

        virtual bool SupportsRandomAccess() const noexcept = 0;

        /// <summary>
        /// Called once before the first <see cref="NextFrame"/> of a build, so a backend can open
        /// whatever it needs only when a build actually starts rather than when it is queued.
        /// </summary>
        virtual bool Prepare() noexcept = 0;

        /// <returns>False at the end of the animation, which is how a build knows it is done.</returns>
        virtual bool NextFrame(uint8_t* pixels, size_t capacity, float& timestamp) noexcept = 0;

        virtual void Reset() noexcept = 0;

        /// <summary>Releases whatever <see cref="Prepare"/> took. Always called, build or no build.</summary>
        virtual void Release() noexcept = 0;

        /// <summary>
        /// Only meaningful when <see cref="SupportsRandomAccess"/>. Serves the uncached fallback -
        /// the frame shown while the cache is still building - and nothing else.
        /// </summary>
        virtual bool RenderFrame(uint32_t /*index*/, uint8_t* /*pixels*/, size_t /*capacity*/) noexcept
        {
            return false;
        }
    };
}
