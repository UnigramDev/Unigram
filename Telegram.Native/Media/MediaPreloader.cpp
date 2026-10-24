#include "pch.h"
#include "MediaPreloader.h"
#if __has_include("Media/MediaPreloader.g.cpp")
#include "Media/MediaPreloader.g.cpp"
#endif

#include "Media/MediaEngineSources.h"

namespace winrt::Telegram::Native::Media::implementation
{
    MediaPreloader::MediaPreloader(IAsyncMediaPlayerRendition const& rendition)
        : m_preload(std::make_shared<EngineRendition>(rendition))
    {
    }

    winrt::Telegram::Native::Media::MediaPreloadResult MediaPreloader::Run(double seconds, int64_t limit)
    {
        const MediaPreloadOutcome outcome = m_preload.Run(seconds, limit);
        return { outcome.completed, outcome.openBytes, outcome.mediaBytes, outcome.mediaSeconds };
    }

    void MediaPreloader::Cancel()
    {
        m_preload.Cancel();
    }
}
