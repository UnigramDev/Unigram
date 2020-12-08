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
		void RemoveSsrcs(IVector<uint32_t> ssrcs);

		void SetIsMuted(bool isMuted);
		void SetAudioOutputDevice(hstring id);
		void SetAudioInputDevice(hstring id);

		winrt::event_token NetworkStateUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			bool> const& value);
		void NetworkStateUpdated(winrt::event_token const& token);

		winrt::event_token AudioLevelsUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			IMapView<uint32_t, float>> const& value);
		void AudioLevelsUpdated(winrt::event_token const& token);

		winrt::event_token MyAudioLevelUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			float> const& value);
		void MyAudioLevelUpdated(winrt::event_token const& token);

	private:
		std::unique_ptr<tgcalls::GroupInstanceImpl> m_impl = nullptr;

		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			bool>> m_networkStateUpdated;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			IMapView<uint32_t, float>>> m_audioLevelsUpdated;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			float>> m_myAudioLevelUpdated;
    };
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
    struct VoipGroupManager : VoipGroupManagerT<VoipGroupManager, implementation::VoipGroupManager>
    {
    };
}
