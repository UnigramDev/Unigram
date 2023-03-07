#pragma once

#include "ScreenshotManager.g.h"

#include <winrt/Microsoft.UI.Xaml.Media.h>

namespace winrt::Unigram::Native::implementation
{
    struct ScreenshotManager : ScreenshotManagerT<ScreenshotManager>
    {
        static winrt::Microsoft::UI::Xaml::Media::ImageSource Capture();

        static winrt::Windows::Foundation::Rect GetWorkingArea();
    };
}

namespace winrt::Unigram::Native::factory_implementation
{
    struct ScreenshotManager : ScreenshotManagerT<ScreenshotManager, implementation::ScreenshotManager>
    {
    };
}
