#include "pch.h"
#include "AsyncMediaPlayerPositionChangedEventArgs.h"
#if __has_include("Media/AsyncMediaPlayerPositionChangedEventArgs.g.cpp")
#include "Media/AsyncMediaPlayerPositionChangedEventArgs.g.cpp"
#endif

namespace winrt::Telegram::Native::Media::implementation
{
    AsyncMediaPlayerPositionChangedEventArgs::AsyncMediaPlayerPositionChangedEventArgs(double position)
        : m_position(position)
    {
    }

    double AsyncMediaPlayerPositionChangedEventArgs::Position()
    {
        return m_position;
    }

    void AsyncMediaPlayerPositionChangedEventArgs::Position(double value)
    {
        m_position = value;
    }
}
