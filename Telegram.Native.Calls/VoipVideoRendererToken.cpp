#include "pch.h"
#include "VoipVideoRendererToken.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipVideoRendererToken::VoipVideoRendererToken(std::shared_ptr<VoipVideoRenderer> sink, winrt::guid visualId)
        : m_sink(sink)
        , m_endpointId(L"")
        , m_visualId(visualId)
    {
    }

    VoipVideoRendererToken::VoipVideoRendererToken(std::shared_ptr<VoipVideoRenderer> sink, hstring endpointId, winrt::guid visualId)
        : m_sink(sink)
        , m_endpointId(endpointId)
        , m_visualId(visualId)
    {
    }

    winrt::Windows::UI::Xaml::Media::Stretch VoipVideoRendererToken::Stretch()
    {
        return m_sink->m_stretch;
    }

    void VoipVideoRendererToken::Stretch(winrt::Windows::UI::Xaml::Media::Stretch value)
    {
        m_sink->m_stretch = value;
    }

    bool VoipVideoRendererToken::IsMirrored()
    {
        return m_sink->m_flip;
    }

    void VoipVideoRendererToken::IsMirrored(bool value)
    {
        m_sink->m_flip = value;
    }

    bool VoipVideoRendererToken::Matches(hstring endpointId, winrt::guid visualId)
    {
        return m_endpointId == endpointId && m_visualId == visualId;
    }

    void VoipVideoRendererToken::Stop()
    {
        m_sink.reset();
        m_sink = nullptr;
    }
}
