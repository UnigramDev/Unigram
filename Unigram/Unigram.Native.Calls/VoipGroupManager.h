#pragma once

#include "VoipGroupManager.g.h"
#include "group/GroupInstanceImpl.h"

using namespace winrt::Telegram::Td::Api;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
    struct VoipGroupManager : VoipGroupManagerT<VoipGroupManager>
    {
        VoipGroupManager(VoipGroupDescriptor descriptor);

		void Close();

		void EmitJoinPayload(EmitJsonPayloadDelegate completion);
		void SetJoinResponsePayload(GroupCallJoinResponse payload);
		void RemoveSsrcs(IVector<int32_t> ssrcs);

		bool IsMuted();
		void IsMuted(bool value);

		void SetAudioOutputDevice(hstring id);
		void SetAudioInputDevice(hstring id);

		void SetVolume(int32_t ssrc, double volume);

		winrt::event_token NetworkStateUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			bool> const& value);
		void NetworkStateUpdated(winrt::event_token const& token);

		winrt::event_token AudioLevelsUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			IMapView<int32_t, IKeyValuePair<float, bool>>> const& value);
		void AudioLevelsUpdated(winrt::event_token const& token);

	private:
		std::unique_ptr<tgcalls::GroupInstanceImpl> m_impl = nullptr;

		bool m_isMuted = true;

		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			bool>> m_networkStateUpdated;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			IMapView<int32_t, IKeyValuePair<float, bool>>>> m_audioLevelsUpdated;
    };
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
    struct VoipGroupManager : VoipGroupManagerT<VoipGroupManager, implementation::VoipGroupManager>
    {
    };
}
