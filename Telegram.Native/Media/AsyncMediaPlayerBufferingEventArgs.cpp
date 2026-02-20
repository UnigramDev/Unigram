#include "pch.h"
#include "AsyncMediaPlayerBufferingEventArgs.h"
#if __has_include("Media/AsyncMediaPlayerBufferingEventArgs.g.cpp")
#include "Media/AsyncMediaPlayerBufferingEventArgs.g.cpp"
#endif

namespace winrt::Telegram::Native::Media::implementation
{
    AsyncMediaPlayerBufferingEventArgs::AsyncMediaPlayerBufferingEventArgs(float cache)
        : m_cache(cache)
    {

    }

    float AsyncMediaPlayerBufferingEventArgs::Cache()
    {
        return m_cache;
    }

    void AsyncMediaPlayerBufferingEventArgs::Cache(float value)
    {
        m_cache = value;
    }
}
