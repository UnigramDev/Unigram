#pragma once

#include "Composition/CompositionDevice.g.h"

#include "dcompex.h"
#include <winrt/windows.ui.composition.h>

using namespace winrt::Windows::UI::Composition;

namespace winrt::Unigram::Native::Composition::implementation
{
    static struct CompositionDevice : CompositionDeviceT<CompositionDevice>
    {
        CompositionDevice();

        static winrt::com_ptr<CompositionDevice> Current()
        {
            winrt::slim_lock_guard const guard(s_lock);

            if (s_current == nullptr) {
                s_current = winrt::make_self<CompositionDevice>();
            }

            return s_current;
        }

        HRESULT CreateCubicBezierAnimation(Compositor compositor, float from, float to, double duration, IDCompositionAnimation** slideAnimation);

        static winrt::Unigram::Native::Composition::DirectRectangleClip CreateRectangleClip(Visual visual);
        static void SetClip(Visual visual, winrt::Unigram::Native::Composition::DirectRectangleClip clip);

    private:
        winrt::com_ptr<IUIAnimationManager2> _manager;
        winrt::com_ptr<IUIAnimationTransitionLibrary2> _transitionLibrary;

        static winrt::slim_mutex s_lock;
        static winrt::com_ptr<CompositionDevice> s_current;
    };
}

namespace winrt::Unigram::Native::Composition::factory_implementation
{
    struct CompositionDevice : CompositionDeviceT<CompositionDevice, implementation::CompositionDevice>
    {
    };
}
