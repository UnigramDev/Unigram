#pragma once

#include "VoipGroupManager.g.h"
#include "group/GroupInstanceCustomImpl.h"

#include "rtc_base/synchronization/mutex.h"

#include "LoopbackCapture.h"

#include <mutex>

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipGroupManager : VoipGroupManagerT<VoipGroupManager>
    {
        VoipGroupManager(VoipGroupDescriptor descriptor);

        void Stop();

        void SetConnectionMode(VoipGroupConnectionMode connectionMode, bool keepBroadcastIfWasEnabled, bool isUnifiedBroadcast);

        void EmitJoinPayload(EmitJsonPayloadDelegate completion);
        void SetJoinResponsePayload(hstring payload);
        void RemoveSsrcs(IVector<int32_t> ssrcs);

        void AddIncomingVideoOutput(hstring endpointId, winrt::Telegram::Native::Calls::VoipVideoOutputSink sink);

        bool IsMuted();
        void IsMuted(bool value);

        bool IsNoiseSuppressionEnabled();
        void IsNoiseSuppressionEnabled(bool value);

        void SetAudioOutputDevice(hstring id);
        void SetAudioInputDevice(hstring id);
        void SetVideoCapture(Telegram::Native::Calls::VoipCaptureBase videoCapture);

        void AddExternalAudioSamples(std::vector<uint8_t>&& samples);

        void SetVolume(int32_t ssrc, double volume);
        void SetRequestedVideoChannels(IVector<VoipVideoChannelInfo> descriptions);

        void SetEncryptDecrypt(EncryptGroupCallDataDelegate encryptData, DecryptGroupCallDataDelegate decryptData);

        winrt::event_token NetworkStateUpdated(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::GroupNetworkStateChangedEventArgs> const& value);
        void NetworkStateUpdated(winrt::event_token const& token);

        winrt::event_token AudioLevelsUpdated(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            IVector<winrt::Telegram::Native::Calls::VoipGroupParticipant>> const& value);
        void AudioLevelsUpdated(winrt::event_token const& token);

        winrt::event_token AudioBroadcastPartRequested(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::AudioBroadcastPartRequestedEventArgs> const& value);
        void AudioBroadcastPartRequested(winrt::event_token const& token);

        winrt::event_token VideoBroadcastPartRequested(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::VideoBroadcastPartRequestedEventArgs> const& value);
        void VideoBroadcastPartRequested(winrt::event_token const& token);

        winrt::event_token BroadcastTimeRequested(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::BroadcastTimeRequestedEventArgs> const& value);
        void BroadcastTimeRequested(winrt::event_token const& token);

        winrt::event_token MediaChannelDescriptionsRequested(Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::MediaChannelDescriptionsRequestedEventArgs> const& value);
        void MediaChannelDescriptionsRequested(winrt::event_token const& token);

    private:
        std::unique_ptr<tgcalls::GroupInstanceCustomImpl> m_impl = nullptr;
        std::mutex m_lock;

        CLoopbackCapture m_loopback;

        bool m_isMuted = true;
        bool m_isNoiseSuppressionEnabled = true;

        bool m_isScreencast = false;

        void OnNetworkStateUpdated(tgcalls::GroupNetworkState state);
        void OnAudioLevelsUpdated(tgcalls::GroupLevelsUpdate const& levels);
        std::shared_ptr<tgcalls::BroadcastPartTask> OnRequestCurrentTime(std::function<void(int64_t)> done);
        std::shared_ptr<tgcalls::BroadcastPartTask> OnRequestVideoBroadcastPart(int64_t time, int64_t period, int32_t channel, tgcalls::VideoChannelDescription::Quality quality, std::function<void(tgcalls::BroadcastPart&&)> done);
        std::shared_ptr<tgcalls::BroadcastPartTask> OnRequestAudioBroadcastPart(int64_t time, int64_t period, std::function<void(tgcalls::BroadcastPart&&)> done);
        std::shared_ptr<tgcalls::RequestMediaChannelDescriptionTask> OnRequestMediaChannelDescriptions(const std::vector<uint32_t>& ssrcs, std::function<void(std::vector<tgcalls::MediaChannelDescription>&&)> done);
        std::vector<uint8_t> OnE2EEncryptDecrypt(std::vector<uint8_t> const& message, int64_t userId, bool encrypt, int32_t channelId);

        EncryptGroupCallDataDelegate m_encryptData;
        DecryptGroupCallDataDelegate m_decryptData;

        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::GroupNetworkStateChangedEventArgs>> m_networkStateUpdated;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            IVector<winrt::Telegram::Native::Calls::VoipGroupParticipant>>> m_audioLevelsUpdated;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::AudioBroadcastPartRequestedEventArgs>> m_audioBroadcastPartRequested;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::VideoBroadcastPartRequestedEventArgs>> m_videoBroadcastPartRequested;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::BroadcastTimeRequestedEventArgs>> m_broadcastTimeRequested;
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Telegram::Native::Calls::VoipGroupManager,
            winrt::Telegram::Native::Calls::MediaChannelDescriptionsRequestedEventArgs>> m_mediaChannelDescriptionsRequested;
    };

    class RequestMediaChannelDescriptionTaskImpl final : public tgcalls::RequestMediaChannelDescriptionTask
    {
    public:
        RequestMediaChannelDescriptionTaskImpl(std::function<void(std::vector<tgcalls::MediaChannelDescription>&&)> done)
            : _done(std::move(done))
        {

        }

        void done(IVector<VoipMediaChannelDescription> participants)
        {
            webrtc::MutexLock lock(&_mutex);

            if (_done)
            {
                auto result = std::vector<tgcalls::MediaChannelDescription>();
                result.reserve(participants.Size());

                for (auto const& value : participants)
                {
                    result.push_back(tgcalls::MediaChannelDescription{
                        .type = tgcalls::MediaChannelDescription::Type::Audio,
                        .audioSsrc = uint32_t(value.AudioSource),
                        .userId = value.UserId
                        });
                }

                _done(std::move(result));
            }
        }

        void cancel() override
        {
            webrtc::MutexLock lock(&_mutex);

            if (!_done)
            {
                return;
            }

            _done = nullptr;
        }

    private:
        std::function<void(std::vector<tgcalls::MediaChannelDescription>&&)> _done;
        webrtc::Mutex _mutex;

    };

    class BroadcastPartTaskImpl final : public tgcalls::BroadcastPartTask
    {
    public:
        BroadcastPartTaskImpl(std::function<void(tgcalls::BroadcastPart&&)> done)
            : _done(std::move(done))
        {

        }

        void done(int64_t time, int64_t response, IVector<uint8_t> filePart)
        {
            webrtc::MutexLock lock(&_mutex);

            if (_done)
            {
                auto broadcastPart = tgcalls::BroadcastPart();

                if (filePart)
                {
                    std::vector data(begin(filePart), end(filePart));

                    const auto size = filePart.Size();
                    auto bytes = std::vector<uint8_t>(size);
                    memcpy(bytes.data(), data.data(), size);

                    broadcastPart.data = std::move(bytes);
                    broadcastPart.responseTimestamp = response;
                    broadcastPart.timestampMilliseconds = time;
                    broadcastPart.status = tgcalls::BroadcastPart::Status::Success;
                }
                else
                {
                    broadcastPart.status = tgcalls::BroadcastPart::Status::NotReady;
                }

                _done(std::move(broadcastPart));
            }
        }

        void cancel() override
        {
            webrtc::MutexLock lock(&_mutex);

            if (!_done)
            {
                return;
            }

            _done = nullptr;
        }

    private:
        std::function<void(tgcalls::BroadcastPart&&)> _done;
        webrtc::Mutex _mutex;

    };

    class BroadcastTimeTaskImpl final : public tgcalls::BroadcastPartTask
    {
    public:
        BroadcastTimeTaskImpl(std::function<void(int64_t)> done)
            : _done(std::move(done))
        {

        }

        void done(int64_t time)
        {
            webrtc::MutexLock lock(&_mutex);

            if (_done)
            {
                _done(time);
            }
        }

        void cancel() override
        {
            webrtc::MutexLock lock(&_mutex);

            if (!_done)
            {
                return;
            }

            _done = nullptr;
        }

    private:
        std::function<void(int64_t)> _done;
        webrtc::Mutex _mutex;
    };
}

namespace winrt::Telegram::Native::Calls::factory_implementation
{
    struct VoipGroupManager : VoipGroupManagerT<VoipGroupManager, implementation::VoipGroupManager>
    {
    };
}
