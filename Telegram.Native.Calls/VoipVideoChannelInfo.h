#pragma once

#include "VoipVideoChannelInfo.g.h"

using namespace winrt::Telegram::Td::Api;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipVideoChannelInfo : VoipVideoChannelInfoT<VoipVideoChannelInfo>
    {
        VoipVideoChannelInfo() = default;
        VoipVideoChannelInfo(int32_t audioSource, hstring endpointId, IVector<GroupCallVideoSourceGroup> sourceGroups, VoipVideoChannelQuality minQuality, VoipVideoChannelQuality maxQuality)
            : m_audioSource(audioSource),
            m_endpointId(endpointId),
            m_sourceGroups(sourceGroups),
            m_minQuality(minQuality),
            m_maxQuality(maxQuality)
        {

        }

        VoipVideoChannelInfo(VoipVideoRendererToken token, VoipVideoChannelQuality minQuality, VoipVideoChannelQuality maxQuality)
            : m_audioSource(token.AudioSource()),
            m_endpointId(token.EndpointId()),
            m_sourceGroups(token.SourceGroups()),
            m_minQuality(minQuality),
            m_maxQuality(maxQuality)
        {

        }

        int32_t AudioSource();
        void AudioSource(int32_t value);

        hstring EndpointId();
        void EndpointId(hstring value);

        IVector<GroupCallVideoSourceGroup> SourceGroups();
        void SourceGroups(IVector<GroupCallVideoSourceGroup> value);

        VoipVideoChannelQuality MinQuality();
        void MinQuality(VoipVideoChannelQuality value);

        VoipVideoChannelQuality MaxQuality();
        void MaxQuality(VoipVideoChannelQuality value);

    private:
        int32_t m_audioSource;
        hstring m_endpointId;
        IVector<GroupCallVideoSourceGroup> m_sourceGroups;
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
