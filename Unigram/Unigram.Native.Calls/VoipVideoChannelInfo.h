#pragma once

#include "VoipVideoChannelInfo.g.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    struct VoipVideoChannelInfo : VoipVideoChannelInfoT<VoipVideoChannelInfo>
    {
        VoipVideoChannelInfo() = default;
        VoipVideoChannelInfo(int32_t audioSource, hstring description, VoipVideoChannelQuality quality)
        : m_audioSource(audioSource),
        m_description(description),
        m_quality(quality)
        {

        }

        int32_t AudioSource();
        void AudioSource(int32_t value);

        hstring Description();
        void Description(hstring value);

        VoipVideoChannelQuality Quality();
        void Quality(VoipVideoChannelQuality value);

    private:
        int32_t m_audioSource;
        hstring m_description;
        VoipVideoChannelQuality m_quality;
    };
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
    struct VoipVideoChannelInfo : VoipVideoChannelInfoT<VoipVideoChannelInfo, implementation::VoipVideoChannelInfo>
    {
    };
}
