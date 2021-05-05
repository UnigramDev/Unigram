#pragma once

#include "VoipGroupManager.g.h"
#include "group/GroupInstanceCustomImpl.h"

#include "rtc_base/synchronization/mutex.h"

#include <winrt/Telegram.Td.Api.h>

using namespace winrt::Microsoft::Graphics::Canvas::UI::Xaml;
using namespace winrt::Telegram::Td::Api;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
	struct VoipGroupManager : VoipGroupManagerT<VoipGroupManager>
	{
		VoipGroupManager(VoipGroupDescriptor descriptor);

		void Close();

		void SetConnectionMode(VoipGroupConnectionMode connectionMode, bool keepBroadcastIfWasEnabled);

		void EmitJoinPayload(EmitJsonPayloadDelegate completion);
		void SetJoinResponsePayload(hstring payload);
		void RemoveSsrcs(IVector<int32_t> ssrcs);

		winrt::Unigram::Native::Calls::VoipVideoRendererToken AddIncomingVideoOutput(hstring endpointId, CanvasControl canvas);

		bool IsMuted();
		void IsMuted(bool value);

		void SetAudioOutputDevice(hstring id);
		void SetAudioInputDevice(hstring id);
		void SetVideoCapture(Unigram::Native::Calls::VoipVideoCapture videoCapture);

		void SetVolume(int32_t ssrc, double volume);

		winrt::event_token NetworkStateUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			winrt::Unigram::Native::Calls::GroupNetworkStateChangedEventArgs> const& value);
		void NetworkStateUpdated(winrt::event_token const& token);

		winrt::event_token AudioLevelsUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			IMapView<int32_t, IKeyValuePair<float, bool>>> const& value);
		void AudioLevelsUpdated(winrt::event_token const& token);

		winrt::event_token BroadcastPartRequested(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			winrt::Unigram::Native::Calls::BroadcastPartRequestedEventArgs> const& value);
		void BroadcastPartRequested(winrt::event_token const& token);

		winrt::event_token MediaChannelDescriptionsRequested(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			winrt::Unigram::Native::Calls::MediaChannelDescriptionsRequestedEventArgs> const& value);
		void MediaChannelDescriptionsRequested(winrt::event_token const& token);

	private:
		std::unique_ptr<tgcalls::GroupInstanceCustomImpl> m_impl = nullptr;
		std::shared_ptr<tgcalls::VideoCaptureInterface> m_capturer = nullptr;

		bool m_isMuted = true;

		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			winrt::Unigram::Native::Calls::GroupNetworkStateChangedEventArgs>> m_networkStateUpdated;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			IMapView<int32_t, IKeyValuePair<float, bool>>>> m_audioLevelsUpdated;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			winrt::Unigram::Native::Calls::BroadcastPartRequestedEventArgs>> m_broadcastPartRequested;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipGroupManager,
			winrt::Unigram::Native::Calls::MediaChannelDescriptionsRequestedEventArgs>> m_mediaChannelDescriptionsRequested;
	};


	class BroadcastPartTaskImpl final : public tgcalls::BroadcastPartTask {
	public:
		BroadcastPartTaskImpl(
			int64_t time,
			int64_t period,
			std::function<void(tgcalls::BroadcastPart&&)> done)
			: _time(time),
			_scale(period),
			_done(std::move(done))
		{

		}

		void done(int64_t time, int64_t response, Telegram::Td::Api::FilePart filePart) {
			webrtc::MutexLock lock(&_mutex);

			if (_done) {
				auto broadcastPart = tgcalls::BroadcastPart();

				if (filePart) {
					auto part = filePart.Data();
					std::vector data(begin(part), end(part));

					const auto size = part.Size();
					auto bytes = std::vector<uint8_t>(size);
					memcpy(bytes.data(), data.data(), size);

					broadcastPart.oggData = std::move(bytes);
					broadcastPart.responseTimestamp = response;
					broadcastPart.timestampMilliseconds = time;
					broadcastPart.status = tgcalls::BroadcastPart::Status::Success;
				}
				else {
					broadcastPart.status = tgcalls::BroadcastPart::Status::NotReady;
				}

				_done(std::move(broadcastPart));
			}
		}

		void cancel() override {
			webrtc::MutexLock lock(&_mutex);

			if (!_done) {
				return;
			}

			_done = nullptr;
		}

	private:
		const int64_t _time = 0;
		const int32_t _scale = 0;
		std::function<void(tgcalls::BroadcastPart&&)> _done;
		webrtc::Mutex _mutex;

	};

	class RequestMediaChannelDescriptionTaskImpl final : public tgcalls::RequestMediaChannelDescriptionTask {
	public:
		RequestMediaChannelDescriptionTaskImpl(
			std::function<void(std::vector<tgcalls::MediaChannelDescription>&&)> done)
			: _done(std::move(done))
		{

		}

		void done(GroupCallMediaChannelDescriptions descriptions) {
			webrtc::MutexLock lock(&_mutex);

			if (_done) {
				if (descriptions) {
					auto vector = std::vector<tgcalls::MediaChannelDescription>();

					for (const GroupCallMediaChannelDescription& description : descriptions.Descriptions()) {
						tgcalls::MediaChannelDescription impl;
						impl.audioSsrc = description.SourceId();
						impl.videoInformation = string_to_unmanaged(description.Description());

						if (description.IsVideo()) {
							impl.type = tgcalls::MediaChannelDescription::Type::Video;
						}
						else {
							impl.type = tgcalls::MediaChannelDescription::Type::Audio;
						}

						vector.push_back(std::move(impl));
					}

					_done(std::move(vector));
				}
			}
		}

		void cancel() override {
			webrtc::MutexLock lock(&_mutex);

			if (!_done) {
				return;
			}

			_done = nullptr;
		}
	private:
		const int64_t _time = 0;
		const int32_t _scale = 0;
		std::function<void(std::vector<tgcalls::MediaChannelDescription>&&)> _done;
		webrtc::Mutex _mutex;
	};
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
	struct VoipGroupManager : VoipGroupManagerT<VoipGroupManager, implementation::VoipGroupManager>
	{
	};
}
