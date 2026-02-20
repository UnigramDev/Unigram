#pragma once

#include "VoipVideoChannelInfo.g.h"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipVideoChannelInfo : VoipVideoChannelInfoT<VoipVideoChannelInfo>
    {
        VoipVideoChannelInfo(int32_t audioSource, int64_t participantId, hstring endpointId, IVector<VoipVideoSourceGroup> sourceGroups, VoipVideoChannelQuality minQuality, VoipVideoChannelQuality maxQuality);

        int32_t AudioSource();
        int64_t ParticipantId();
        hstring EndpointId();
        IVector<VoipVideoSourceGroup> SourceGroups();
        VoipVideoChannelQuality MinQuality();
        VoipVideoChannelQuality MaxQuality();

    private:
        int32_t m_audioSource;
        int64_t m_participantId;
        hstring m_endpointId;
        IVector<VoipVideoSourceGroup> m_sourceGroups;
        VoipVideoChannelQuality m_minQuality;
        VoipVideoChannelQuality m_maxQuality;
    };
}

namespace winrt::Telegram::Native::Calls::factory_implementation
{
    struct VoipVideoChannelInfo : VoipVideoChannelInfoT<VoipVideoChannelInfo, implementation::VoipVideoChannelInfo>
    {
    };
}
