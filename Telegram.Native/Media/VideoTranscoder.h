#pragma once

#include "Media/VideoTranscoder.g.h"

#include <atomic>

namespace winrt::Telegram::Native::Media::implementation
{
    struct VideoTranscoder : VideoTranscoderT<VideoTranscoder>
    {
        VideoTranscoder() = default;

        void Transcode(winrt::Telegram::Native::Media::VideoTranscoderOptions const& options, winrt::Telegram::Native::Media::VideoTranscoderProgressHandler const& progress);
        void Cancel();

    private:
        std::atomic<bool> m_canceled{ false };
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct VideoTranscoder : VideoTranscoderT<VideoTranscoder, implementation::VideoTranscoder>
    {
    };
}
