#include "pch.h"
#include "VoipManager.h"
#include "VoipManager.g.cpp"

#include <stddef.h>
#include <memory>

#include "VoipVideoRendererToken.h"

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
    VoipManager::VoipManager(hstring version, VoipDescriptor descriptor)
    {
        m_version = version;
        m_descriptor = descriptor;
    }

    void VoipManager::Close()
    {
        m_descriptor = nullptr;
        m_capturer.reset();
        m_impl.reset();
    }

    inline std::string hexStr(IVector<uint8_t> data)
    {
        std::stringstream ss;
        ss << std::hex;

        for (const uint8_t& x : data)
        {
            ss << std::setw(2) << std::setfill('0') << (int)x;
        }

        return ss.str();
    }

    void VoipManager::Start()
    {
        auto logPath = Windows::Storage::ApplicationData::Current().LocalFolder().Path();
        logPath = logPath + hstring(L"\\tgcalls.txt");

        tgcalls::Config config = tgcalls::Config
        {
            .initializationTimeout = m_descriptor.InitializationTimeout(),
            .receiveTimeout = m_descriptor.ReceiveTimeout(),
            .dataSaving = tgcalls::DataSaving::Never,
            .enableP2P = m_descriptor.EnableP2p(),
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
            .protocolVersion = tgcalls::ProtocolVersion::V1
        };

        tgcalls::MediaDevicesConfig mediaConfig = {
            .audioInputId = winrt::to_string(m_descriptor.AudioInputId()),
            .audioOutputId = winrt::to_string(m_descriptor.AudioOutputId()),
            .inputVolume = 1.f,
            .outputVolume = 1.f
        };

        std::vector<uint8_t> persistentState = {};
        if (m_descriptor.PersistentState())
        {
            for (int i = 0; i < m_descriptor.PersistentState().Size(); i++)
            {
                persistentState[i] = m_descriptor.PersistentState().GetAt(i);
            }
        }

        std::array<uint8_t, 256> encryptionKey = {};
        for (int i = 0; i < 256; i++)
        {
            encryptionKey[i] = m_descriptor.EncryptionKey().GetAt(i);
        }

        std::shared_ptr<std::array<uint8_t, 256>> encryptionKeyPointer
            = std::make_shared<std::array<uint8_t, 256>>(encryptionKey);

        auto rtc = std::vector<tgcalls::RtcServer>();
        auto ids = std::vector<long>();

        for (const CallServer& x : m_descriptor.Servers())
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

        for (const CallServer& x : m_descriptor.Servers())
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

        if (m_descriptor.VideoCapture())
        {
            auto implementation = winrt::get_self<implementation::VoipVideoCapture>(m_descriptor.VideoCapture());
            m_capturer = implementation->m_impl;
        }

        tgcalls::Descriptor descriptor = tgcalls::Descriptor
        {
            .version = winrt::to_string(m_version),
            .config = config,
            .persistentState = persistentState,
            .endpoints = std::vector<tgcalls::Endpoint>(),
            .proxy = NULL,
            .rtcServers = rtc,
            .initialNetworkType = tgcalls::NetworkType(),
            .encryptionKey = tgcalls::EncryptionKey(encryptionKeyPointer, m_descriptor.IsOutgoing()),
            .mediaDevicesConfig = mediaConfig,
            .videoCapture = m_capturer,
            .stateUpdated = [this](tgcalls::State state) {
                m_stateUpdatedEventSource(*this, (VoipState)state);
            },
            .signalBarsUpdated = [this](int signalBars) {
                m_signalBarsUpdatedEventSource(*this, signalBars);
            },
            .audioLevelUpdated = [this](float level) {
                m_audioLevelUpdated(*this, level);
            },
            .remoteBatteryLevelIsLowUpdated = [this](bool low) {
                m_remoteBatteryLevelIsLowUpdatedEventSource(*this, low);
            },
            .remoteMediaStateUpdated = [this](tgcalls::AudioState audio, tgcalls::VideoState video) {
                auto args = winrt::make_self<winrt::Telegram::Native::Calls::implementation::RemoteMediaStateUpdatedEventArgs>((VoipAudioState)audio, (VoipVideoState)video);
                m_remoteMediaStateUpdatedEventSource(*this, *args);
            },
            .remotePrefferedAspectRatioUpdated = [this](float aspect) {
                m_remotePrefferedAspectRatioUpdatedEventSource(*this, aspect);
            },
            .signalingDataEmitted = [this](std::vector<uint8_t> data) {
                auto bytes = winrt::single_threaded_vector<uint8_t>(std::move(data));
                auto args = winrt::make_self<winrt::Telegram::Native::Calls::implementation::SignalingDataEmittedEventArgs>(bytes);
                m_signalingDataEmittedEventSource(*this, *args);
            }
        };

        m_impl = tgcalls::Meta::Create(winrt::to_string(m_version), std::move(descriptor));
        //impl->setVideoCapture(capturer);
    }


    //void VoipManager::SetNetworkType(NetworkType networkType);

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

    //void SetIncomingVideoOutput(std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> sink);
    winrt::Telegram::Native::Calls::VoipVideoRendererToken VoipManager::SetIncomingVideoOutput(winrt::Microsoft::Graphics::Canvas::UI::Xaml::CanvasControl canvas)
    {
        if (m_impl)
        {
            if (canvas != nullptr)
            {
                m_renderer = std::make_shared<VoipVideoRenderer>(canvas, false, false);
                m_impl->setIncomingVideoOutput(m_renderer);
                return *winrt::make_self<VoipVideoRendererToken>(m_renderer, canvas);
            }
            else
            {
                m_renderer.reset();
            }
        }

        return nullptr;
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
                    m_capturer = implementation->m_impl;
                }
                else if (auto video = videoCapture.try_as<winrt::default_interface<VoipVideoCapture>>())
                {
                    auto implementation = winrt::get_self<VoipVideoCapture>(video);
                    m_capturer = implementation->m_impl;
                }
            }
            else
            {
                m_capturer = nullptr;
            }

            m_impl->setVideoCapture(m_capturer);
        }
    }

    void VoipManager::SetRequestedVideoAspect(float aspect)
    {
        if (m_impl)
        {
            m_impl->setRequestedVideoAspect(aspect);
        }
    }



    winrt::event_token VoipManager::StateUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        VoipState> const& value)
    {
        return m_stateUpdatedEventSource.add(value);
    }

    void VoipManager::StateUpdated(winrt::event_token const& token)
    {
        m_stateUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::SignalBarsUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        int> const& value)
    {
        return m_signalBarsUpdatedEventSource.add(value);
    }

    void VoipManager::SignalBarsUpdated(winrt::event_token const& token)
    {
        m_signalBarsUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::AudioLevelUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        float> const& value)
    {
        return m_audioLevelUpdated.add(value);
    }

    void VoipManager::AudioLevelUpdated(winrt::event_token const& token)
    {
        m_audioLevelUpdated.remove(token);
    }



    winrt::event_token VoipManager::RemoteBatteryLevelIsLowUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        bool> const& value)
    {
        return m_remoteBatteryLevelIsLowUpdatedEventSource.add(value);
    }

    void VoipManager::RemoteBatteryLevelIsLowUpdated(winrt::event_token const& token)
    {
        m_remoteBatteryLevelIsLowUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::RemoteMediaStateUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        winrt::Telegram::Native::Calls::RemoteMediaStateUpdatedEventArgs> const& value)
    {
        return m_remoteMediaStateUpdatedEventSource.add(value);
    }

    void VoipManager::RemoteMediaStateUpdated(winrt::event_token const& token)
    {
        m_remoteMediaStateUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::RemotePrefferedAspectRatioUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        float> const& value)
    {
        return m_remotePrefferedAspectRatioUpdatedEventSource.add(value);
    }

    void VoipManager::RemotePrefferedAspectRatioUpdated(winrt::event_token const& token)
    {
        m_remotePrefferedAspectRatioUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::SignalingDataEmitted(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        winrt::Telegram::Native::Calls::SignalingDataEmittedEventArgs> const& value)
    {
        return m_signalingDataEmittedEventSource.add(value);
    }

    void VoipManager::SignalingDataEmitted(winrt::event_token const& token)
    {
        m_signalingDataEmittedEventSource.remove(token);
    }
} // namespace winrt::Telegram::Native::Calls::implementation
