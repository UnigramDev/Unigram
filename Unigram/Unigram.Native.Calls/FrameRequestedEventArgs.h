#pragma once

#include "FrameRequestedEventArgs.g.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    struct FrameRequestedEventArgs : FrameRequestedEventArgsT<FrameRequestedEventArgs>
    {
        FrameRequestedEventArgs(int32_t scale, int64_t time, FrameReadyDelegate ready);
        FrameRequestedEventArgs() = default;

        int32_t m_scale;
        int32_t Scale();
        
        int64_t m_time;
        int64_t Time();

        FrameReadyDelegate m_ready;
        FrameReadyDelegate Ready();
    };
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
    struct FrameRequestedEventArgs : FrameRequestedEventArgsT<FrameRequestedEventArgs, implementation::FrameRequestedEventArgs>
    {
    };
}
