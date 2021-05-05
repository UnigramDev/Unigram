#include "pch.h"
#include "VoipVideoRendererToken.h"

namespace winrt::Unigram::Native::Calls::implementation
{
    VoipVideoRendererToken::VoipVideoRendererToken(std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> sink, hstring endpointId, CanvasControl canvasControl) {
        m_sink = sink;
        m_endpointId = endpointId;
        m_canvasControl = canvasControl;
    }

    bool VoipVideoRendererToken::IsMatch(hstring endpointId, CanvasControl canvasControl)
    {
        return m_endpointId == endpointId && m_canvasControl == canvasControl;
    }
}
