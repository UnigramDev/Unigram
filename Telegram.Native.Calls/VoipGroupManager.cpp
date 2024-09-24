﻿#include "pch.h"
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

        tgcalls::GroupInstanceDescriptor impl = tgcalls::GroupInstanceDescriptor();
        impl.threads = tgcalls::StaticThreads::getThreads();
        impl.config = config;
        impl.networkStateUpdated = [this](tgcalls::GroupNetworkState state) {
            auto args = winrt::make_self<GroupNetworkStateChangedEventArgs>(state.isConnected, state.isTransitioningFromBroadcastToRtc);
            m_networkStateUpdated(*this, *args);
            };
        impl.audioLevelsUpdated = [this](tgcalls::GroupLevelsUpdate const& levels) {
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
            };
        impl.initialInputDeviceId = winrt::to_string(descriptor.AudioInputId());
        impl.initialOutputDeviceId = winrt::to_string(descriptor.AudioOutputId());
        impl.initialEnableNoiseSuppression = m_isNoiseSuppressionEnabled = descriptor.IsNoiseSuppressionEnabled();
        impl.videoContentType = (tgcalls::VideoContentType)descriptor.VideoContentType();

        if (auto videoCapture = descriptor.VideoCapture())
        {
            if (auto screen = videoCapture.try_as<winrt::default_interface<VoipScreenCapture>>())
            {
                auto implementation = winrt::get_self<VoipScreenCapture>(screen);
                impl.videoCapture = implementation->m_impl;
            }
            else if (auto video = videoCapture.try_as<winrt::default_interface<VoipVideoCapture>>())
            {
                auto implementation = winrt::get_self<VoipVideoCapture>(video);
                impl.videoCapture = implementation->m_impl;
            }
        }

        impl.requestCurrentTime = [this](std::function<void(int64_t)> done) {
            auto task = std::make_shared<BroadcastTimeTaskImpl>(std::move(done));
            auto args = winrt::make_self<BroadcastTimeRequestedEventArgs>([task](int64_t time) { task->done(time); });

            m_broadcastTimeRequested(*this, *args);
            return task;
            };

        impl.requestVideoBroadcastPart = [this](int64_t time, int64_t period, int32_t channel, tgcalls::VideoChannelDescription::Quality quality, std::function<void(tgcalls::BroadcastPart&&)> done) {
            int scale = 0;
            switch (period)
            {
            case 1000: scale = 0; break;
            case 500: scale = 1; break;
            case 250: scale = 2; break;
            case 125: scale = 3; break;
            }

            Telegram::Td::Api::GroupCallVideoQuality qualityImpl;
            switch (quality)
            {
            case tgcalls::VideoChannelDescription::Quality::Thumbnail:
                qualityImpl = Telegram::Td::Api::GroupCallVideoQualityThumbnail();
                break;
            case tgcalls::VideoChannelDescription::Quality::Medium:
                qualityImpl = Telegram::Td::Api::GroupCallVideoQualityMedium();
                break;
            case tgcalls::VideoChannelDescription::Quality::Full:
                qualityImpl = Telegram::Td::Api::GroupCallVideoQualityFull();
                break;
            }

            auto task = std::make_shared<BroadcastPartTaskImpl>(time, scale, std::move(done));
            auto args = winrt::make_self<BroadcastPartRequestedEventArgs>(scale, time, channel, qualityImpl,
                [task](int64_t time, int64_t response, FilePart filePart) { task->done(time, response, filePart); });

            m_broadcastPartRequested(*this, *args);
            return task;
            };

        impl.requestAudioBroadcastPart = [this](int64_t time, int64_t period, std::function<void(tgcalls::BroadcastPart&&)> done) {
            int scale = 0;
            switch (period)
            {
            case 1000: scale = 0; break;
            case 500: scale = 1; break;
            case 250: scale = 2; break;
            case 125: scale = 3; break;
            }

            auto task = std::make_shared<BroadcastPartTaskImpl>(time, scale, std::move(done));
            auto args = winrt::make_self<BroadcastPartRequestedEventArgs>(scale, time, 0, nullptr,
                [task](int64_t time, int64_t response, FilePart filePart) { task->done(time, response, filePart); });

            m_broadcastPartRequested(*this, *args);
            return task;
            };

        m_impl = std::make_unique<tgcalls::GroupInstanceCustomImpl>(std::move(impl));
    }

    void VoipGroupManager::Close()
    {
        if (m_impl)
        {
            m_impl->stop();
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
            auto impl = std::vector<uint32_t>();

            for (const uint32_t& x : ssrcs)
            {
                impl.push_back(x);
            }

            m_impl->removeSsrcs(impl);
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
            if (videoCapture)
            {
                if (auto screen = videoCapture.try_as<winrt::default_interface<VoipScreenCapture>>())
                {
                    auto implementation = winrt::get_self<VoipScreenCapture>(screen);
                    m_impl->setVideoCapture(implementation->m_impl);
                }
                else if (auto video = videoCapture.try_as<winrt::default_interface<VoipVideoCapture>>())
                {
                    auto implementation = winrt::get_self<VoipVideoCapture>(video);
                    m_impl->setVideoCapture(implementation->m_impl);
                }
            }
            else
            {
                m_impl->setVideoCapture(nullptr);
            }
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
        if (m_impl)
        {
            m_impl->setVolume(ssrc, volume);
        }
    }

    void VoipGroupManager::SetRequestedVideoChannels(IVector<VoipVideoChannelInfo> descriptions)
    {
        if (m_impl)
        {
            auto impl = std::vector<tgcalls::VideoChannelDescription>();

            for (const VoipVideoChannelInfo& x : descriptions)
            {
                tgcalls::VideoChannelDescription item;
                item.audioSsrc = x.AudioSource();
                item.endpointId = winrt::to_string(x.EndpointId());
                item.minQuality = (tgcalls::VideoChannelDescription::Quality)x.MinQuality();
                item.maxQuality = (tgcalls::VideoChannelDescription::Quality)x.MaxQuality();

                for (const GroupCallVideoSourceGroup& group : x.SourceGroups())
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



    winrt::event_token VoipGroupManager::BroadcastPartRequested(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipGroupManager,
        winrt::Telegram::Native::Calls::BroadcastPartRequestedEventArgs> const& value)
    {
        return m_broadcastPartRequested.add(value);
    }

    void VoipGroupManager::BroadcastPartRequested(winrt::event_token const& token)
    {
        m_broadcastPartRequested.remove(token);
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
}
