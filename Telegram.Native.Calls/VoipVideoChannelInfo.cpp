#include "pch.h"
#include "VoipVideoChannelInfo.h"
#if __has_include("VoipVideoChannelInfo.g.cpp")
#include "VoipVideoChannelInfo.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipVideoChannelInfo::VoipVideoChannelInfo(int32_t audioSource, int64_t participantId, hstring endpointId, IVector<VoipVideoSourceGroup> sourceGroups, VoipVideoChannelQuality minQuality, VoipVideoChannelQuality maxQuality)
        : m_audioSource(audioSource)
        , m_participantId(participantId)
        , m_endpointId(endpointId)
        , m_sourceGroups(sourceGroups)
        , m_minQuality(minQuality)
        , m_maxQuality(maxQuality)
    {
    }

    int32_t VoipVideoChannelInfo::AudioSource()
    {
        return m_audioSource;
    }

    int64_t VoipVideoChannelInfo::ParticipantId()
    {
        return m_participantId;
    }

    hstring VoipVideoChannelInfo::EndpointId()
    {
        return m_endpointId;
    }

    IVector<VoipVideoSourceGroup> VoipVideoChannelInfo::SourceGroups()
    {
        return m_sourceGroups;
    }

    VoipVideoChannelQuality VoipVideoChannelInfo::MinQuality()
    {
        return m_minQuality;
    }

    VoipVideoChannelQuality VoipVideoChannelInfo::MaxQuality()
    {
        return m_maxQuality;
    }
}
