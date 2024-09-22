#include "pch.h"
#include "VoipVideoChannelInfo.h"
#if __has_include("VoipVideoChannelInfo.g.cpp")
#include "VoipVideoChannelInfo.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipVideoChannelInfo::VoipVideoChannelInfo(int32_t audioSource, hstring endpointId, IVector<GroupCallVideoSourceGroup> sourceGroups, VoipVideoChannelQuality minQuality, VoipVideoChannelQuality maxQuality)
        : m_audioSource(audioSource)
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

    hstring VoipVideoChannelInfo::EndpointId()
    {
        return m_endpointId;
    }

    IVector<GroupCallVideoSourceGroup> VoipVideoChannelInfo::SourceGroups()
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
