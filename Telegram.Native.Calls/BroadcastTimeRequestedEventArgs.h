#pragma once

#include "BroadcastTimeRequestedEventArgs.g.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    struct BroadcastTimeRequestedEventArgs : BroadcastTimeRequestedEventArgsT<BroadcastTimeRequestedEventArgs>
    {
        BroadcastTimeRequestedEventArgs(BroadcastTimeRequestedDeferral deferral);

        BroadcastTimeRequestedDeferral Deferral();

    private:
        BroadcastTimeRequestedDeferral m_deferral;
    };
}
