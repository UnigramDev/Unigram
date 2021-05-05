#pragma once

#include "VoipVideoRendererToken.g.h"

#include "api/video/video_sink_interface.h"
#include "api/video/video_frame.h"

using namespace winrt::Microsoft::Graphics::Canvas::UI::Xaml;

namespace winrt::Unigram::Native::Calls::implementation
{
    struct VoipVideoRendererToken : VoipVideoRendererTokenT<VoipVideoRendererToken>
    {
        VoipVideoRendererToken(std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> sink, hstring endpointId, CanvasControl canvasControl);

        bool IsMatch(hstring endpointId, CanvasControl canvasControl);

    private:
        std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> m_sink;
        hstring m_endpointId;
        CanvasControl m_canvasControl{ nullptr };
    };
}
