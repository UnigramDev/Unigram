#include "pch.h"
#include "CachedVideoAnimation.h"
#if __has_include("CachedVideoAnimation.g.cpp")
#include "CachedVideoAnimation.g.cpp"
#endif

#include <algorithm>

#include "Cache/FrameCacheService.h"

extern "C"
{
#include <libavcodec/avcodec.h>
}

namespace winrt::Telegram::Native::implementation
{
    namespace
    {
        // Everything that changes the pixels goes in the name, and nothing that does not. Playback
        // policy - autoplay, loop count, whether it sits in a popup - belongs to the presenter, not
        // to the file.
        //
        // The extension is new because the format is. Old .cache files are simply never opened
        // again and age out with the rest of the cache, so there is no migration to get wrong.
        std::wstring BuildCachePath(const winrt::hstring& path, int32_t width, int32_t height, bool fit)
        {
            std::wstring result(path);

            result += L".";
            result += std::to_wstring(width);
            result += L"x";
            result += std::to_wstring(height);

            if (fit)
            {
                result += L".fit";
            }

            result += L".tgfc";
            return result;
        }
    }

    winrt::Telegram::Native::CachedVideoAnimation CachedVideoAnimation::LoadFromFile(IVideoAnimationSource file, int32_t width, int32_t height, bool fit, bool precache, bool limitFps)
    {
        auto info = winrt::make_self<CachedVideoAnimation>();

        info->m_file = file;
        info->m_requestedWidth = width;
        info->m_requestedHeight = height;
        info->m_fit = fit;
        info->m_limitFps = limitFps;

        auto path = file.FilePath();

        if (precache && path.size())
        {
            info->m_precache = true;
            info->m_cachePath = BuildCachePath(path, width, height, fit);

            // No lock and no validation beyond the header: a file that opens is a file that was
            // renamed into place complete.
            //
            // Tried before the decoder, and this is the whole point of keying on the requested
            // size rather than the decoded one. The two are equally precise - the decoded size is
            // a pure function of the file and the request - but only one of them can be known
            // without opening the file, and a cache hit that has to open the file to find its own
            // name has already paid the cost it was avoiding.
            if (info->m_reader.Open(info->m_cachePath))
            {
                info->m_pixelWidth = info->m_reader.Width();
                info->m_pixelHeight = info->m_reader.Height();
                info->m_fps = info->m_reader.FrameRate();
                info->m_rotation = info->m_reader.Rotation();

                return info.as<winrt::Telegram::Native::CachedVideoAnimation>();
            }
        }

        if (!info->EnsureProducer())
        {
            return nullptr;
        }

        return info.as<winrt::Telegram::Native::CachedVideoAnimation>();
    }

    bool CachedVideoAnimation::EnsureProducer()
    {
        if (m_producer)
        {
            return true;
        }

        if (m_file == nullptr)
        {
            return false;
        }

        // What the caller was told at load time, and sized its bitmap from.
        auto width = m_pixelWidth;
        auto height = m_pixelHeight;

        m_file.SeekCallback(0);

        if (!Load(m_file, m_requestedWidth, m_requestedHeight, m_fit, m_limitFps))
        {
            return false;
        }

        // Only reachable when a cache file was adopted and then failed to read: the decoder should
        // agree with the header, and if it does not, the frames it produces do not fit the buffer
        // the caller allocated. Stopping is survivable, overrunning it is not.
        if (width != 0 && (m_pixelWidth != width || m_pixelHeight != height))
        {
            m_pixelWidth = width;
            m_pixelHeight = height;
            m_producer = nullptr;

            return false;
        }

        return true;
    }

    bool CachedVideoAnimation::Load(IVideoAnimationSource file, int32_t width, int32_t height, bool fit, bool limitFps)
    {
        auto animation = VideoAnimation::LoadFromFile(file, false, limitFps, false).as<VideoAnimation>();
        if (animation == nullptr)
        {
            return false;
        }

        auto pixelWidth = animation->PixelWidth();
        auto pixelHeight = animation->PixelHeight();

        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return false;
        }

        if (width > 0 && height > 0)
        {
            double ratioX = (double)width / pixelWidth;
            double ratioY = (double)height / pixelHeight;
            double ratio = fit ? std::min(ratioX, ratioY) : std::max(ratioX, ratioY);

            pixelWidth = (int)(pixelWidth * ratio);
            pixelHeight = (int)(pixelHeight * ratio);
        }

        auto widthalign = AV_INPUT_BUFFER_PADDING_SIZE / 4;
        auto neededWidth = pixelWidth + ((pixelWidth % widthalign) ? (widthalign - (pixelWidth % widthalign)) : 0);

        m_pixelWidth = neededWidth;
        m_pixelHeight = (int)((double)neededWidth / pixelWidth * pixelHeight);
        m_fps = animation->FrameRate();

