#include "pch.h"
#include "VoipGroupManager.h"
#if __has_include("VoipGroupManager.g.cpp")
#include "VoipGroupManager.g.cpp"
#endif

#include "VoipVideoCapture.h"
#include "VoipVideoRendererToken.h"
#include "GroupNetworkStateChangedEventArgs.h"
#include "BroadcastPartRequestedEventArgs.h"
#include "MediaChannelDescriptionsRequestedEventArgs.h"

#include "StaticThreads.h"

namespace winrt::Unigram::Native::Calls::implementation
{
	VoipGroupManager::VoipGroupManager(VoipGroupDescriptor descriptor) {
		auto logPath = Windows::Storage::ApplicationData::Current().LocalFolder().Path();
		logPath = logPath + hstring(L"\\tgcalls_group.txt");

		tgcalls::GroupConfig config = tgcalls::GroupConfig{
			true,
			logPath.data()
		};

		tgcalls::GroupInstanceDescriptor impl = tgcalls::GroupInstanceDescriptor();
		impl.threads = tgcalls::StaticThreads::getThreads();
		impl.config = config;
		impl.networkStateUpdated = [this](tgcalls::GroupNetworkState state) {
			auto args = winrt::make_self<GroupNetworkStateChangedEventArgs>(state.isConnected, state.isTransitioningFromBroadcastToRtc);
			m_networkStateUpdated(*this, *args);
		};
		impl.audioLevelsUpdated = [this](tgcalls::GroupLevelsUpdate const& levels) {
			auto args = winrt::single_threaded_map<int32_t, IKeyValuePair<float, bool>>(/*std::move(levels)*/);

			for (const tgcalls::GroupLevelUpdate& x : levels.updates) {
				args.Insert(x.ssrc, winrt::make<winrt::impl::key_value_pair<IKeyValuePair<float, bool>>>(x.value.level, x.value.voice));
			}

			m_audioLevelsUpdated(*this, args.GetView());
		};
		impl.initialInputDeviceId = string_to_unmanaged(descriptor.AudioInputId());
		impl.initialOutputDeviceId = string_to_unmanaged(descriptor.AudioOutputId());
		impl.videoContentType = tgcalls::VideoContentType::Generic;
		//impl.incomingVideoSourcesUpdated = [this](std::vector<uint32_t> const&) {

		//};
		//impl.participantDescriptionsRequired = [this](std::vector<uint32_t> const& ssrcs) {
		//	auto participants = std::vector<tgcalls::GroupParticipantDescription>();

		//	for (const uint32_t& x : ssrcs) {
		//		auto participant = tgcalls::GroupParticipantDescription();
		//		participant.audioSsrc = x;
		//		participants.push_back(participant);
		//	}

		//	m_impl->addParticipants(std::move(participants));
		//};
		impl.requestBroadcastPart = [this](int64_t time, int64_t period, std::function<void(tgcalls::BroadcastPart&&)> done) {
			int scale = 0;
			switch (period) {
			case 1000: scale = 0; break;
			case 500: scale = 1; break;
			case 250: scale = 2; break;
			case 125: scale = 3; break;
			}

			auto task = std::make_shared<BroadcastPartTaskImpl>(time, scale, std::move(done));
			auto args = winrt::make_self<BroadcastPartRequestedEventArgs>(scale, time,
				[task](int64_t time, int64_t response, FilePart filePart) { task->done(time, response, filePart); });

			m_broadcastPartRequested(*this, *args);
			return task;
		};
		impl.requestMediaChannelDescriptions = [this](const std::vector<uint32_t>& ssrcs, std::function<void(std::vector<tgcalls::MediaChannelDescription>&&)> done) {
			auto task = std::make_shared<RequestMediaChannelDescriptionTaskImpl>(std::move(done));
			auto vector = winrt::single_threaded_vector<int32_t>();

			for (const uint32_t& ssrc : ssrcs) {
				vector.Append(ssrc);
			}

			auto args = winrt::make_self<MediaChannelDescriptionsRequestedEventArgs>(vector,
				[task](GroupCallMediaChannelDescriptions descriptions) { task->done(descriptions); });

			m_mediaChannelDescriptionsRequested(*this, *args);
			return task;
		};

		m_impl = std::make_unique<tgcalls::GroupInstanceCustomImpl>(std::move(impl));
	}

	void VoipGroupManager::Close() {
		m_impl->stop();
		m_impl.reset();
	}

