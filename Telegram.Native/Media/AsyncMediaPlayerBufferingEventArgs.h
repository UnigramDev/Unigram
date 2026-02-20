#pragma once

#include "Media/AsyncMediaPlayerBufferingEventArgs.g.h"

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayerBufferingEventArgs : AsyncMediaPlayerBufferingEventArgsT<AsyncMediaPlayerBufferingEventArgs>
    {
        AsyncMediaPlayerBufferingEventArgs(float cache);

        float Cache();
        void Cache(float value);

    private:
        float m_cache;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerBufferingEventArgs : AsyncMediaPlayerBufferingEventArgsT<AsyncMediaPlayerBufferingEventArgs, implementation::AsyncMediaPlayerBufferingEventArgs>
    {
    };
}
