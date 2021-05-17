#include "pch.h"
#include "VoipVideoChannelInfo.h"
#if __has_include("VoipVideoChannelInfo.g.cpp")
#include "VoipVideoChannelInfo.g.cpp"
#endif

namespace winrt::Unigram::Native::Calls::implementation
{
    int32_t VoipVideoChannelInfo::AudioSource()
    {
        return m_audioSource;
    }

    void VoipVideoChannelInfo::AudioSource(int32_t value)
    {
        m_audioSource = value;
    }

    hstring VoipVideoChannelInfo::Description()
    {
        return m_description;
    }

    void VoipVideoChannelInfo::Description(hstring value)
    {
        m_description = value;
    }

    VoipVideoChannelQuality VoipVideoChannelInfo::Quality()
    {
        return m_quality;
    }

    void VoipVideoChannelInfo::Quality(VoipVideoChannelQuality value)
    {
        m_quality = value;
    }
}
