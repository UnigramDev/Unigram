#include "pch.h"
#include "GroupNetworkStateChangedEventArgs.h"
#if __has_include("GroupNetworkStateChangedEventArgs.g.cpp")
#include "GroupNetworkStateChangedEventArgs.g.cpp"
#endif

namespace winrt::Unigram::Native::Calls::implementation
{
    GroupNetworkStateChangedEventArgs::GroupNetworkStateChangedEventArgs(bool isConnected, bool isTransitioningFromBroadcastToRtc)
        : m_isConnected(isConnected),
        m_isTransitioningFromBroadcastToRtc(isTransitioningFromBroadcastToRtc)
    {

    }

    bool GroupNetworkStateChangedEventArgs::IsConnected()
    {
        return m_isConnected;
    }

    bool GroupNetworkStateChangedEventArgs::IsTransitioningFromBroadcastToRtc()
    {
        return m_isTransitioningFromBroadcastToRtc;
    }
}
