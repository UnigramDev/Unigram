#include "pch.h"
#include "BroadcastPartRequestedEventArgs.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    AudioBroadcastPartRequestedEventArgs::AudioBroadcastPartRequestedEventArgs(int32_t scale, int64_t time, BroadcastPartRequestedDeferral deferral)
        : m_scale(scale)
        , m_time(time)
        , m_deferral(deferral)
    {

    }

    int32_t AudioBroadcastPartRequestedEventArgs::Scale()
    {
        return m_scale;
    }

    int64_t AudioBroadcastPartRequestedEventArgs::Time()
    {
        return m_time;
    }

    BroadcastPartRequestedDeferral AudioBroadcastPartRequestedEventArgs::Deferral()
    {
        return m_deferral;
    }

    VideoBroadcastPartRequestedEventArgs::VideoBroadcastPartRequestedEventArgs(int32_t scale, int64_t time, int32_t channelId, VoipVideoChannelQuality videoQuality, BroadcastPartRequestedDeferral deferral)
        : m_scale(scale)
        , m_time(time)
        , m_channelId(channelId)
        , m_videoQuality(videoQuality)
        , m_deferral(deferral)
    {

    }

    int32_t VideoBroadcastPartRequestedEventArgs::Scale()
    {
        return m_scale;
    }

    int64_t VideoBroadcastPartRequestedEventArgs::Time()
    {
        return m_time;
    }

    int32_t VideoBroadcastPartRequestedEventArgs::ChannelId()
    {
        return m_channelId;
    }

    VoipVideoChannelQuality VideoBroadcastPartRequestedEventArgs::VideoQuality()
    {
        return m_videoQuality;
    }

    BroadcastPartRequestedDeferral VideoBroadcastPartRequestedEventArgs::Deferral()
    {
        return m_deferral;
    }
}
