#include "pch.h"
#include "AsyncMediaPlayerDurationChangedEventArgs.h"
#if __has_include("Media/AsyncMediaPlayerDurationChangedEventArgs.g.cpp")
#include "Media/AsyncMediaPlayerDurationChangedEventArgs.g.cpp"
#endif

namespace winrt::Telegram::Native::Media::implementation
{
    AsyncMediaPlayerDurationChangedEventArgs::AsyncMediaPlayerDurationChangedEventArgs(double duration)
        : m_duration(duration)
    {
    }

    double AsyncMediaPlayerDurationChangedEventArgs::Duration()
    {
        return m_duration;
    }

    void AsyncMediaPlayerDurationChangedEventArgs::Duration(double value)
    {
        m_duration = value;
    }
}
