#pragma once

#include "VoipGroupParticipantDescription.g.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    struct VoipGroupParticipantDescription : VoipGroupParticipantDescriptionT<VoipGroupParticipantDescription>
    {
        VoipGroupParticipantDescription() = default;

        hstring m_endpointId;
        hstring EndpointId();
        void EndpointId(hstring value);

        int32_t m_audioSsrc = 0;
        int32_t AudioSsrc();
        void AudioSsrc(int32_t value);

        bool m_isRemoved = false;
        bool IsRemoved();
        void IsRemoved(bool value);
    };
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
    struct VoipGroupParticipantDescription : VoipGroupParticipantDescriptionT<VoipGroupParticipantDescription, implementation::VoipGroupParticipantDescription>
    {
    };
}
