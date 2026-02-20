#include "pch.h"
#include "AsyncMediaPlayerStateChangedEventArgs.h"
#if __has_include("Media/AsyncMediaPlayerStateChangedEventArgs.g.cpp")
#include "Media/AsyncMediaPlayerStateChangedEventArgs.g.cpp"
#endif

namespace winrt::Telegram::Native::Media::implementation
{
    AsyncMediaPlayerStateChangedEventArgs::AsyncMediaPlayerStateChangedEventArgs(AsyncMediaPlayerState state)
        : m_state(state)
    {

    }

    AsyncMediaPlayerState AsyncMediaPlayerStateChangedEventArgs::State()
    {
        return m_state;
    }

    void AsyncMediaPlayerStateChangedEventArgs::State(AsyncMediaPlayerState value)
    {
        m_state = value;
    }
}
