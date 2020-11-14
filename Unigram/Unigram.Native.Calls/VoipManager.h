// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

#include "VoipManager.g.h"
#include "VoipVideoCapture.h"
#include "Instance.h"
#include "InstanceImpl.h"
#include "VideoCaptureInterface.h"
#include "SignalingDataEmittedEventArgs.h"
#include "RemoteMediaStateUpdatedEventArgs.h"

//using namespace winrt::Windows::Foundation;
//using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Unigram::Native::Calls::implementation
{
struct VoipManager : VoipManagerT<VoipManager>
{
  VoipManager(VoipDescriptor descriptor);
  ~VoipManager();

  void Close();

  VoipDescriptor m_descriptor = nullptr;

  std::unique_ptr<tgcalls::Instance> m_impl = nullptr;
  std::shared_ptr<tgcalls::VideoCaptureInterface> m_capturer = nullptr;
  //std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> m_renderer = nullptr;

  void VoipManager::Start();

  //void SetNetworkType(NetworkType networkType);
  void SetMuteMicrophone(bool muteMicrophone);
  void SetAudioOutputGainControlEnabled(bool enabled);
  void SetEchoCancellationStrength(int strength);

  bool SupportsVideo();
  void SetIncomingVideoOutput(Windows::UI::Xaml::UIElement canvas);

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
  void SetVideoCapture(Unigram::Native::Calls::VoipVideoCapture videoCapture);
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
	winrt::event<Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipManager,
		VoipState>> m_stateUpdatedEventSource;
	winrt::event<Windows::Foundation::TypedEventHandler<
		winrt::Unigram::Native::Calls::VoipManager,
		int>> m_signalBarsUpdatedEventSource;
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
