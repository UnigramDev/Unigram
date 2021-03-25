#include "pch.h"
#include "VoipGroupParticipantDescription.h"
#if __has_include("VoipGroupParticipantDescription.g.cpp")
#include "VoipGroupParticipantDescription.g.cpp"
#endif

namespace winrt::Unigram::Native::Calls::implementation
{
    hstring VoipGroupParticipantDescription::EndpointId()
    {
        return m_endpointId;
    }

    void VoipGroupParticipantDescription::EndpointId(hstring value)
    {
        m_endpointId = value;
    }

    int32_t VoipGroupParticipantDescription::AudioSsrc()
    {
        return m_audioSsrc;
    }

    void VoipGroupParticipantDescription::AudioSsrc(int32_t value)
    {
        m_audioSsrc = value;
    }

    bool VoipGroupParticipantDescription::IsRemoved()
    {
        return m_audioSsrc;
    }

    void VoipGroupParticipantDescription::IsRemoved(bool value)
    {
        m_audioSsrc = value;
    }
}
