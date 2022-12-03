#pragma once

#include "VoipManager.g.h"
#include "VoipVideoCapture.h"
#include "VoipScreenCapture.h"
#include "Instance.h"
#include "InstanceImpl.h"
#include "v2/InstanceV2Impl.h"
#include "v2/InstanceV2ReferenceImpl.h"
#include "v2_4_0_0/InstanceV2_4_0_0Impl.h"
#include "VideoCaptureInterface.h"
#include "SignalingDataEmittedEventArgs.h"
#include "RemoteMediaStateUpdatedEventArgs.h"

//using namespace winrt::Windows::Foundation;
//using namespace winrt::Windows::Foundation::Collections;

#include <winrt/Telegram.Td.Api.h>

using namespace winrt::Telegram::Td::Api;

namespace winrt::Unigram::Native::Calls::implementation
{
	const auto RegisterTag = tgcalls::Register<tgcalls::InstanceImpl>();
	const auto RegisterTagV2_4_0_0 = tgcalls::Register<tgcalls::InstanceV2_4_0_0Impl>();
	const auto RegisterTagV2_4_0_1 = tgcalls::Register<tgcalls::InstanceV2Impl>();
	const auto RegisterTagV2_4_1_2 = tgcalls::Register<tgcalls::InstanceV2ReferenceImpl>();

	struct VoipManager : VoipManagerT<VoipManager>
	{
		static CallProtocol Protocol() {
			auto minLayer = 92;
			auto maxLayer = tgcalls::Meta::MaxLayer();
			auto versions = tgcalls::Meta::Versions();

			auto CompareVersions = [](std::string a, std::string b) {
				return a > b;
			};

			// Server processes them newer to older
			std::sort(versions.begin(), versions.end(), CompareVersions);

			auto args = winrt::single_threaded_vector<hstring>();

			for (const std::string& x : versions) {
				args.Append(winrt::to_hstring(x));
			}

			return CallProtocol(true, true, minLayer, maxLayer, args);
		}

		VoipManager(hstring version, VoipDescriptor descriptor);

		void Close();

		hstring m_version;
		VoipDescriptor m_descriptor = nullptr;

		std::unique_ptr<tgcalls::Instance> m_impl = nullptr;
		std::shared_ptr<tgcalls::VideoCaptureInterface> m_capturer = nullptr;
		//std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> m_renderer = nullptr;

		void Start();

		//void SetNetworkType(NetworkType networkType);
		bool IsMuted();
		void IsMuted(bool value);
		void SetAudioOutputGainControlEnabled(bool enabled);
		void SetEchoCancellationStrength(int strength);

		bool SupportsVideo();
		void SetIncomingVideoOutput(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl canvas);

		void SetAudioInputDevice(hstring id);
		void SetAudioOutputDevice(hstring id);
		void SetInputVolume(float level);
		void SetOutputVolume(float level);
		void SetAudioOutputDuckingEnabled(bool enabled);

		void SetIsLowBatteryLevel(bool isLowBatteryLevel);

		//std::string getLastError();
		hstring GetDebugInfo();
		int64_t GetPreferredRelayId();
		//TrafficStats getTrafficStats();
		//PersistentState getPersistentState();

		void ReceiveSignalingData(IVector<uint8_t> const data);
		//virtual void setVideoCapture(std::shared_ptr<VideoCaptureInterface> videoCapture) = 0;
		void SetVideoCapture(Unigram::Native::Calls::VoipCaptureBase videoCapture);
		void SetRequestedVideoAspect(float aspect);

		//void stop(std::function<void(FinalState)> completion);

		winrt::event_token StateUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			VoipState> const& value);
		void StateUpdated(winrt::event_token const& token);

		winrt::event_token SignalBarsUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			int> const& value);
		void SignalBarsUpdated(winrt::event_token const& token);

		winrt::event_token AudioLevelUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			float> const& value);
		void AudioLevelUpdated(winrt::event_token const& token);

		winrt::event_token RemoteBatteryLevelIsLowUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			bool> const& value);
		void RemoteBatteryLevelIsLowUpdated(winrt::event_token const& token);

		winrt::event_token RemoteMediaStateUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			winrt::Unigram::Native::Calls::RemoteMediaStateUpdatedEventArgs> const& value);
		void RemoteMediaStateUpdated(winrt::event_token const& token);

		winrt::event_token RemotePrefferedAspectRatioUpdated(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			float> const& value);
		void RemotePrefferedAspectRatioUpdated(winrt::event_token const& token);

		winrt::event_token SignalingDataEmitted(Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			winrt::Unigram::Native::Calls::SignalingDataEmittedEventArgs> const& value);
		void SignalingDataEmitted(winrt::event_token const& token);

	private:
		bool m_isMuted = false;

		std::shared_ptr<VoipVideoRenderer> m_renderer = nullptr;

		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			VoipState>> m_stateUpdatedEventSource;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			int>> m_signalBarsUpdatedEventSource;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			float>> m_audioLevelUpdated;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			bool>> m_remoteBatteryLevelIsLowUpdatedEventSource;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			winrt::Unigram::Native::Calls::RemoteMediaStateUpdatedEventArgs>> m_remoteMediaStateUpdatedEventSource;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			float>> m_remotePrefferedAspectRatioUpdatedEventSource;
		winrt::event<Windows::Foundation::TypedEventHandler<
			winrt::Unigram::Native::Calls::VoipManager,
			winrt::Unigram::Native::Calls::SignalingDataEmittedEventArgs>> m_signalingDataEmittedEventSource;

	};
} // namespace winrt::Unigram::Native::Calls::implementation

namespace winrt::Unigram::Native::Calls::factory_implementation
{
	struct VoipManager : VoipManagerT<VoipManager, implementation::VoipManager>
	{
	};
} // namespace winrt::Unigram::Native::Calls::factory_implementation
