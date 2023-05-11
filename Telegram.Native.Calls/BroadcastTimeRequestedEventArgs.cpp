#include "pch.h"
#include "BroadcastTimeRequestedEventArgs.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    BroadcastTimeRequestedEventArgs::BroadcastTimeRequestedEventArgs(BroadcastTimeRequestedDeferral deferral)
        : m_deferral(deferral)
    {

    }

    BroadcastTimeRequestedDeferral BroadcastTimeRequestedEventArgs::Deferral() {
        return m_deferral;
    }
}
