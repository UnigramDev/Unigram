#pragma once

#include "VoipCallServerTypeTelegramReflector.g.h"
#include "VoipCallServerTypeWebrtc.g.h"
#include "VoipCallServer.g.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipCallServerTypeTelegramReflector : VoipCallServerTypeTelegramReflectorT<VoipCallServerTypeTelegramReflector>
    {
        VoipCallServerTypeTelegramReflector(hstring peerTag, bool isTcp);

        hstring PeerTag();
        bool IsTcp();

    private:
        hstring m_peerTag;
        bool m_isTcp;
    };

    struct VoipCallServerTypeWebrtc : VoipCallServerTypeWebrtcT<VoipCallServerTypeWebrtc>
    {
        VoipCallServerTypeWebrtc(hstring username, hstring password, bool supportsTurn, bool supportsStun);

        hstring Username();
        hstring Password();
        bool SupportsTurn();
        bool SupportsStun();

    private:
        hstring m_username;
        hstring m_password;
        bool m_supportsTurn;
        bool m_supportsStun;
    };

    struct VoipCallServer : VoipCallServerT<VoipCallServer>
    {
        VoipCallServer(int64_t id, hstring ipAddress, hstring ipv6Address, int32_t port, VoipCallServerType type);

        int64_t Id();
        hstring IpAddress();
        hstring Ipv6Address();
        int32_t Port();
        VoipCallServerType Type();

    private:
        int64_t m_id;
        hstring m_ipAddress;
        hstring m_ipv6Address;
        int32_t m_port;
        VoipCallServerType m_type;
    };
}

namespace winrt::Telegram::Native::Calls::factory_implementation
{
    struct VoipCallServerTypeTelegramReflector : VoipCallServerTypeTelegramReflectorT<VoipCallServerTypeTelegramReflector, implementation::VoipCallServerTypeTelegramReflector>
    {
    };

    struct VoipCallServerTypeWebrtc : VoipCallServerTypeWebrtcT<VoipCallServerTypeWebrtc, implementation::VoipCallServerTypeWebrtc>
    {
    };

    struct VoipCallServer : VoipCallServerT<VoipCallServer, implementation::VoipCallServer>
    {
    };
}
