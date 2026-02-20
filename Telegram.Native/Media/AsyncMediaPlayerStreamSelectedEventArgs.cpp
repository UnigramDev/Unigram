#include "pch.h"
#include "AsyncMediaPlayerStreamSelectedEventArgs.h"
#if __has_include("Media/AsyncMediaPlayerStreamSelectedEventArgs.g.cpp")
#include "Media/AsyncMediaPlayerStreamSelectedEventArgs.g.cpp"
#endif

namespace winrt::Telegram::Native::Media::implementation
{
    AsyncMediaPlayerStreamSelectedEventArgs::AsyncMediaPlayerStreamSelectedEventArgs(int32_t id, AsyncMediaPlayerStreamType type, int32_t width, int32_t height)
        : m_id(id)
        , m_type(type)
        , m_width(width)
        , m_height(height)
    {

    }

    int32_t AsyncMediaPlayerStreamSelectedEventArgs::Id()
    {
        return m_id;
    }

    AsyncMediaPlayerStreamType AsyncMediaPlayerStreamSelectedEventArgs::Type()
    {
        return m_type;
    }

    int32_t AsyncMediaPlayerStreamSelectedEventArgs::Width()
    {
        return m_width;
    }

    int32_t AsyncMediaPlayerStreamSelectedEventArgs::Height()
    {
        return m_height;
    }
}
