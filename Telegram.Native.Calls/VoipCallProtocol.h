#pragma once

#include "VoipCallProtocol.g.h"

using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipCallProtocol : VoipCallProtocolT<VoipCallProtocol>
    {
        VoipCallProtocol(bool udpP2p, bool udpReflector, int32_t minLayer, int32_t maxLayer, IVector<hstring> libraryVersions);

        bool UdpP2p();
        bool UdpReflector();
        int32_t MinLayer();
        int32_t MaxLayer();
        IVector<hstring> LibraryVersions();

    private:
        bool m_udpP2p;
        bool m_udpReflector;
        int32_t m_minLayer;
        int32_t m_maxLayer;
        IVector<hstring> m_libraryVersions;
    };
}

namespace winrt::Telegram::Native::Calls::factory_implementation
{
    struct VoipCallProtocol : VoipCallProtocolT<VoipCallProtocol, implementation::VoipCallProtocol>
    {
    };
}
