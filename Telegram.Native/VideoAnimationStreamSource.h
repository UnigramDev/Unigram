#pragma once

#include "VideoAnimationStreamSource.g.h"
#include "Helpers\COMHelper.h"

#include <shcore.h>

#include <winrt/Windows.Storage.Streams.h>

using namespace winrt::Windows::Storage::Streams;

namespace winrt::Telegram::Native::implementation
{
    struct VideoAnimationStreamSource : VideoAnimationStreamSourceT<VideoAnimationStreamSource>
    {
        VideoAnimationStreamSource(IRandomAccessStream stream)
        {
            m_size = stream.Size();
            CreateStreamOverRandomAccessStream(winrt::get_unknown(stream), IID_PPV_ARGS(&m_stream));
        }

        void SeekCallback(int64_t offset)
        {

        }

        void ReadCallback(int64_t count)
        {

        }

        hstring FilePath()
        {
            return L"";
        }

        int64_t FileSize()
        {
            return m_size;
        }

        int64_t Offset()
        {
            return 0;
        }

        int64_t Id()
        {
            return 0;
        }

    public:
        winrt::com_ptr<IStream> m_stream;
        int64_t m_size;
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct VideoAnimationStreamSource : VideoAnimationStreamSourceT<VideoAnimationStreamSource, implementation::VideoAnimationStreamSource>
    {
    };
}
