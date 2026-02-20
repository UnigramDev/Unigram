#pragma once

#include "Media/AsyncMediaPlayerStreamSelectedEventArgs.g.h"

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayerStreamSelectedEventArgs : AsyncMediaPlayerStreamSelectedEventArgsT<AsyncMediaPlayerStreamSelectedEventArgs>
    {
        AsyncMediaPlayerStreamSelectedEventArgs(int32_t id, AsyncMediaPlayerStreamType type, int32_t width, int32_t height);

        int32_t Id();
        AsyncMediaPlayerStreamType Type();
        int32_t Width();
        int32_t Height();

    private:
        int32_t m_id;
        AsyncMediaPlayerStreamType m_type;
        int32_t m_width;
        int32_t m_height;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerStreamSelectedEventArgs : AsyncMediaPlayerStreamSelectedEventArgsT<AsyncMediaPlayerStreamSelectedEventArgs, implementation::AsyncMediaPlayerStreamSelectedEventArgs>
    {
    };
}
