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

        std::vector<uint8_t> persistentState;
        if (descriptor.PersistentState())
        {
            persistentState = vector_to_unmanaged<uint8_t, uint8_t>(descriptor.PersistentState());
        }

        std::shared_ptr<std::array<uint8_t, 256>> encryptionKeyPointer
            = std::make_shared<std::array<uint8_t, 256>>();

        if (auto encryptionKey = descriptor.EncryptionKey())
        {
            // One call rather than 256, and it clamps to what is there instead of
            // throwing hresult_out_of_bounds out of Start on a short key.
            encryptionKey.GetMany(0, *encryptionKeyPointer);
        }

        auto rtc = std::vector<tgcalls::RtcServer>();
        auto ids = std::vector<long>();

        for (const VoipCallServer& x : descriptor.Servers())
        {
            if (auto webRtc = x.Type().try_as<VoipCallServerTypeWebrtc>())
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
                        if (host.empty())
                        {
                            return;
                        }
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
            else if (auto reflector = x.Type().try_as<VoipCallServerTypeTelegramReflector>())
            {
                ids.push_back(x.Id());
            }
        }

        std::sort(ids.begin(), ids.end());

        for (const VoipCallServer& x : descriptor.Servers())
        {
            if (auto reflector = x.Type().try_as<VoipCallServerTypeTelegramReflector>())
            {
                const auto reflectorId = std::find(ids.begin(), ids.end(), x.Id()) - ids.begin();
                const auto host = winrt::to_string(x.IpAddress());
                const auto port = uint16_t(x.Port());
                tgcalls::RtcServer server;
                server.id = reflectorId;
                server.host = host;
                server.port = port;
                server.login = "reflector";
                server.password = winrt::to_string(reflector.PeerTag());
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

        // The descriptor carries whichever capture is active when the call turns ready,
        // which is a screen capture if the user started sharing before that happened.
        descriptorImpl.videoCapture = GetVideoCaptureImpl(descriptor.VideoCapture());

        m_impl = tgcalls::Meta::Create(descriptorImpl.version, std::move(descriptorImpl));
    }

    VoipManager::~VoipManager()
    {
        Stop();
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
        if (m_impl == nullptr || sink == nullptr)
        {
            return;
        }

        auto impl = winrt::get_self<VoipVideoOutputSink>(sink)->Sink();

        // tgcalls appends sinks and only drops them once the weak_ptr expires, so handing
        // it one it already holds renders every frame twice. The page re-sets the output
        // on each remote media state change, which is the same sink every time.
        if (m_incomingVideoOutput.lock() == impl)
        {
            return;
        }

        // Weak, so that VoipVideoOutputSink::Stop stays the way the output is detached.
        m_incomingVideoOutput = impl;
        m_impl->setIncomingVideoOutput(impl);
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
            auto bytes = std::vector<uint8_t>(data.Size());
            data.GetMany(0, bytes);

            m_impl->receiveSignalingData(bytes);
        }
    }

    void VoipManager::SetVideoCapture(Telegram::Native::Calls::VoipCaptureBase videoCapture)
    {
        if (m_impl)
        {
            m_impl->setVideoCapture(GetVideoCaptureImpl(videoCapture));
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
        m_stateUpdatedEventSource(*this, (VoipReadyState)state);
    }

    void VoipManager::OnSignalBarsUpdated(int signalBars)
    {
        m_signalBarsUpdatedEventSource(*this, signalBars);
    }

    void VoipManager::OnAudioLevelUpdated(float level)
    {
        m_audioLevelUpdatedEventSource(*this, level);
    }

    void VoipManager::OnRemoteBatteryLevelIsLowUpdated(bool low)
    {
        m_remoteBatteryLevelIsLowUpdatedEventSource(*this, low);
    }

    void VoipManager::OnRemoteMediaStateUpdated(tgcalls::AudioState audio, tgcalls::VideoState video)
    {
        auto args = winrt::make_self<RemoteMediaStateUpdatedEventArgs>((VoipAudioState)audio, (VoipVideoState)video);
        m_remoteMediaStateUpdatedEventSource(*this, *args);
    }

    void VoipManager::OnRemotePrefferedAspectRadioUpdated(float aspect)
    {
        m_remotePrefferedAspectRatioUpdatedEventSource(*this, aspect);
    }

    void VoipManager::OnSignalingDataEmitted(std::vector<uint8_t> data)
    {
        auto bytes = winrt::single_threaded_vector<uint8_t>(std::move(data));
        auto args = winrt::make_self<SignalingDataEmittedEventArgs>(bytes);
        m_signalingDataEmittedEventSource(*this, *args);
    }




    winrt::event_token VoipManager::StateUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Telegram::Native::Calls::VoipManager,
        VoipReadyState> const& value)
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
        return m_audioLevelUpdatedEventSource.add(value);
    }

    void VoipManager::AudioLevelUpdated(winrt::event_token const& token)
    {
        m_audioLevelUpdatedEventSource.remove(token);
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
