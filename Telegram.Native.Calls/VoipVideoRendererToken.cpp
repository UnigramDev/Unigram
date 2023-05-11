#include "pch.h"
#include "VoipVideoRendererToken.h"

namespace winrt::Telegram::Native::Calls::implementation
{
    VoipVideoRendererToken::VoipVideoRendererToken(std::shared_ptr<VoipVideoRenderer> sink, int32_t audioSource, hstring endpointId, IVector<GroupCallVideoSourceGroup> sourceGroups, CanvasControl canvasControl)
        : m_sink(sink),
        m_audioSource(audioSource),
        m_endpointId(endpointId),
        m_sourceGroups(sourceGroups),
        m_canvasControl(std::make_shared<CanvasControl>(canvasControl))
    {
    }

    int32_t VoipVideoRendererToken::AudioSource() {
        return m_audioSource;
    }

    hstring VoipVideoRendererToken::EndpointId() {
        return m_endpointId;
    }

    IVector<GroupCallVideoSourceGroup> VoipVideoRendererToken::SourceGroups() {
        return m_sourceGroups;
    }

    winrt::Windows::UI::Xaml::Media::Stretch VoipVideoRendererToken::Stretch() {
        return m_sink->m_stretch;
    }

    void VoipVideoRendererToken::Stretch(winrt::Windows::UI::Xaml::Media::Stretch value) {
        m_sink->m_stretch = value;
    }

    bool VoipVideoRendererToken::IsMirrored() {
        return m_sink->m_flip;
    }

    void VoipVideoRendererToken::IsMirrored(bool value) {
        m_sink->m_flip = value;
    }

    bool VoipVideoRendererToken::IsMatch(hstring endpointId, CanvasControl canvasControl)
    {
        return m_endpointId == endpointId && *m_canvasControl == canvasControl;
    }

    void VoipVideoRendererToken::Stop() {
        m_canvasControl.reset();
        m_canvasControl = nullptr;

        m_sink.reset();
        m_sink = nullptr;
    }
}
