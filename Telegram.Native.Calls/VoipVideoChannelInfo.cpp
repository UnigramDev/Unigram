#include "pch.h"
#include "VoipVideoChannelInfo.h"
#if __has_include("VoipVideoChannelInfo.g.cpp")
#include "VoipVideoChannelInfo.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    int32_t VoipVideoChannelInfo::AudioSource()
    {
        return m_audioSource;
    }

    void VoipVideoChannelInfo::AudioSource(int32_t value)
    {
        m_audioSource = value;
    }

    hstring VoipVideoChannelInfo::EndpointId()
    {
        return m_endpointId;
    }

    void VoipVideoChannelInfo::EndpointId(hstring value)
    {
        m_endpointId = value;
    }

    IVector<GroupCallVideoSourceGroup> VoipVideoChannelInfo::SourceGroups()
    {
        return m_sourceGroups;
    }

    void VoipVideoChannelInfo::SourceGroups(IVector<GroupCallVideoSourceGroup> value)
    {
        m_sourceGroups = value;
    }

    VoipVideoChannelQuality VoipVideoChannelInfo::MinQuality()
    {
        return m_minQuality;
    }

    void VoipVideoChannelInfo::MinQuality(VoipVideoChannelQuality value)
    {
        m_minQuality = value;
    }

    VoipVideoChannelQuality VoipVideoChannelInfo::MaxQuality()
    {
        return m_maxQuality;
    }

    void VoipVideoChannelInfo::MaxQuality(VoipVideoChannelQuality value)
    {
        m_maxQuality = value;
    }
}