	void VoipGroupManager::SetConnectionMode(VoipGroupConnectionMode connectionMode, bool keepBroadcastIfWasEnabled) {
		m_impl->setConnectionMode((tgcalls::GroupConnectionMode)connectionMode, keepBroadcastIfWasEnabled);
	}

	void VoipGroupManager::EmitJoinPayload(EmitJsonPayloadDelegate completion) {
		m_impl->emitJoinPayload([completion](auto const& payload) {
			completion(payload.audioSsrc, string_from_unmanaged(payload.json));
			});
	}

	void VoipGroupManager::SetJoinResponsePayload(hstring payload) {
		m_impl->setJoinResponsePayload(string_to_unmanaged(payload));
	}

	void VoipGroupManager::RemoveSsrcs(IVector<int32_t> ssrcs) {
		auto impl = std::vector<uint32_t>();

		for (const uint32_t& x : ssrcs) {
			impl.push_back(x);
		}

		m_impl->removeSsrcs(impl);
	}

	winrt::Unigram::Native::Calls::VoipVideoRendererToken VoipGroupManager::AddIncomingVideoOutput(hstring endpointId, CanvasControl canvas) {
		auto renderer = std::make_shared<VoipVideoRenderer>(canvas, true);
		m_impl->addIncomingVideoOutput(string_to_unmanaged(endpointId), renderer);

		return *winrt::make_self<VoipVideoRendererToken>(renderer, endpointId, canvas);
	}



	bool VoipGroupManager::IsMuted() {
		return m_isMuted;
	}

	void VoipGroupManager::IsMuted(bool value) {
		m_impl->setIsMuted(m_isMuted = value);
	}

	void VoipGroupManager::SetAudioOutputDevice(hstring id) {
		m_impl->setAudioOutputDevice(string_to_unmanaged(id));
	}
	void VoipGroupManager::SetAudioInputDevice(hstring id) {
		m_impl->setAudioInputDevice(string_to_unmanaged(id));
	}

	void VoipGroupManager::SetVideoCapture(Unigram::Native::Calls::VoipVideoCapture videoCapture) {
		if (videoCapture) {
			auto implementation = winrt::get_self<implementation::VoipVideoCapture>(videoCapture);
			m_capturer = implementation->m_impl;
		}
		else {
			m_capturer = nullptr;
		}

		m_impl->setVideoCapture(m_capturer);
	}

	void VoipGroupManager::SetVolume(int32_t ssrc, double volume) {
		m_impl->setVolume(ssrc, volume);
	}

	winrt::Unigram::Native::Calls::VoipVideoRendererToken VoipGroupManager::SetFullSizeVideoEndpointId(hstring endpointId, CanvasControl canvas) {
		m_impl->setFullSizeVideoEndpointId(string_to_unmanaged(endpointId));
		return AddIncomingVideoOutput(endpointId, canvas);
	}



	winrt::event_token VoipGroupManager::NetworkStateUpdated(Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipGroupManager,
		winrt::Unigram::Native::Calls::GroupNetworkStateChangedEventArgs> const& value)
	{
		return m_networkStateUpdated.add(value);
	}

	void VoipGroupManager::NetworkStateUpdated(winrt::event_token const& token)
	{
		m_networkStateUpdated.remove(token);
	}



	winrt::event_token VoipGroupManager::AudioLevelsUpdated(Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipGroupManager,
		IMapView<int32_t, IKeyValuePair<float, bool>>> const& value)
	{
		return m_audioLevelsUpdated.add(value);
	}

	void VoipGroupManager::AudioLevelsUpdated(winrt::event_token const& token)
	{
		m_audioLevelsUpdated.remove(token);
	}



	winrt::event_token VoipGroupManager::BroadcastPartRequested(Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipGroupManager,
		winrt::Unigram::Native::Calls::BroadcastPartRequestedEventArgs> const& value)
	{
		return m_broadcastPartRequested.add(value);
	}

	void VoipGroupManager::BroadcastPartRequested(winrt::event_token const& token)
	{
		m_broadcastPartRequested.remove(token);
	}



	winrt::event_token VoipGroupManager::MediaChannelDescriptionsRequested(Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipGroupManager,
		winrt::Unigram::Native::Calls::MediaChannelDescriptionsRequestedEventArgs> const& value)
	{
		return m_mediaChannelDescriptionsRequested.add(value);
	}

	void VoipGroupManager::MediaChannelDescriptionsRequested(winrt::event_token const& token)
	{
		m_mediaChannelDescriptionsRequested.remove(token);
	}
}
