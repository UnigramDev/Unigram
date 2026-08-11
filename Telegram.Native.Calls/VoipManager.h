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

            // Compared component by component as numbers. A plain string compare puts
            // "10.0.0" and "11.0.0" below "9.0.0", which buried the two newest protocols
            // at the wrong end of a list the server reads newest first.
            auto CompareVersions = [](std::string const& a, std::string const& b) {
                size_t i = 0;
                size_t j = 0;

                while (i < a.size() || j < b.size())
                {
                    int left = 0;
                    int right = 0;

                    while (i < a.size() && a[i] >= '0' && a[i] <= '9')
                    {
                        left = left * 10 + (a[i++] - '0');
                    }
                    while (j < b.size() && b[j] >= '0' && b[j] <= '9')
                    {
                        right = right * 10 + (b[j++] - '0');
                    }

                    if (left != right)
                    {
                        return left > right;
                    }

                    // Step over the separator, whatever it turns out to be. One of the
                    // two always advances, so this cannot spin.
                    if (i < a.size()) i++;
                    if (j < b.size()) j++;
                }

                return false;
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
