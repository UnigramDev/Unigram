#pragma once

#include "VoipManager.g.h"
#include "VoipVideoCapture.h"
#include "VoipScreenCapture.h"
#include "Instance.h"
#include "InstanceImpl.h"
#include "v2/InstanceV2Impl.h"
#include "v2/InstanceV2ReferenceImpl.h"
#include "VideoCaptureInterface.h"
#include "SignalingDataEmittedEventArgs.h"
#include "RemoteMediaStateUpdatedEventArgs.h"
#include "VoipVideoOutput.h"

//using namespace winrt::Windows::Foundation;
//using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipManager : VoipManagerT<VoipManager>
    {
        static winrt::Telegram::Native::Calls::VoipCallProtocol Protocol()
        {
            auto minLayer = 92;
            auto maxLayer = tgcalls::Meta::MaxLayer();
            auto versions = tgcalls::Meta::Versions();

            auto CompareVersions = [](std::string const& a, std::string const& b) {
                return a > b;
            };

            // Server processes them newer to older
            std::sort(versions.begin(), versions.end(), CompareVersions);

            auto args = winrt::single_threaded_vector<hstring>();

            for (const std::string& x : versions)
            {
                args.Append(winrt::to_hstring(x));
            }

            return winrt::Telegram::Native::Calls::VoipCallProtocol(true, true, minLayer, maxLayer, args);
        }

        VoipManager() = default;

        // See VoipGroupManager: destroying the instance without stopping it skips
        // tgcalls' own teardown.
        ~VoipManager();

        void Start(VoipDescriptor descriptor);
        void Stop();

        bool IsMuted();
        void IsMuted(bool value);
        void SetAudioOutputGainControlEnabled(bool enabled);
        void SetEchoCancellationStrength(int strength);

        bool SupportsVideo();
        void SetIncomingVideoOutput(winrt::Telegram::Native::Calls::VoipVideoOutputSink sink);

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
        void SetVideoCapture(Telegram::Native::Calls::VoipCaptureBase videoCapture);
        void SetRequestedVideoAspect(float aspect);

        //void stop(std::function<void(FinalState)> completion);

        winrt::event_token StateUpdated(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            VoipReadyState> const& value);
        void StateUpdated(winrt::event_token const& token);

        winrt::event_token SignalBarsUpdated(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            int> const& value);
        void SignalBarsUpdated(winrt::event_token const& token);

        winrt::event_token AudioLevelUpdated(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            float> const& value);
        void AudioLevelUpdated(winrt::event_token const& token);

        winrt::event_token RemoteBatteryLevelIsLowUpdated(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            bool> const& value);
        void RemoteBatteryLevelIsLowUpdated(winrt::event_token const& token);

        winrt::event_token RemoteMediaStateUpdated(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            winrt::Telegram::Native::Calls::RemoteMediaStateUpdatedEventArgs> const& value);
        void RemoteMediaStateUpdated(winrt::event_token const& token);

        winrt::event_token RemotePrefferedAspectRatioUpdated(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            float> const& value);
        void RemotePrefferedAspectRatioUpdated(winrt::event_token const& token);

        winrt::event_token SignalingDataEmitted(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            winrt::Telegram::Native::Calls::SignalingDataEmittedEventArgs> const& value);
        void SignalingDataEmitted(winrt::event_token const& token);

    private:
        std::unique_ptr<tgcalls::Instance> m_impl = nullptr;

        std::weak_ptr<VoipVideoOutput> m_incomingVideoOutput;

        bool m_isMuted = false;

        void OnStateUpdated(tgcalls::State state);
        void OnSignalBarsUpdated(int signalBarCount);
        void OnAudioLevelUpdated(float audioLevel);
        void OnRemoteBatteryLevelIsLowUpdated(bool isLow);
        void OnRemoteMediaStateUpdated(tgcalls::AudioState audio, tgcalls::VideoState video);
        void OnRemotePrefferedAspectRadioUpdated(float ratio);
        void OnSignalingDataEmitted(std::vector<uint8_t> data);

        // winrt::event synchronises itself, so these need no lock of their own. Adding
        // one back would reintroduce a deadlock: the callbacks below run on tgcalls
        // threads and reach managed code that takes its own locks, while the UI thread
        // unsubscribes from inside those same locks.
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            VoipReadyState>> m_stateUpdatedEventSource;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            int>> m_signalBarsUpdatedEventSource;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            float>> m_audioLevelUpdatedEventSource;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            bool>> m_remoteBatteryLevelIsLowUpdatedEventSource;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            winrt::Telegram::Native::Calls::RemoteMediaStateUpdatedEventArgs>> m_remoteMediaStateUpdatedEventSource;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            float>> m_remotePrefferedAspectRatioUpdatedEventSource;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipManager,
            winrt::Telegram::Native::Calls::SignalingDataEmittedEventArgs>> m_signalingDataEmittedEventSource;

    };
} // namespace winrt::Telegram::Native::Calls::implementation

namespace winrt::Telegram::Native::Calls::factory_implementation
{
    struct VoipManager : VoipManagerT<VoipManager, implementation::VoipManager>
    {
    };
} // namespace winrt::Telegram::Native::Calls::factory_implementation
