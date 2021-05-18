#pragma once

#include "VoipVideoRendererToken.g.h"

#include "api/video/video_sink_interface.h"
#include "api/video/video_frame.h"

using namespace winrt::Microsoft::Graphics::Canvas::UI::Xaml;

namespace winrt::Unigram::Native::Calls::implementation
{
    struct VoipVideoRendererToken : VoipVideoRendererTokenT<VoipVideoRendererToken>
    {
        VoipVideoRendererToken(std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> sink, int32_t audioSource, hstring endpointId, hstring description, CanvasControl canvasControl);

        int32_t AudioSource();
        hstring EndpointId();
        hstring Description();

        bool IsMatch(hstring endpointId, CanvasControl canvasControl);

        void Stop();

    private:
        std::shared_ptr<rtc::VideoSinkInterface<webrtc::VideoFrame>> m_sink;
        int32_t m_audioSource;
        hstring m_endpointId;
        hstring m_description;
        CanvasControl m_canvasControl{ nullptr };
    };
}
