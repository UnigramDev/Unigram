#include "pch.h"
#include "VoipCallProtocol.h"
#if __has_include("VoipCallProtocol.g.cpp")
#include "VoipCallProtocol.g.cpp"
#endif

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipCallProtocol::VoipCallProtocol(bool udpP2p, bool udpReflector, int32_t minLayer, int32_t maxLayer, IVector<hstring> libraryVersions)
        : m_udpP2p(udpP2p)
        , m_udpReflector(udpReflector)
        , m_minLayer(minLayer)
        , m_maxLayer(maxLayer)
        , m_libraryVersions(libraryVersions)
    {

    }

    bool VoipCallProtocol::UdpP2p()
    {
        return m_udpP2p;
    }

    bool VoipCallProtocol::UdpReflector()
    {
        return m_udpReflector;
    }

    int32_t VoipCallProtocol::MinLayer()
    {
        return m_minLayer;
    }

    int32_t VoipCallProtocol::MaxLayer()
    {
        return m_maxLayer;
    }

    IVector<hstring> VoipCallProtocol::LibraryVersions()
    {
        return m_libraryVersions;
    }
}
