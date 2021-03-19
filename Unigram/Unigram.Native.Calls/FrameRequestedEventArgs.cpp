#include "pch.h"
#include "FrameRequestedEventArgs.h"
#if __has_include("FrameRequestedEventArgs.g.cpp")
#include "FrameRequestedEventArgs.g.cpp"
#endif

namespace winrt::Unigram::Native::Calls::implementation
{
    FrameRequestedEventArgs::FrameRequestedEventArgs(int32_t scale, int64_t time, FrameReadyDelegate ready)
        : m_scale(scale),
        m_time(time),
        m_ready(ready)
    {

    }

    int32_t FrameRequestedEventArgs::Scale() {
        return m_scale;
    }

    int64_t FrameRequestedEventArgs::Time() {
        return m_time;
    }

    FrameReadyDelegate FrameRequestedEventArgs::Ready() {
        return m_ready;
    }
}