        m_producer = std::make_shared<VideoFrameProducer>(animation, m_pixelWidth, m_pixelHeight);
        m_rotation = m_producer->Rotation();

        return true;
    }

    void CachedVideoAnimation::Stop()
    {
        if (m_producer)
        {
            m_producer->SeekToMilliseconds(0, false);
        }

        m_frameIndex = 0;
    }

    void CachedVideoAnimation::Seek(double seconds)
    {
        if (m_producer)
        {
            m_producer->SeekToMilliseconds((int64_t)(seconds * 1000), true);
        }
    }

    void CachedVideoAnimation::RenderSync(IBuffer bitmap, double& seconds, bool& completed)
    {
        uint8_t* pixels = bitmap.data();
        bool rendered;
        RenderSync(pixels, seconds, completed, &rendered);
    }

    void CachedVideoAnimation::RenderSync(uint8_t* pixels, double& seconds, bool& completed, bool* rendered)
    {
        if (rendered)
        {
            *rendered = false;
        }

        if (pixels == nullptr)
        {
            return;
        }

        // While this animation's own cache is being built, the build owns the decoder: it is
        // walking the producer to the end, and a direct render would fight it for position. The
        // presenter sits on the last frame it got, which is what it did before this rewrite too.
        //
        // Note this is only ever *this* animation's build. Another animation reading a finished
        // cache is never blocked by anyone.
        if (IsCaching() && !m_reader.IsOpen())
        {
            return;
        }

        // Adopted only at a loop boundary. The reader restarts at frame 0, so switching
        // part-way would jump the picture backwards; waiting for the wrap makes it invisible.
        if (m_building && !m_building->load(std::memory_order_relaxed) && m_atLoopBoundary && !m_reader.IsOpen())
        {
            m_building = nullptr;
            m_frameIndex = 0;
            m_reader.Open(m_cachePath, m_pixelWidth, m_pixelHeight);
        }

        if (m_reader.IsOpen())
        {
            auto size = static_cast<size_t>(m_pixelWidth) * m_pixelHeight * 4;

            if (m_scratch.size() < m_reader.MaxCompressedSize())
            {
                m_scratch.resize(m_reader.MaxCompressedSize());
            }

            if (m_reader.ReadFrame(m_frameIndex, pixels, size, m_scratch.data(), m_scratch.size()))
            {
                seconds = m_reader.Timestamp(m_frameIndex);

                if (rendered)
                {
                    *rendered = true;
                }

                m_frameIndex++;

                if (m_frameIndex >= m_reader.FrameCount())
                {
                    m_frameIndex = 0;
                    completed = true;
                }
                else
                {
                    completed = false;
                }

                return;
            }

            // A cache file that stops reading is a cache file we should not be holding. Dropping
            // it falls through to the decoder rather than freezing the animation.
            m_reader.Close();
        }

        // Opened here rather than at load, so an animation served entirely from its cache never
        // touches ffmpeg at all.
        if (!EnsureProducer())
        {
            return;
        }

        float timestamp = 0;
        auto size = static_cast<size_t>(m_pixelWidth) * m_pixelHeight * 4;

        if (m_producer->NextFrame(pixels, size, timestamp))
        {
            seconds = timestamp;
            completed = m_producer->IsFinished();

            // The producer latches at the end so a cache build knows to stop. Playback shares
            // that path, so the latch has to be cleared here or the animation renders nothing
            // ever again: one dead frame, then a stale one, then it stops.
            if (completed)
            {
                m_producer->Reset();
                m_atLoopBoundary = true;
            }

            if (rendered)
            {
                *rendered = true;
            }

            // Queued here rather than reported to the caller and queued back on the next tick.
            // The frame it needed to show first is already in the buffer.
            RequestCache();
        }
        else
        {
            completed = true;
        }
    }

    void CachedVideoAnimation::RequestCache()
    {
        if (!m_precache || m_producer == nullptr || m_cachePath.empty() || m_building)
        {
            return;
        }

        // Null when refused; the first caller's token when this key is already queued.
        m_building = Cache::FrameCacheService::Instance().Enqueue(
            m_cachePath, std::weak_ptr<Cache::IFrameProducer>(m_producer));
    }

    bool CachedVideoAnimation::IsCaching()
    {
        return m_building && m_building->load(std::memory_order_relaxed);
    }

    double CachedVideoAnimation::FrameRate()
    {
        if (m_reader.IsOpen() && m_reader.FrameRate() > 0)
        {
            return m_reader.FrameRate();
        }

        return m_fps;
    }

    int32_t CachedVideoAnimation::TotalFrame()
    {
        return m_reader.IsOpen() ? static_cast<int32_t>(m_reader.FrameCount()) : 0;
    }
}
