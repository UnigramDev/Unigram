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
    };
}

namespace winrt::Unigram::Native::Calls::factory_implementation
{
    struct VoipScreenCapture : VoipScreenCaptureT<VoipScreenCapture, implementation::VoipScreenCapture>
    {
    };
}
