#pragma once

#include "Composition.CompositionDevice.g.h"

#include <winrt/windows.ui.composition.h>

using namespace winrt::Windows::UI::Composition;

namespace winrt::Unigram::Native::Composition::implementation
{
    static struct CompositionDevice : CompositionDeviceT<CompositionDevice>
    {
        static winrt::Unigram::Native::Composition::DirectRectangleClip CreateRectangleClip(Visual visual);
    };
}

namespace winrt::Unigram::Native::Composition::factory_implementation
{
    struct CompositionDevice : CompositionDeviceT<CompositionDevice, implementation::CompositionDevice>
    {
    };
}
