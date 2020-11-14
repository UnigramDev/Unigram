// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

// clang-format off
#include "pch.h"
#include "VoipServer.h"
#include "VoipServer.g.cpp"
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

#include "Instance.h"
#include "InstanceImpl.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    VoipServer::VoipServer()
    {
    }

    hstring VoipServer::Host() {
        return m_host;
    }

    void VoipServer::Host(hstring value) {
        m_host = value;
    }

    uint16_t VoipServer::Port() {
        return m_port;
    }

    void VoipServer::Port(uint16_t value) {
        m_port = value;
    }

    hstring VoipServer::Login() {
        return m_login;
    }

    void VoipServer::Login(hstring value) {
        m_login = value;
    }

    hstring VoipServer::Password() {
        return m_password;
    }

    void VoipServer::Password(hstring value) {
        m_password = value;
    }

    bool VoipServer::IsTurn() {
        return m_isTurn;
    }

    void VoipServer::IsTurn(bool value) {
        m_isTurn = value;
    }

} // namespace winrt::Unigram::Native::Calls::implementation
