#include "pch.h"
#include "VoipManager.h"
#include "VoipManager.g.cpp"

#include <stddef.h>
#include <memory>

#include "VoipVideoOutputSink.h"

#include "api/media_stream_interface.h"
#include "api/create_peerconnection_factory.h"
#include "api/peer_connection_interface.h"
#include "api/audio_codecs/builtin_audio_decoder_factory.h"
#include "api/audio_codecs/builtin_audio_encoder_factory.h"
#include "api/video_codecs/builtin_video_decoder_factory.h"
#include "api/video_codecs/builtin_video_encoder_factory.h"
#include "pc/video_track_source.h"
#include "rtc_base/rtc_certificate_generator.h"
#include "rtc_base/ssl_adapter.h"

#include "api/video/i420_buffer.h"
#include "modules/video_capture/video_capture_factory.h"
#include "modules/video_capture/windows/device_info_winrt.h"
#include "libyuv.h"

#include "api/video/video_frame.h"
#include "api/video/video_source_interface.h"
#include "media/base/video_adapter.h"
#include "media/base/video_broadcaster.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    inline static std::string hexStr(IVector<uint8_t> data)
    {
        std::stringstream ss;
        ss << std::hex;

        for (const uint8_t& x : data)
        {
            ss << std::setw(2) << std::setfill('0') << (int)x;
        }

        return ss.str();
    }

    void VoipManager::Start(VoipDescriptor descriptor)
    {
        auto logPath = Windows::Storage::ApplicationData::Current().LocalFolder().Path();
        logPath = logPath + hstring(L"\\tgcalls.txt");

        tgcalls::Config config = tgcalls::Config
        {
            .initializationTimeout = descriptor.InitializationTimeout(),
            .receiveTimeout = descriptor.ReceiveTimeout(),
            .dataSaving = tgcalls::DataSaving::Never,
            .enableP2P = descriptor.EnableP2p(),
            .allowTCP = false,
            .enableStunMarking = false,
            .enableAEC = true,
            .enableNS = true,
            .enableAGC = true,
            .enableCallUpgrade = false,
            .enableVolumeControl = false,
        #ifndef _WIN32
            std::string logPath;
            std::string statsLogPath;
        #else
            .logPath = logPath.data(),
            .statsLogPath = L"",
        #endif
            .maxApiLayer = 92,
            .enableHighBitrateVideo = false,
            .preferredVideoCodecs = std::vector<std::string>(),
            .protocolVersion = tgcalls::ProtocolVersion::V1,
            .customParameters = winrt::to_string(descriptor.CustomParameters())
        };

        tgcalls::MediaDevicesConfig mediaConfig = {
            .audioInputId = winrt::to_string(descriptor.AudioInputId()),
            .audioOutputId = winrt::to_string(descriptor.AudioOutputId()),
            .inputVolume = 1.f,
            .outputVolume = 1.f
        };

        std::vector<uint8_t> persistentState = {};
        if (descriptor.PersistentState())
        {
            for (int i = 0; i < descriptor.PersistentState().Size(); i++)
            {
                persistentState[i] = descriptor.PersistentState().GetAt(i);
            }
        }

        std::array<uint8_t, 256> encryptionKey = {};
        for (int i = 0; i < 256; i++)
        {
            encryptionKey[i] = descriptor.EncryptionKey().GetAt(i);
        }

        std::shared_ptr<std::array<uint8_t, 256>> encryptionKeyPointer
            = std::make_shared<std::array<uint8_t, 256>>(encryptionKey);

        auto rtc = std::vector<tgcalls::RtcServer>();
        auto ids = std::vector<long>();

        for (const CallServer& x : descriptor.Servers())
        {
            if (auto webRtc = x.Type().try_as<CallServerTypeWebrtc>())
            {
                const auto host = winrt::to_string(x.IpAddress());
                const auto hostv6 = winrt::to_string(x.Ipv6Address());
                const auto port = uint16_t(x.Port());
                if (webRtc.SupportsStun())
                {
                    const auto pushStun = [&](const std::string& host) {
                        if (host.empty())
                        {
                            return;
                        }
                        tgcalls::RtcServer server;
                        server.host = host;
                        server.port = port;
                        server.isTurn = false;
                        rtc.push_back(server);
                        };
                    pushStun(host);
                    pushStun(hostv6);
                }
                const auto username = winrt::to_string(webRtc.Username());
                const auto password = winrt::to_string(webRtc.Password());
                if (webRtc.SupportsTurn() && !username.empty() && !password.empty())
                {
                    const auto pushTurn = [&](const std::string& host) {
                        tgcalls::RtcServer server;
                        server.host = host;
                        server.port = port;
                        server.login = username;
                        server.password = password;
                        server.isTurn = true;
                        rtc.push_back(server);
                        };
                    pushTurn(host);
                    pushTurn(hostv6);
                }
            }
            else if (auto reflector = x.Type().try_as<CallServerTypeTelegramReflector>())
            {
                ids.push_back(x.Id());
            }
        }

        std::sort(ids.begin(), ids.end());

        for (const CallServer& x : descriptor.Servers())
        {
            if (auto reflector = x.Type().try_as<CallServerTypeTelegramReflector>())
            {
                const auto reflectorId = std::find(ids.begin(), ids.end(), x.Id()) - ids.begin();
                const auto host = winrt::to_string(x.IpAddress());
                const auto port = uint16_t(x.Port());
                tgcalls::RtcServer server;
                server.id = reflectorId;
                server.host = host;
                server.port = port;
                server.login = "reflector";
                server.password = hexStr(reflector.PeerTag());
                server.isTurn = true;
                server.isTcp = reflector.IsTcp();
                rtc.push_back(server);
            }
        }

        tgcalls::Descriptor descriptorImpl = tgcalls::Descriptor
        {
            .version = winrt::to_string(descriptor.Version()),
            .config = config,
            .persistentState = persistentState,
            .endpoints = std::vector<tgcalls::Endpoint>(),
            .proxy = NULL,
            .rtcServers = rtc,
            .initialNetworkType = tgcalls::NetworkType(),
            .encryptionKey = tgcalls::EncryptionKey(encryptionKeyPointer, descriptor.IsOutgoing()),
            .mediaDevicesConfig = mediaConfig,
            .stateUpdated = [weakThis{ get_weak() }](tgcalls::State state) {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->OnStateUpdated(state);
                }
            },
            .signalBarsUpdated = [weakThis{ get_weak() }](int signalBars) {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->OnSignalBarsUpdated(signalBars);
                }
            },
            .audioLevelUpdated = [weakThis{ get_weak() }](float level) {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->OnAudioLevelUpdated(level);
                }
            },
            .remoteBatteryLevelIsLowUpdated = [weakThis{ get_weak() }](bool low) {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->OnRemoteBatteryLevelIsLowUpdated(low);
                }
            },
            .remoteMediaStateUpdated = [weakThis{ get_weak() }](tgcalls::AudioState audio, tgcalls::VideoState video) {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->OnRemoteMediaStateUpdated(audio, video);
                }
            },
            .remotePrefferedAspectRatioUpdated = [weakThis{ get_weak() }](float aspect) {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->OnRemotePrefferedAspectRadioUpdated(aspect);
                }
            },
            .signalingDataEmitted = [weakThis{ get_weak() }](std::vector<uint8_t> data) {
                if (auto strongThis = weakThis.get())
                {
                    strongThis->OnSignalingDataEmitted(data);
                }
            }
        };

        if (descriptor.VideoCapture())
        {
            auto implementation = winrt::get_self<implementation::VoipVideoCapture>(descriptor.VideoCapture());
            descriptorImpl.videoCapture = implementation->m_impl;
        }

        m_impl = tgcalls::Meta::Create(descriptorImpl.version, std::move(descriptorImpl));
    }

    void VoipManager::Stop()
    {
        if (m_impl)
        {
            m_impl->stop([](tgcalls::FinalState) {});
            m_impl.reset();
        }
    }

    bool VoipManager::IsMuted()
    {
        return m_isMuted;
    }

    void VoipManager::IsMuted(bool muteMicrophone)
    {
        if (m_impl)
        {
            m_impl->setMuteMicrophone(m_isMuted = muteMicrophone);
        }
        else
        {
            m_isMuted = muteMicrophone;
        }
    }

    void VoipManager::SetAudioOutputGainControlEnabled(bool enabled)
    {
        if (m_impl)
        {
            m_impl->setAudioOutputGainControlEnabled(enabled);
        }
    }

    void VoipManager::SetEchoCancellationStrength(int strength)
    {
        if (m_impl)
        {
            m_impl->setEchoCancellationStrength(strength);
        }
    }

    bool VoipManager::SupportsVideo()
    {
        if (m_impl)
        {
            return m_impl->supportsVideo();
        }

        return false;
    }

    void VoipManager::SetIncomingVideoOutput(winrt::Telegram::Native::Calls::VoipVideoOutputSink sink)
    {
        if (m_impl && sink)
        {
            auto implementation = winrt::get_self<VoipVideoOutputSink>(sink);
            m_impl->setIncomingVideoOutput(implementation->Sink());
        }
    }



    void VoipManager::SetAudioInputDevice(hstring id)
    {
        if (m_impl)
        {
            m_impl->setAudioInputDevice(winrt::to_string(id));
        }
    }

    void VoipManager::SetAudioOutputDevice(hstring id)
    {
        if (m_impl)
        {
            m_impl->setAudioOutputDevice(winrt::to_string(id));
        }
    }

    void VoipManager::SetInputVolume(float level)
    {
        if (m_impl)
        {
            m_impl->setInputVolume(level);
        }
    }

    void VoipManager::SetOutputVolume(float level)
    {
        if (m_impl)
        {
            m_impl->setOutputVolume(level);
        }
    }

    void VoipManager::SetAudioOutputDuckingEnabled(bool enabled)
    {
        if (m_impl)
        {
            m_impl->setAudioOutputDuckingEnabled(enabled);
        }
    }

    void VoipManager::SetIsLowBatteryLevel(bool isLowBatteryLevel)
    {
        if (m_impl)
        {
            m_impl->setIsLowBatteryLevel(isLowBatteryLevel);
        }
    }



    //std::string getLastError();
    hstring VoipManager::GetDebugInfo()
    {
        if (m_impl)
        {
            std::string log = m_impl->getDebugInfo();
            size_t len = sizeof(wchar_t) * (log.length() + 1);
            wchar_t* wlog = (wchar_t*)malloc(len);
            MultiByteToWideChar(CP_UTF8, 0, log.c_str(), -1, wlog, len / sizeof(wchar_t));
            return hstring(wlog);
            return winrt::to_hstring(m_impl->getDebugInfo());
        }

        return L"";
    }

    int64_t VoipManager::GetPreferredRelayId()
    {
        if (m_impl)
        {
            return m_impl->getPreferredRelayId();
        }

        return -1;
    }
    //TrafficStats getTrafficStats();
    //PersistentState getPersistentState();



    void VoipManager::ReceiveSignalingData(IVector<uint8_t> const data)
    {
        if (m_impl)
        {
            auto bytes = std::vector<unsigned char>();
            for (const uint8_t& x : data)
            {
                bytes.push_back(x);
            }

            m_impl->receiveSignalingData(bytes);
        }
    }

    void VoipManager::SetVideoCapture(Telegram::Native::Calls::VoipCaptureBase videoCapture)
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

    void VoipManager::SetRequestedVideoAspect(float aspect)
    {
        if (m_impl)
        {
            m_impl->setRequestedVideoAspect(aspect);
        }
    }



    void VoipManager::OnStateUpdated(tgcalls::State state)
    {
        std::lock_guard const guard(m_lock);
        m_stateUpdatedEventSource(*this, (VoipReadyState)state);
    }

    void VoipManager::OnSignalBarsUpdated(int signalBars)
    {
        std::lock_guard const guard(m_lock);
        m_signalBarsUpdatedEventSource(*this, signalBars);
    }

    void VoipManager::OnAudioLevelUpdated(float level)
    {
        std::lock_guard const guard(m_lock);
        m_audioLevelUpdatedEventSource(*this, level);
    }

    void VoipManager::OnRemoteBatteryLevelIsLowUpdated(bool low)
    {
        std::lock_guard const guard(m_lock);
        m_remoteBatteryLevelIsLowUpdatedEventSource(*this, low);
    }

    void VoipManager::OnRemoteMediaStateUpdated(tgcalls::AudioState audio, tgcalls::VideoState video)
    {
        std::lock_guard const guard(m_lock);
        auto args = winrt::make_self<RemoteMediaStateUpdatedEventArgs>((VoipAudioState)audio, (VoipVideoState)video);
        m_remoteMediaStateUpdatedEventSource(*this, *args);
    }

    void VoipManager::OnRemotePrefferedAspectRadioUpdated(float aspect)
    {
        std::lock_guard const guard(m_lock);
        m_remotePrefferedAspectRatioUpdatedEventSource(*this, aspect);
    }

    void VoipManager::OnSignalingDataEmitted(std::vector<uint8_t> data)
    {
        std::lock_guard const guard(m_lock);
        auto bytes = winrt::single_threaded_vector<uint8_t>(std::move(data));
        auto args = winrt::make_self<SignalingDataEmittedEventArgs>(bytes);
        m_signalingDataEmittedEventSource(*this, *args);
    }




    winrt::event_token VoipManager::StateUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        VoipReadyState> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_stateUpdatedEventSource.add(value);
    }

    void VoipManager::StateUpdated(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_stateUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::SignalBarsUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        int> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_signalBarsUpdatedEventSource.add(value);
    }

    void VoipManager::SignalBarsUpdated(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_signalBarsUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::AudioLevelUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        float> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_audioLevelUpdatedEventSource.add(value);
    }

    void VoipManager::AudioLevelUpdated(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_audioLevelUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::RemoteBatteryLevelIsLowUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        bool> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_remoteBatteryLevelIsLowUpdatedEventSource.add(value);
    }

    void VoipManager::RemoteBatteryLevelIsLowUpdated(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_remoteBatteryLevelIsLowUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::RemoteMediaStateUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        winrt::Telegram::Native::Calls::RemoteMediaStateUpdatedEventArgs> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_remoteMediaStateUpdatedEventSource.add(value);
    }

    void VoipManager::RemoteMediaStateUpdated(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_remoteMediaStateUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::RemotePrefferedAspectRatioUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        float> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_remotePrefferedAspectRatioUpdatedEventSource.add(value);
    }

    void VoipManager::RemotePrefferedAspectRatioUpdated(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_remotePrefferedAspectRatioUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::SignalingDataEmitted(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        winrt::Telegram::Native::Calls::SignalingDataEmittedEventArgs> const& value)
    {
        std::lock_guard const guard(m_lock);
        return m_signalingDataEmittedEventSource.add(value);
    }

    void VoipManager::SignalingDataEmitted(winrt::event_token const& token)
    {
        std::lock_guard const guard(m_lock);
        m_signalingDataEmittedEventSource.remove(token);
    }
} // namespace winrt::Telegram::Native::Calls::implementation
