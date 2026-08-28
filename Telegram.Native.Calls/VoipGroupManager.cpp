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
        // Sharing a screen runs a second manager alongside the main one, and both would
        // otherwise interleave into the same file.
        auto logPath = Windows::Storage::ApplicationData::Current().LocalFolder().Path()
            + (VoipVideoContentType::Screencast == descriptor.VideoContentType()
                ? hstring(L"\\tgcalls_screencast.txt")
                : hstring(L"\\tgcalls_group.txt"));

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

            m_loopback = winrt::make_self<VoipLoopbackCapture>([weakThis{ get_weak() }](std::vector<uint8_t>&& samples) {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->AddExternalAudioSamples(std::move(samples));
                }
                });

            // -1 means everything this process is not rendering, i.e. the whole system
            // minus ourselves; anything else is that process and its children.
            auto result = audioProcessId == -1
                ? m_loopback->Start(GetCurrentProcessId(), false)
                : m_loopback->Start(static_cast<uint32_t>(audioProcessId), true);

            if (FAILED(result))
            {
                // Process loopback is unavailable on older builds; share the screen
                // without its audio rather than failing the whole capture.
                m_loopback = nullptr;
                return;
            }

            IsMuted(false);
        }
    }

    VoipGroupManager::~VoipGroupManager()
    {
        Stop();
    }

    void VoipGroupManager::Stop()
    {
        if (m_loopback)
        {
            m_loopback->Stop();
            m_loopback = nullptr;
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

        // Nothing to join with once the instance is gone. Completing with an empty
        // payload only got the caller as far as a join the server would reject, and it
        // did it on this thread rather than the one the success path answers on.
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
        if (m_impl == nullptr)
        {
            return;
        }

        auto impl = std::vector<tgcalls::VideoChannelDescription>();
        impl.reserve(descriptions.Size());

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

        m_impl->setRequestedVideoChannels(std::move(impl));
    }



    void VoipGroupManager::OnNetworkStateUpdated(tgcalls::GroupNetworkState state)
    {
        auto args = winrt::make_self<GroupNetworkStateChangedEventArgs>(state.isConnected, state.isTransitioningFromBroadcastToRtc);
        m_networkStateUpdated(*this, *args);
    }

    void VoipGroupManager::OnAudioLevelsUpdated(tgcalls::GroupLevelsUpdate const& levels)
    {
        // Filled first and handed over whole: appending to the IVector would be a COM
        // call per participant, and this arrives about ten times a second for the whole
        // length of the call.
        auto participants = std::vector<winrt::Telegram::Native::Calls::VoipGroupParticipant>();
        participants.reserve(levels.updates.size());

        for (const tgcalls::GroupLevelUpdate& x : levels.updates)
        {
            participants.push_back(winrt::Telegram::Native::Calls::VoipGroupParticipant{
                .AudioSource = static_cast<int32_t>(x.ssrc),
                    .Level = x.value.level,
                    .IsSpeaking = x.value.voice,
                    .IsMuted = x.value.isMuted
                });
        }

        m_audioLevelsUpdated(*this, winrt::single_threaded_vector(std::move(participants)));
    }

    std::shared_ptr<tgcalls::BroadcastPartTask> VoipGroupManager::OnRequestCurrentTime(std::function<void(int64_t)> done)
    {
        auto task = std::make_shared<BroadcastTimeTaskImpl>(std::move(done));
        auto args = winrt::make_self<BroadcastTimeRequestedEventArgs>([task](int64_t time) { task->done(time); });

        m_broadcastTimeRequested(*this, *args);
        return task;
    }

    std::shared_ptr<tgcalls::BroadcastPartTask> VoipGroupManager::OnRequestVideoBroadcastPart(int64_t time, int64_t period, int32_t channel, tgcalls::VideoChannelDescription::Quality quality, std::function<void(tgcalls::BroadcastPart&&)> done)
    {
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
            [task](int64_t time, int64_t response, array_view<uint8_t const> data) { task->done(time, response, data); });

        m_videoBroadcastPartRequested(*this, *args);
        return task;
    }

    std::shared_ptr<tgcalls::BroadcastPartTask> VoipGroupManager::OnRequestAudioBroadcastPart(int64_t time, int64_t period, std::function<void(tgcalls::BroadcastPart&&)> done)
    {
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
            [task](int64_t time, int64_t response, array_view<uint8_t const> data) { task->done(time, response, data); });

        m_audioBroadcastPartRequested(*this, *args);
        return task;
    }

    std::shared_ptr<tgcalls::RequestMediaChannelDescriptionTask> VoipGroupManager::OnRequestMediaChannelDescriptions(const std::vector<uint32_t>& ssrcs, std::function<void(std::vector<tgcalls::MediaChannelDescription>&&)> done)
    {

        auto audioSourceIds = winrt::single_threaded_vector<uint32_t>(std::vector<uint32_t>(ssrcs));

        auto task = std::make_shared<RequestMediaChannelDescriptionTaskImpl>(std::move(done));
        auto args = winrt::make_self<MediaChannelDescriptionsRequestedEventArgs>(audioSourceIds,
            [task](IVector<VoipMediaChannelDescription> participants) { task->done(participants); });

        m_mediaChannelDescriptionsRequested(*this, *args);
        return task;
    }

    std::vector<uint8_t> VoipGroupManager::OnE2EEncryptDecrypt(std::vector<uint8_t> const& message, int64_t userId, bool encrypt, int32_t unencryptedPrefixSize)
    {
        // Taken by copy rather than held: this runs per frame on a media thread, and the
        // managed delegates block on a TDLib round trip. The lock covers only the read.
        EncryptGroupCallDataDelegate encryptData{ nullptr };
        DecryptGroupCallDataDelegate decryptData{ nullptr };

        {
            std::lock_guard const guard(m_encryptLock);
            encryptData = m_encryptData;
            decryptData = m_decryptData;
        }

        // An empty result is how tgcalls is told the frame failed and must be dropped.
        // Returning the input untouched would put plaintext on the wire when encrypting,
        // and hand ciphertext to the decoder as if it were plaintext when decrypting.
        if (encrypt ? encryptData == nullptr : decryptData == nullptr)
        {
            return {};
        }

        // A view over the caller's buffer, so nothing is copied on this side: the
        // projection marshals it straight into the managed array.
        auto view = array_view<uint8_t const>(message.data(), message.data() + message.size());

        auto data = encrypt
            ? encryptData(m_isScreencast ? VoipDataChannel::ScreenSharing : VoipDataChannel::Main, view, unencryptedPrefixSize)
            : decryptData(userId, view);

        return std::vector<uint8_t>(data.begin(), data.end());
    }



    void VoipGroupManager::SetEncryptDecrypt(EncryptGroupCallDataDelegate encryptData, DecryptGroupCallDataDelegate decryptData)
    {
        std::lock_guard const guard(m_encryptLock);
        m_encryptData = encryptData;
        m_decryptData = decryptData;
    }


    winrt::event_token VoipGroupManager::NetworkStateUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::GroupNetworkStateChangedEventArgs> const& value)
    {
        return m_networkStateUpdated.add(value);
    }

    void VoipGroupManager::NetworkStateUpdated(winrt::event_token const& token)
    {
        m_networkStateUpdated.remove(token);
    }



    winrt::event_token VoipGroupManager::AudioLevelsUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        IVector<winrt::Telegram::Native::Calls::VoipGroupParticipant>> const& value)
    {
        return m_audioLevelsUpdated.add(value);
    }

    void VoipGroupManager::AudioLevelsUpdated(winrt::event_token const& token)
    {
        m_audioLevelsUpdated.remove(token);
    }



    winrt::event_token VoipGroupManager::AudioBroadcastPartRequested(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::AudioBroadcastPartRequestedEventArgs> const& value)
    {
        return m_audioBroadcastPartRequested.add(value);
    }

    void VoipGroupManager::AudioBroadcastPartRequested(winrt::event_token const& token)
    {
        m_audioBroadcastPartRequested.remove(token);
    }



    winrt::event_token VoipGroupManager::VideoBroadcastPartRequested(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::VideoBroadcastPartRequestedEventArgs> const& value)
    {
        return m_videoBroadcastPartRequested.add(value);
    }

    void VoipGroupManager::VideoBroadcastPartRequested(winrt::event_token const& token)
    {
        m_videoBroadcastPartRequested.remove(token);
    }



    winrt::event_token VoipGroupManager::BroadcastTimeRequested(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::BroadcastTimeRequestedEventArgs> const& value)
    {
        return m_broadcastTimeRequested.add(value);
    }

    void VoipGroupManager::BroadcastTimeRequested(winrt::event_token const& token)
    {
        m_broadcastTimeRequested.remove(token);
    }



    winrt::event_token VoipGroupManager::MediaChannelDescriptionsRequested(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::MediaChannelDescriptionsRequestedEventArgs> const& value)
    {
        return m_mediaChannelDescriptionsRequested.add(value);
    }

    void VoipGroupManager::MediaChannelDescriptionsRequested(winrt::event_token const& token)
    {
        m_mediaChannelDescriptionsRequested.remove(token);
    }
}
