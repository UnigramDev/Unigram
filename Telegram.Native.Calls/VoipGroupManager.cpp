#include "pch.h"
#include "VoipGroupManager.h"
#if __has_include("VoipGroupManager.g.cpp")
#include "VoipGroupManager.g.cpp"
#endif

#include "VoipVideoCapture.h"
#include "VoipScreenCapture.h"
#include "VoipVideoOutputSink.h"
#include "GroupNetworkStateChangedEventArgs.h"
#include "BroadcastPartRequestedEventArgs.h"
#include "BroadcastTimeRequestedEventArgs.h"
#include "MediaChannelDescriptionsRequestedEventArgs.h"

#include "StaticThreads.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipGroupManager::VoipGroupManager(VoipGroupDescriptor descriptor)
    {
        auto logPath = Windows::Storage::ApplicationData::Current().LocalFolder().Path();
        logPath = logPath + hstring(L"\\tgcalls_group.txt");

        tgcalls::GroupConfig config = tgcalls::GroupConfig{
            true,
            logPath.data()
        };

        tgcalls::GroupInstanceDescriptor impl = tgcalls::GroupInstanceDescriptor
        {
            .threads = tgcalls::StaticThreads::getThreads(),
            .config = config,
            .networkStateUpdated = [weakThis{ get_weak() }](tgcalls::GroupNetworkState state) {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->OnNetworkStateUpdated(state);
                }
            },
            .audioLevelsUpdated = [weakThis{ get_weak() }](tgcalls::GroupLevelsUpdate const& levels) {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->OnAudioLevelsUpdated(levels);
                }
            },
            .initialInputDeviceId = winrt::to_string(descriptor.AudioInputId()),
            .initialOutputDeviceId = winrt::to_string(descriptor.AudioOutputId()),
            .requestCurrentTime = [weakThis{ get_weak() }](std::function<void(int64_t)> done) {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->OnRequestCurrentTime(done);
                }
            },
            .requestAudioBroadcastPart = [weakThis{ get_weak() }](int64_t time, int64_t period, std::function<void(tgcalls::BroadcastPart&&)> done) {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->OnRequestAudioBroadcastPart(time, period, done);
                }
            },
            .requestVideoBroadcastPart = [weakThis{ get_weak() }](int64_t time, int64_t period, int32_t channel, tgcalls::VideoChannelDescription::Quality quality, std::function<void(tgcalls::BroadcastPart&&)> done) {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->OnRequestVideoBroadcastPart(time, period, channel, quality, done);
                }
            },
            .videoContentType = (tgcalls::VideoContentType)descriptor.VideoContentType(),
            .initialEnableNoiseSuppression = m_isNoiseSuppressionEnabled = descriptor.IsNoiseSuppressionEnabled(),
        };

        if (descriptor.IsConference())
        {
            impl.isConference = true;
            impl.e2eEncryptDecrypt = [weakThis{ get_weak() }](std::vector<uint8_t> const& message, int64_t userId, bool encrypt, int32_t channelId) {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->OnE2EEncryptDecrypt(message, userId, encrypt, channelId);
                }
                };
            impl.requestMediaChannelDescriptions = [weakThis{ get_weak() }](std::vector<uint32_t> const& ssrcs, std::function<void(std::vector<tgcalls::MediaChannelDescription>&&)> done) {
                if (auto strongThis = weakThis.get())
                {
                    return strongThis->OnRequestMediaChannelDescriptions(ssrcs, done);
                }
            };
        }

        impl.videoCapture = GetVideoCaptureImpl(descriptor.VideoCapture());

        m_impl = std::make_unique<tgcalls::GroupInstanceCustomImpl>(std::move(impl));

        if (VoipVideoContentType::Screencast == descriptor.VideoContentType())
        {
            m_isScreencast = true;

            auto audioProcessId = descriptor.AudioProcessId();
            if (audioProcessId == 0)
            {
                return;
            }

            m_loopback = Microsoft::WRL::Make<CLoopbackCapture>();
            m_loopback->SetOutputSink([weakThis{ get_weak() }](std::vector<uint8_t>&& samples) {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->AddExternalAudioSamples(std::move(samples));
                }
                });

            auto result = audioProcessId == -1
                ? m_loopback->StartCaptureAsync(GetCurrentProcessId(), false)
                : m_loopback->StartCaptureAsync(static_cast<DWORD>(audioProcessId), true);

            if (FAILED(result))
            {
                // Process loopback is unavailable on older builds; share the screen
                // without its audio rather than failing the whole capture.
                m_loopback.Reset();
                return;
            }

            IsMuted(false);
        }
    }

    void VoipGroupManager::Stop()
    {
        if (m_loopback)
        {
            m_loopback->StopCaptureAsync();
            m_loopback.Reset();
        }

        if (m_impl)
        {
            m_impl->stop([]() {});
            m_impl.reset();
        }
    }

    void VoipGroupManager::SetConnectionMode(VoipGroupConnectionMode connectionMode, bool keepBroadcastIfWasEnabled, bool isUnifiedBroadcast)
    {
        if (m_impl)
        {
            m_impl->setConnectionMode((tgcalls::GroupConnectionMode)connectionMode, keepBroadcastIfWasEnabled, isUnifiedBroadcast);
        }
    }

    void VoipGroupManager::EmitJoinPayload(EmitJsonPayloadDelegate completion)
    {
        if (m_impl)
        {
            m_impl->emitJoinPayload([completion](auto const& payload) {
                completion(payload.audioSsrc, winrt::to_hstring(payload.json));
                });
        }
        else
        {
            completion(0, L"");
        }
    }

    void VoipGroupManager::SetJoinResponsePayload(hstring payload)
    {
        if (m_impl)
        {
            m_impl->setJoinResponsePayload(winrt::to_string(payload));
        }
    }

    void VoipGroupManager::RemoveSsrcs(IVector<int32_t> ssrcs)
    {
        if (m_impl)
        {
            m_impl->removeSsrcs(std::vector<uint32_t>(ssrcs.begin(), ssrcs.end()));
        }
    }

    void VoipGroupManager::AddIncomingVideoOutput(hstring endpointId, winrt::Telegram::Native::Calls::VoipVideoOutputSink sink)
    {
        if (m_impl && sink)
        {
            auto implementation = winrt::get_self<VoipVideoOutputSink>(sink);
            m_impl->addIncomingVideoOutput(winrt::to_string(endpointId), implementation->Sink());
        }
    }



    bool VoipGroupManager::IsMuted()
    {
        return m_isMuted;
    }

    void VoipGroupManager::IsMuted(bool value)
    {
        if (m_impl)
        {
            m_impl->setIsMuted(m_isMuted = value);
        }
    }

    bool VoipGroupManager::IsNoiseSuppressionEnabled()
    {
        return m_isNoiseSuppressionEnabled;
    }

    void VoipGroupManager::IsNoiseSuppressionEnabled(bool value)
    {
        if (m_impl)
        {
            m_impl->setIsNoiseSuppressionEnabled(m_isNoiseSuppressionEnabled = value);
        }
    }

    void VoipGroupManager::SetAudioOutputDevice(hstring id)
    {
        if (m_impl)
        {
            m_impl->setAudioOutputDevice(winrt::to_string(id));
        }
    }
    void VoipGroupManager::SetAudioInputDevice(hstring id)
    {
        if (m_impl)
        {
            m_impl->setAudioInputDevice(winrt::to_string(id));
        }
    }

    void VoipGroupManager::SetVideoCapture(Telegram::Native::Calls::VoipCaptureBase videoCapture)
    {
        if (m_impl)
        {
            m_impl->setVideoCapture(GetVideoCaptureImpl(videoCapture));
        }
    }

    void VoipGroupManager::AddExternalAudioSamples(std::vector<uint8_t>&& samples)
    {
        if (m_impl)
        {
            m_impl->addExternalAudioSamples(std::move(samples));
        }
    }

    void VoipGroupManager::SetVolume(int32_t ssrc, double volume)
    {
        if (m_impl && ssrc)
        {
            m_impl->setVolume(ssrc, volume);
        }
    }

    void VoipGroupManager::SetRequestedVideoChannels(IVector<VoipVideoChannelInfo> descriptions)
    {
        auto impl = std::vector<tgcalls::VideoChannelDescription>();

        for (const VoipVideoChannelInfo& x : descriptions)
        {
            tgcalls::VideoChannelDescription item;
            item.audioSsrc = x.AudioSource();
            item.endpointId = winrt::to_string(x.EndpointId());
            item.userId = x.ParticipantId();
            item.minQuality = (tgcalls::VideoChannelDescription::Quality)x.MinQuality();
            item.maxQuality = (tgcalls::VideoChannelDescription::Quality)x.MaxQuality();

            for (const VoipVideoSourceGroup& group : x.SourceGroups())
            {
                tgcalls::MediaSsrcGroup groupImpl;
                groupImpl.semantics = winrt::to_string(group.Semantics());
                groupImpl.ssrcs = vector_to_unmanaged<uint32_t, int32_t>(group.SourceIds());

                item.ssrcGroups.push_back(std::move(groupImpl));
            }

            impl.push_back(std::move(item));
        }

        if (m_impl)
        {
            m_impl->setRequestedVideoChannels(std::move(impl));
        }
    }



    void VoipGroupManager::OnNetworkStateUpdated(tgcalls::GroupNetworkState state)
    {
        std::lock_guard const guard(m_lock);
        auto args = winrt::make_self<GroupNetworkStateChangedEventArgs>(state.isConnected, state.isTransitioningFromBroadcastToRtc);
        m_networkStateUpdated(*this, *args);
    }

    void VoipGroupManager::OnAudioLevelsUpdated(tgcalls::GroupLevelsUpdate const& levels)
    {
        std::lock_guard const guard(m_lock);
        auto args = winrt::single_threaded_vector<winrt::Telegram::Native::Calls::VoipGroupParticipant>(/*std::move(levels)*/);

        for (const tgcalls::GroupLevelUpdate& x : levels.updates)
        {
            args.Append(winrt::Telegram::Native::Calls::VoipGroupParticipant{
                .AudioSource = static_cast<int32_t>(x.ssrc),
                    .Level = x.value.level,
                    .IsSpeaking = x.value.voice,
                    .IsMuted = x.value.isMuted
                });
        }

        m_audioLevelsUpdated(*this, args);
    }

    std::shared_ptr<tgcalls::BroadcastPartTask> VoipGroupManager::OnRequestCurrentTime(std::function<void(int64_t)> done)
    {
        std::lock_guard const guard(m_lock);
        auto task = std::make_shared<BroadcastTimeTaskImpl>(std::move(done));
        auto args = winrt::make_self<BroadcastTimeRequestedEventArgs>([task](int64_t time) { task->done(time); });

        m_broadcastTimeRequested(*this, *args);
        return task;
    }

    std::shared_ptr<tgcalls::BroadcastPartTask> VoipGroupManager::OnRequestVideoBroadcastPart(int64_t time, int64_t period, int32_t channel, tgcalls::VideoChannelDescription::Quality quality, std::function<void(tgcalls::BroadcastPart&&)> done)
    {
        std::lock_guard const guard(m_lock);
        int scale = 0;
        switch (period)
        {
        case 1000: scale = 0; break;
        case 500: scale = 1; break;
        case 250: scale = 2; break;
        case 125: scale = 3; break;
        }

        VoipVideoChannelQuality qualityImpl = VoipVideoChannelQuality::Thumbnail;
        switch (quality)
        {
        case tgcalls::VideoChannelDescription::Quality::Thumbnail:
            qualityImpl = VoipVideoChannelQuality::Thumbnail;
            break;
        case tgcalls::VideoChannelDescription::Quality::Medium:
            qualityImpl = VoipVideoChannelQuality::Medium;
            break;
        case tgcalls::VideoChannelDescription::Quality::Full:
            qualityImpl = VoipVideoChannelQuality::Full;
            break;
        }

        auto task = std::make_shared<BroadcastPartTaskImpl>(std::move(done));
        auto args = winrt::make_self<VideoBroadcastPartRequestedEventArgs>(scale, time, channel, qualityImpl,
            [task](int64_t time, int64_t response, IVector<uint8_t> data) { task->done(time, response, data); });

        m_videoBroadcastPartRequested(*this, *args);
        return task;
    }

    std::shared_ptr<tgcalls::BroadcastPartTask> VoipGroupManager::OnRequestAudioBroadcastPart(int64_t time, int64_t period, std::function<void(tgcalls::BroadcastPart&&)> done)
    {
        std::lock_guard const guard(m_lock);
        int scale = 0;
        switch (period)
        {
        case 1000: scale = 0; break;
        case 500: scale = 1; break;
        case 250: scale = 2; break;
        case 125: scale = 3; break;
        }

        auto task = std::make_shared<BroadcastPartTaskImpl>(std::move(done));
        auto args = winrt::make_self<AudioBroadcastPartRequestedEventArgs>(scale, time,
            [task](int64_t time, int64_t response, IVector<uint8_t> data) { task->done(time, response, data); });

        m_audioBroadcastPartRequested(*this, *args);
        return task;
    }

    std::shared_ptr<tgcalls::RequestMediaChannelDescriptionTask> VoipGroupManager::OnRequestMediaChannelDescriptions(const std::vector<uint32_t>& ssrcs, std::function<void(std::vector<tgcalls::MediaChannelDescription>&&)> done)
    {
        std::lock_guard const guard(m_lock);

        auto audioSourceIds = winrt::single_threaded_vector<uint32_t>(std::vector<uint32_t>(ssrcs));

        auto task = std::make_shared<RequestMediaChannelDescriptionTaskImpl>(std::move(done));
        auto args = winrt::make_self<MediaChannelDescriptionsRequestedEventArgs>(audioSourceIds,
            [task](IVector<VoipMediaChannelDescription> participants) { task->done(participants); });

        m_mediaChannelDescriptionsRequested(*this, *args);
        return task;
    }

    std::vector<uint8_t> VoipGroupManager::OnE2EEncryptDecrypt(std::vector<uint8_t> const& message, int64_t userId, bool encrypt, int32_t unencryptedPrefixSize)
    {
        std::lock_guard const guard(m_lock);

        // An empty result is how tgcalls is told the frame failed and must be dropped.
        // Returning the input untouched would put plaintext on the wire when encrypting,
        // and hand ciphertext to the decoder as if it were plaintext when decrypting.
        // Both delegates are set after construction, so this window is real.
        if (encrypt ? m_encryptData == nullptr : m_decryptData == nullptr)
        {
            return {};
        }

        auto data = winrt::single_threaded_vector<uint8_t>(std::vector<uint8_t>(message));

        if (encrypt)
        {
            data = m_encryptData(m_isScreencast ? VoipDataChannel::ScreenSharing : VoipDataChannel::Main, data, unencryptedPrefixSize);
        }
        else
        {
            data = m_decryptData(userId, data);
        }

        if (data == nullptr)
        {
            return {};
        }

        std::vector<uint8_t> result;
        result.reserve(data.Size());

        for (auto const& value : data)
        {
            result.push_back(value);
        }

        return result;
    }



    void VoipGroupManager::SetEncryptDecrypt(EncryptGroupCallDataDelegate encryptData, DecryptGroupCallDataDelegate decryptData)
    {
        std::lock_guard const guard(m_lock);
        m_encryptData = encryptData;
        m_decryptData = decryptData;
    }


    winrt::event_token VoipGroupManager::NetworkStateUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::GroupNetworkStateChangedEventArgs> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_networkStateUpdated.add(value);
    }

    void VoipGroupManager::NetworkStateUpdated(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_networkStateUpdated.remove(token);
    }



    winrt::event_token VoipGroupManager::AudioLevelsUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        IVector<winrt::Telegram::Native::Calls::VoipGroupParticipant>> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_audioLevelsUpdated.add(value);
    }

    void VoipGroupManager::AudioLevelsUpdated(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_audioLevelsUpdated.remove(token);
    }



    winrt::event_token VoipGroupManager::AudioBroadcastPartRequested(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::AudioBroadcastPartRequestedEventArgs> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_audioBroadcastPartRequested.add(value);
    }

    void VoipGroupManager::AudioBroadcastPartRequested(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_audioBroadcastPartRequested.remove(token);
    }



    winrt::event_token VoipGroupManager::VideoBroadcastPartRequested(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::VideoBroadcastPartRequestedEventArgs> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_videoBroadcastPartRequested.add(value);
    }

    void VoipGroupManager::VideoBroadcastPartRequested(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_videoBroadcastPartRequested.remove(token);
    }



    winrt::event_token VoipGroupManager::BroadcastTimeRequested(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::BroadcastTimeRequestedEventArgs> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_broadcastTimeRequested.add(value);
    }

    void VoipGroupManager::BroadcastTimeRequested(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_broadcastTimeRequested.remove(token);
    }



    winrt::event_token VoipGroupManager::MediaChannelDescriptionsRequested(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::MediaChannelDescriptionsRequestedEventArgs> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_mediaChannelDescriptionsRequested.add(value);
    }

    void VoipGroupManager::MediaChannelDescriptionsRequested(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_mediaChannelDescriptionsRequested.remove(token);
    }
}
