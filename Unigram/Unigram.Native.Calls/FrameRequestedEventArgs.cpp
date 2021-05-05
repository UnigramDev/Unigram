#include "pch.h"
#include "FrameRequestedEventArgs.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    FrameRequestedEventArgs::FrameRequestedEventArgs(int32_t scale, int64_t time, BroadcastPartRequestedDeferral deferral)
        : m_scale(scale),
        m_time(time),
        m_deferral(deferral)
    {

    }

    int32_t FrameRequestedEventArgs::Scale() {
        return m_scale;
    }

    int64_t FrameRequestedEventArgs::Time() {
        return m_time;
    }

    BroadcastPartRequestedDeferral FrameRequestedEventArgs::Deferral() {
        return m_deferral;
    }
}
