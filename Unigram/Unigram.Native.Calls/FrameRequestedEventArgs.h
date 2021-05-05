#pragma once

#include "FrameRequestedEventArgs.g.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    struct FrameRequestedEventArgs : FrameRequestedEventArgsT<FrameRequestedEventArgs>
    {
        FrameRequestedEventArgs(int32_t scale, int64_t time, BroadcastPartRequestedDeferral deferral);

        int32_t Scale();
        int64_t Time();
        BroadcastPartRequestedDeferral Deferral();

    private:
        int32_t m_scale;
        int64_t m_time;
        BroadcastPartRequestedDeferral m_deferral;
    };
}
