#pragma once

#include "Media/AsyncMediaPlayerStateChangedEventArgs.g.h"

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayerStateChangedEventArgs : AsyncMediaPlayerStateChangedEventArgsT<AsyncMediaPlayerStateChangedEventArgs>
    {
        AsyncMediaPlayerStateChangedEventArgs(AsyncMediaPlayerState state);

        AsyncMediaPlayerState State();
        void State(AsyncMediaPlayerState value);

    private:
        AsyncMediaPlayerState m_state;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerStateChangedEventArgs : AsyncMediaPlayerStateChangedEventArgsT<AsyncMediaPlayerStateChangedEventArgs, implementation::AsyncMediaPlayerStateChangedEventArgs>
    {
    };
}
