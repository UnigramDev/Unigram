#pragma once

#include "Media/AsyncMediaPlayerDurationChangedEventArgs.g.h"

namespace winrt::Telegram::Native::Media::implementation
{
    struct AsyncMediaPlayerDurationChangedEventArgs : AsyncMediaPlayerDurationChangedEventArgsT<AsyncMediaPlayerDurationChangedEventArgs>
    {
        AsyncMediaPlayerDurationChangedEventArgs(double duration);

        double Duration();
        void Duration(double value);

    private:
        double m_duration;
    };
}

namespace winrt::Telegram::Native::Media::factory_implementation
{
    struct AsyncMediaPlayerDurationChangedEventArgs : AsyncMediaPlayerDurationChangedEventArgsT<AsyncMediaPlayerDurationChangedEventArgs, implementation::AsyncMediaPlayerDurationChangedEventArgs>
    {
    };
}
