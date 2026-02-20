#pragma once

#include "AudioBroadcastPartRequestedEventArgs.g.h"
#include "VideoBroadcastPartRequestedEventArgs.g.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    struct AudioBroadcastPartRequestedEventArgs : AudioBroadcastPartRequestedEventArgsT<AudioBroadcastPartRequestedEventArgs>
    {
        AudioBroadcastPartRequestedEventArgs(int32_t scale, int64_t time, BroadcastPartRequestedDeferral deferral);

        int32_t Scale();
        int64_t Time();
        BroadcastPartRequestedDeferral Deferral();

    private:
        int32_t m_scale;
        int64_t m_time;
        BroadcastPartRequestedDeferral m_deferral;
    };

    struct VideoBroadcastPartRequestedEventArgs : VideoBroadcastPartRequestedEventArgsT<VideoBroadcastPartRequestedEventArgs>
    {
        VideoBroadcastPartRequestedEventArgs(int32_t scale, int64_t time, int32_t channelId, VoipVideoChannelQuality videoQuality, BroadcastPartRequestedDeferral deferral);

        int32_t Scale();
        int64_t Time();
        int32_t ChannelId();
        VoipVideoChannelQuality VideoQuality();
        BroadcastPartRequestedDeferral Deferral();

    private:
        int32_t m_scale;
        int64_t m_time;
        int32_t m_channelId;
        VoipVideoChannelQuality m_videoQuality;
        BroadcastPartRequestedDeferral m_deferral;
    };
}
