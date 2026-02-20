#include "pch.h"
#include "VoipCallServer.h"
#if __has_include("VoipCallServerTypeTelegramReflector.g.cpp")
#include "VoipCallServerTypeTelegramReflector.g.cpp"
#endif
#if __has_include("VoipCallServerTypeWebrtc.g.cpp")
#include "VoipCallServerTypeWebrtc.g.cpp"
#endif
#if __has_include("VoipCallServer.g.cpp")
#include "VoipCallServer.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipCallServerTypeTelegramReflector::VoipCallServerTypeTelegramReflector(hstring peerTag, bool isTcp)
        : m_peerTag(peerTag)
        , m_isTcp(isTcp)
    {

    }

    hstring VoipCallServerTypeTelegramReflector::PeerTag()
    {
        return m_peerTag;
    }

    bool VoipCallServerTypeTelegramReflector::IsTcp()
    {
        return m_isTcp;
    }

    VoipCallServerTypeWebrtc::VoipCallServerTypeWebrtc(hstring username, hstring password, bool supportsTurn, bool supportsStun)
        : m_username(username)
        , m_password(password)
        , m_supportsTurn(supportsTurn)
        , m_supportsStun(supportsStun)
    {

    }

    hstring VoipCallServerTypeWebrtc::Username()
    {
        return m_username;
    }

    hstring VoipCallServerTypeWebrtc::Password()
    {
        return m_password;
    }

    bool VoipCallServerTypeWebrtc::SupportsTurn()
    {
        return m_supportsTurn;
    }

    bool VoipCallServerTypeWebrtc::SupportsStun()
    {
        return m_supportsStun;
    }

    VoipCallServer::VoipCallServer(int64_t id, hstring ipAddress, hstring ipv6Address, int32_t port, VoipCallServerType type)
        : m_id(id)
        , m_ipAddress(ipAddress)
        , m_ipv6Address(ipv6Address)
        , m_port(port)
        , m_type(type)
    {

    }

    int64_t VoipCallServer::Id()
    {
        return m_id;
    }

    hstring VoipCallServer::IpAddress()
    {
        return m_ipAddress;
    }

    hstring VoipCallServer::Ipv6Address()
    {
        return m_ipv6Address;
    }

    int32_t VoipCallServer::Port()
    {
        return m_port;
    }

    VoipCallServerType VoipCallServer::Type()
    {
        return m_type;
    }
}
