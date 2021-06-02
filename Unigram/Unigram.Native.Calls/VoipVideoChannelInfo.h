#pragma once

#include "VoipVideoChannelInfo.g.h"

using namespace winrt::Telegram::Td::Api;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
	struct VoipVideoChannelInfo : VoipVideoChannelInfoT<VoipVideoChannelInfo>
	{
		VoipVideoChannelInfo() = default;
		VoipVideoChannelInfo(int32_t audioSource, hstring endpointId, IVector<GroupCallVideoSourceGroup> sourceGroups, VoipVideoChannelQuality quality)
			: m_audioSource(audioSource),
			m_endpointId(endpointId),
			m_sourceGroups(sourceGroups),
			m_quality(quality)
		{

		}

		VoipVideoChannelInfo(VoipVideoRendererToken token, VoipVideoChannelQuality quality)
			: m_audioSource(token.AudioSource()),
			m_endpointId(token.EndpointId()),
			m_sourceGroups(token.SourceGroups()),
			m_quality(quality)
		{

		}

		int32_t AudioSource();
		void AudioSource(int32_t value);

		hstring EndpointId();
		void EndpointId(hstring value);

		IVector<GroupCallVideoSourceGroup> SourceGroups();
		void SourceGroups(IVector<GroupCallVideoSourceGroup> value);

		VoipVideoChannelQuality Quality();
		void Quality(VoipVideoChannelQuality value);

	private:
		int32_t m_audioSource;
		hstring m_endpointId;
		IVector<GroupCallVideoSourceGroup> m_sourceGroups;
		VoipVideoChannelQuality m_quality;
	};
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
	struct VoipVideoChannelInfo : VoipVideoChannelInfoT<VoipVideoChannelInfo, implementation::VoipVideoChannelInfo>
	{
	};
}
