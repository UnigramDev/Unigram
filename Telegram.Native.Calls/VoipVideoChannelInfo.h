#pragma once

#include "VoipVideoChannelInfo.g.h"

using namespace winrt::Telegram::Td::Api;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipVideoChannelInfo : VoipVideoChannelInfoT<VoipVideoChannelInfo>
    {
        VoipVideoChannelInfo(int32_t audioSource, hstring endpointId, IVector<GroupCallVideoSourceGroup> sourceGroups, VoipVideoChannelQuality minQuality, VoipVideoChannelQuality maxQuality);

        int32_t AudioSource();
        hstring EndpointId();
        IVector<GroupCallVideoSourceGroup> SourceGroups();
        VoipVideoChannelQuality MinQuality();
        VoipVideoChannelQuality MaxQuality();

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
