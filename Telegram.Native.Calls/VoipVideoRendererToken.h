#pragma once

#include "VoipVideoRendererToken.g.h"

#include "VoipVideoRenderer.h"

#include "api/video/video_sink_interface.h"
#include "api/video/video_frame.h"

using namespace winrt::Microsoft::Graphics::Canvas::UI::Xaml;
using namespace winrt::Telegram::Td::Api;
using namespace winrt::Windows::Foundation::Collections;

namespace winrt::Telegram::Native::Calls::implementation
{
    struct VoipVideoRendererToken : VoipVideoRendererTokenT<VoipVideoRendererToken>
    {
        VoipVideoRendererToken(std::shared_ptr<VoipVideoRenderer> sink, winrt::guid visualId);
        VoipVideoRendererToken(std::shared_ptr<VoipVideoRenderer> sink, hstring endpointId, winrt::guid visualId);

        winrt::Windows::UI::Xaml::Media::Stretch Stretch();
        void Stretch(winrt::Windows::UI::Xaml::Media::Stretch value);

        bool IsMirrored();
        void IsMirrored(bool value);

        bool Matches(hstring endpointId, winrt::guid visualId);

        void Stop();

    private:
        std::shared_ptr<VoipVideoRenderer> m_sink;
        winrt::guid m_visualId;
        hstring m_endpointId;
    };
}
