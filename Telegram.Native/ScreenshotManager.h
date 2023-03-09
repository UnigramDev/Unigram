#pragma once

#include "ScreenshotManager.g.h"

#include <winrt/Windows.UI.Xaml.Media.h>

namespace winrt::Telegram::Native::implementation
{
    struct ScreenshotManager : ScreenshotManagerT<ScreenshotManager>
    {
        static winrt::Windows::UI::Xaml::Media::ImageSource Capture();

        static winrt::Windows::Foundation::Rect GetWorkingArea();
    };
}

namespace winrt::Telegram::Native::factory_implementation
{
    struct ScreenshotManager : ScreenshotManagerT<ScreenshotManager, implementation::ScreenshotManager>
    {
    };
}
