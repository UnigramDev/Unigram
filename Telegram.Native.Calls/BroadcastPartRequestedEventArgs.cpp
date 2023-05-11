#include "pch.h"
#include "BroadcastPartRequestedEventArgs.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    BroadcastPartRequestedEventArgs::BroadcastPartRequestedEventArgs(int32_t scale, int64_t time, int32_t channelId, GroupCallVideoQuality videoQuality, BroadcastPartRequestedDeferral deferral)
        : m_scale(scale),
        m_time(time),
        m_channelId(channelId),
        m_videoQuality(videoQuality),
        m_deferral(deferral)
    {

    }

    int32_t BroadcastPartRequestedEventArgs::Scale() {
        return m_scale;
    }

    int64_t BroadcastPartRequestedEventArgs::Time() {
        return m_time;
    }

    int32_t BroadcastPartRequestedEventArgs::ChannelId() {
        return m_channelId;
    }

    GroupCallVideoQuality BroadcastPartRequestedEventArgs::VideoQuality() {
        return m_videoQuality;
    }

    BroadcastPartRequestedDeferral BroadcastPartRequestedEventArgs::Deferral() {
        return m_deferral;
    }
}
