#pragma once

#include "Media/AsyncMediaPlayerPositionChangedEventArgs.g.h"

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayerPositionChangedEventArgs : AsyncMediaPlayerPositionChangedEventArgsT<AsyncMediaPlayerPositionChangedEventArgs>
    {
        AsyncMediaPlayerPositionChangedEventArgs(double position);

        double Position();
        void Position(double value);

    private:
        double m_position;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerPositionChangedEventArgs : AsyncMediaPlayerPositionChangedEventArgsT<AsyncMediaPlayerPositionChangedEventArgs, implementation::AsyncMediaPlayerPositionChangedEventArgs>
    {
    };
}
