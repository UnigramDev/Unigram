// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// clang-format off
#include "pch.h"
#include "VoipManager.h"
#include "VoipManager.g.cpp"
// clang-format on

#include <stddef.h>

#include <memory>

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
#include "rtc_base/critical_section.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    VoipManager::VoipManager(VoipDescriptor descriptor)
    {
        m_descriptor = descriptor;
        tgcalls::Register<tgcalls::InstanceImpl>();
    }

    VoipManager::~VoipManager()
    {
        m_descriptor = nullptr;
        m_capturer = nullptr;
        m_impl = nullptr;
    }

    void VoipManager::Close() {
        m_descriptor = nullptr;
        m_capturer = nullptr;
        m_impl = nullptr;
    }

    void VoipManager::Start()
    {
        auto logPath = Windows::Storage::ApplicationData::Current().LocalFolder().Path();
        logPath = logPath + hstring(L"\\tgcalls.txt");

        tgcalls::Config config = tgcalls::Config
        {
            /*double initializationTimeout =*/ m_descriptor.InitializationTimeout(),
            /*double receiveTimeout =*/ m_descriptor.ReceiveTimeout(),
            /*DataSaving dataSaving =*/ tgcalls::DataSaving::Never,
            /*bool enableP2P =*/ false,
            /*bool allowTCP =*/ false,
            /*bool enableStunMarking =*/ false,
            /*bool enableAEC =*/ true,
            /*bool enableNS =*/ true,
            /*bool enableAGC =*/ true,
            /*bool enableCallUpgrade =*/ false,
            /*bool enableVolumeControl =*/ false,
        #ifndef _WIN32
            std::string logPath;
            std::string statsLogPath;
        #else
            /*std::wstring logPath*/ logPath.data(), //L"C:\\Users\\Fela\\AppData\\Local\\Packages\\4ad57552-10ee-4389-8171-0fcec0b928b3_k580v1fv7e4c6\\LocalState\\tgcalls.txt",
            /*std::wstring statsLogPath*/ L"",
        #endif
            /*int maxApiLayer =*/ 92,
            /*bool enableHighBitrateVideo =*/ false,
            /*std::vector<std::string> preferredVideoCodecs =*/std::vector<std::string>(),
            /*ProtocolVersion protocolVersion =*/ tgcalls::ProtocolVersion::V1
    };

        tgcalls::MediaDevicesConfig mediaConfig = {
            /*audioInputId =*/ string_to_unmanaged(m_descriptor.AudioInputId()),
            /*audioOutputId =*/ string_to_unmanaged(m_descriptor.AudioOutputId()),
            /*inputVolume =*/ 1.f,
            /*outputVolume =*/ 1.f
        };

        std::vector<uint8_t> persistentState = {};
        if (m_descriptor.PersistentState()) {
            for (int i = 0; i < m_descriptor.PersistentState().Size(); i++) {
                persistentState[i] = m_descriptor.PersistentState().GetAt(i);
            }
        }

        std::array<uint8_t, 256> encryptionKey = {};
        for (int i = 0; i < 256; i++) {
            encryptionKey[i] = m_descriptor.EncryptionKey().GetAt(i);
        }

        std::shared_ptr<std::array<uint8_t, 256>> encryptionKeyPointer
            = std::make_shared<std::array<uint8_t, 256>>(encryptionKey);

        auto rtc = std::vector<tgcalls::RtcServer>();

        for (const VoipServer& x : m_descriptor.Servers()) {
            rtc.push_back(tgcalls::RtcServer {
                string_to_unmanaged(x.Host()),
                x.Port(),
                string_to_unmanaged(x.Login()),
                string_to_unmanaged(x.Password()),
                x.IsTurn()
            });
        }

        auto a = 1 + 2;

        if (m_descriptor.VideoCapture()) {
            auto implementation = winrt::get_self<implementation::VoipVideoCapture>(m_descriptor.VideoCapture());
            m_capturer = implementation->m_impl;
        }


        tgcalls::Descriptor descriptor = tgcalls::Descriptor
        {
            /*config =*/ config,
            /*persistentState =*/ persistentState,
            /*endpoints =*/ std::vector<tgcalls::Endpoint>(),
            /*proxy =*/ NULL,
            /*rtcServers =*/ rtc,
            /*initialNetworkType =*/ tgcalls::NetworkType(),
            /*encryptionKey =*/ tgcalls::EncryptionKey(encryptionKeyPointer, m_descriptor.IsOutgoing()),
            /*mediaDevicesConfig =*/ mediaConfig,
            /*videoCapture =*/ m_capturer,
            /*stateUpdated =*/ [this](tgcalls::State state) {
                m_stateUpdatedEventSource(*this, (VoipState)state);
            },
            /*signalBarsUpdated =*/ [this](int signalBars) {
                m_signalBarsUpdatedEventSource(*this, signalBars);
            },
            /*remoteBatteryLevelIsLowUpdated =*/ [this](bool low) {
                m_remoteBatteryLevelIsLowUpdatedEventSource(*this, low);
            },
            /*remoteMediaStateUpdated =*/ [this](tgcalls::AudioState audio, tgcalls::VideoState video) {
                auto args = winrt::make_self<winrt::Unigram::Native::Calls::implementation::RemoteMediaStateUpdatedEventArgs>((VoipAudioState)audio, (VoipVideoState)video);
                m_remoteMediaStateUpdatedEventSource(*this, *args);
            },
            /*remotePrefferedAspectRatioUpdated =*/ [this](float aspect) {
                m_remotePrefferedAspectRatioUpdatedEventSource(*this, aspect);
            },
            /*signalingDataEmitted =*/ [this](std::vector<uint8_t> data) {
                auto bytes = winrt::single_threaded_vector<uint8_t>(std::move(data));
                auto args = winrt::make_self<winrt::Unigram::Native::Calls::implementation::SignalingDataEmittedEventArgs>(bytes);
                m_signalingDataEmittedEventSource(*this, *args);
            }
        };

        m_impl = tgcalls::Meta::Create("3.0.0", std::move(descriptor));
        //impl->setVideoCapture(capturer);
    }


    //void VoipManager::SetNetworkType(NetworkType networkType);

    void VoipManager::SetMuteMicrophone(bool muteMicrophone) {
        if (m_impl) {
            m_impl->setMuteMicrophone(muteMicrophone);
        }
    }

    void VoipManager::SetAudioOutputGainControlEnabled(bool enabled) {
        if (m_impl) {
            m_impl->setAudioOutputGainControlEnabled(enabled);
        }
    }

    void VoipManager::SetEchoCancellationStrength(int strength) {
        if (m_impl) {
            m_impl->setEchoCancellationStrength(strength);
        }
    }

    bool VoipManager::SupportsVideo() {
        if (m_impl) {
            return m_impl->supportsVideo();
        }

        return false;
    }

    //void SetIncomingVideoOutput(std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> sink);
    void VoipManager::SetIncomingVideoOutput(Windows::UI::Xaml::UIElement canvas) {
        if (m_impl) {
            if (canvas != nullptr) {
                //m_renderer = std::make_shared<VoipVideoRenderer>(canvas);
                m_impl->setIncomingVideoOutput(std::make_shared<VoipVideoRenderer>(canvas));
            }
            else {
                m_impl->setIncomingVideoOutput(nullptr);
                //m_renderer = nullptr;
            }
        }
    }



    void VoipManager::SetAudioInputDevice(hstring id) {
        if (m_impl) {
            m_impl->setAudioInputDevice(string_to_unmanaged(id));
        }
    }

    void VoipManager::SetAudioOutputDevice(hstring id) {
        if (m_impl) {
            m_impl->setAudioOutputDevice(string_to_unmanaged(id));
        }
    }

    void VoipManager::SetInputVolume(float level) {
        if (m_impl) {
            m_impl->setInputVolume(level);
        }
    }

    void VoipManager::SetOutputVolume(float level) {
        if (m_impl) {
            m_impl->setOutputVolume(level);
        }
    }

    void VoipManager::SetAudioOutputDuckingEnabled(bool enabled) {
        if (m_impl) {
            m_impl->setAudioOutputDuckingEnabled(enabled);
        }
    }

    void VoipManager::SetIsLowBatteryLevel(bool isLowBatteryLevel) {
        if (m_impl) {
            m_impl->setIsLowBatteryLevel(isLowBatteryLevel);
        }
    }



    //std::string getLastError();
    hstring VoipManager::GetDebugInfo() {
        if (m_impl) {
            std::string log = m_impl->getDebugInfo();
            size_t len = sizeof(wchar_t) * (log.length() + 1);
            wchar_t* wlog = (wchar_t*)malloc(len);
            MultiByteToWideChar(CP_UTF8, 0, log.c_str(), -1, wlog, len / sizeof(wchar_t));
            return hstring(wlog);
            return winrt::to_hstring(m_impl->getDebugInfo());
        }

        return L"";
    }

    int64_t VoipManager::GetPreferredRelayId() {
        if (m_impl) {
            return m_impl->getPreferredRelayId();
        }

        return -1;
    }
    //TrafficStats getTrafficStats();
    //PersistentState getPersistentState();



    void VoipManager::ReceiveSignalingData(IVector<uint8_t> const data) {
        if (m_impl) {
            auto bytes = std::vector<unsigned char>();
            for (const uint8_t& x : data) {
                bytes.push_back(x);
            }

            m_impl->receiveSignalingData(bytes);
        }
    }

    //virtual void setVideoCapture(std::shared_ptr<VideoCaptureInterface> videoCapture) = 0;
    void VoipManager::SetVideoCapture(Unigram::Native::Calls::VoipVideoCapture videoCapture) {
        if (m_impl) {
            if (videoCapture) {
                auto implementation = winrt::get_self<implementation::VoipVideoCapture>(videoCapture);
                m_capturer = implementation->m_impl;
            }
            else {
                m_capturer = nullptr;
            }

            m_impl->setVideoCapture(m_capturer);
        }
    }

    void VoipManager::SetRequestedVideoAspect(float aspect) {
        if (m_impl) {
            m_impl->setRequestedVideoAspect(aspect);
        }
    }



    winrt::event_token VoipManager::StateUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Unigram::Native::Calls::VoipManager,
        VoipState> const& value)
    {
        return m_stateUpdatedEventSource.add(value);
    }

    void VoipManager::StateUpdated(winrt::event_token const& token)
    {
        m_stateUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::SignalBarsUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Unigram::Native::Calls::VoipManager,
        int> const& value)
    {
        return m_signalBarsUpdatedEventSource.add(value);
    }

    void VoipManager::SignalBarsUpdated(winrt::event_token const& token)
    {
        m_signalBarsUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::RemoteBatteryLevelIsLowUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Unigram::Native::Calls::VoipManager,
        bool> const& value)
    {
        return m_remoteBatteryLevelIsLowUpdatedEventSource.add(value);
    }

    void VoipManager::RemoteBatteryLevelIsLowUpdated(winrt::event_token const& token)
    {
        m_remoteBatteryLevelIsLowUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::RemoteMediaStateUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Unigram::Native::Calls::VoipManager,
        winrt::Unigram::Native::Calls::RemoteMediaStateUpdatedEventArgs> const& value)
    {
        return m_remoteMediaStateUpdatedEventSource.add(value);
    }

    void VoipManager::RemoteMediaStateUpdated(winrt::event_token const& token)
    {
        m_remoteMediaStateUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::RemotePrefferedAspectRatioUpdated(Windows::Foundation::TypedEventHandler<
        winrt::Unigram::Native::Calls::VoipManager,
        float> const& value)
    {
        return m_remotePrefferedAspectRatioUpdatedEventSource.add(value);
    }

    void VoipManager::RemotePrefferedAspectRatioUpdated(winrt::event_token const& token)
    {
        m_remotePrefferedAspectRatioUpdatedEventSource.remove(token);
    }



    winrt::event_token VoipManager::SignalingDataEmitted(Windows::Foundation::TypedEventHandler<
        winrt::Unigram::Native::Calls::VoipManager, 
        winrt::Unigram::Native::Calls::SignalingDataEmittedEventArgs> const& value)
    {
        return m_signalingDataEmittedEventSource.add(value);
    }

    void VoipManager::SignalingDataEmitted(winrt::event_token const& token)
    {
        m_signalingDataEmittedEventSource.remove(token);
    }
} // namespace winrt::Unigram::Native::Calls::implementation
