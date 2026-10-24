#pragma once

#include "Media/MediaPreloader.g.h"
#include "Media/MediaEngine.h"

namespace winrt::Telegram::Native::Media::implementation
{
    struct MediaPreloader : MediaPreloaderT<MediaPreloader>
    {
        MediaPreloader(IAsyncMediaPlayerRendition const& rendition);

        winrt::Telegram::Native::Media::MediaPreloadResult Run(double seconds, int64_t limit);
        void Cancel();

    private:
        MediaPreload m_preload;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct MediaPreloader : MediaPreloaderT<MediaPreloader, implementation::MediaPreloader>
    {
    };
}
