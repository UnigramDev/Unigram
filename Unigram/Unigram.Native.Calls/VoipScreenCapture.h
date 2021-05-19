#pragma once

#include "VoipScreenCapture.g.h"

#include "VoipVideoCapture.h"

#include <winrt/Windows.Graphics.Capture.h>

using namespace winrt::Windows::Graphics::Capture;

namespace winrt::Unigram::Native::Calls::implementation
{
    struct VoipScreenCapture : VoipScreenCaptureT<VoipScreenCapture, VoipVideoCapture>
    {
        VoipScreenCapture(GraphicsCaptureItem item);

        winrt::event_token FatalErrorOccurred(Windows::Foundation::TypedEventHandler<
            winrt::Unigram::Native::Calls::VoipScreenCapture,
            winrt::Windows::Foundation::IInspectable> const& value);
        void FatalErrorOccurred(winrt::event_token const& token);

    private:
        winrt::event<Windows::Foundation::TypedEventHandler<
            winrt::Unigram::Native::Calls::VoipScreenCapture,
            winrt::Windows::Foundation::IInspectable>> m_fatalErrorOccurred;
    };
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
    struct VoipScreenCapture : VoipScreenCaptureT<VoipScreenCapture, implementation::VoipScreenCapture>
    {
    };
}
