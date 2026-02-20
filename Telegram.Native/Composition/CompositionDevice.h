#pragma once

#include "Composition/CompositionDevice.g.h"

#pragma push_macro("WINAPI_FAMILY")
#undef WINAPI_FAMILY
#define WINAPI_FAMILY WINAPI_FAMILY_DESKTOP_APP
#define IUnknown ::IUnknown
#include <UIAnimation.h>
#include <dcomp.h>
#pragma pop_macro("WINAPI_FAMILY")
#undef IUnknown

#include <winrt/windows.ui.composition.h>
#include <winrt/windows.ui.xaml.h>

using namespace winrt::Windows::UI::Composition;
using namespace winrt::Windows::UI::Xaml;

using PFN_CreateVisual = HRESULT(WINAPI*)(IDCompositionDevice2* pThis, IDCompositionVisual2** ppVisual);

namespace winrt::Telegram::Native::Composition::implementation
{
    struct CompositionDevice : CompositionDeviceT<CompositionDevice>
    {
        CompositionDevice();

        static winrt::com_ptr<CompositionDevice> Current()
        {
            std::lock_guard const guard(s_lock);

            if (s_current == nullptr) {
                s_current = winrt::make_self<CompositionDevice>();
            }

            return s_current;
        }

        HRESULT CreateCubicBezierAnimation(Compositor compositor, float from, float to, double duration, IDCompositionAnimation** slideAnimation);

        static winrt::Telegram::Native::Composition::DirectRectangleClip2 CreateRectangleClip2(UIElement const& element);
        static winrt::Telegram::Native::Composition::DirectRectangleClip2 CreateRectangleClip2(Visual const& visual);
        static void SetClip(Visual const& visual, winrt::Telegram::Native::Composition::DirectRectangleClip2 const& clip);

        static LayerVisual GetElementLayerVisual(UIElement const& element);

    private:
        winrt::com_ptr<IUIAnimationManager2> _manager;
        winrt::com_ptr<IUIAnimationTransitionLibrary2> _transitionLibrary;

        static bool s_Hooked;
        static DWORD s_ThreadId;
        static std::mutex s_Mutex;
        static PFN_CreateVisual s_CreateVisual;

        static void EnsureHooked();
        static HRESULT WINAPI CreateVisualHook(IDCompositionDevice2* pThis, IDCompositionVisual2** ppVisual);

        static std::mutex s_lock;
        static winrt::com_ptr<CompositionDevice> s_current;
    };
}

namespace winrt::Telegram::Native::Composition::factory_implementation
{
    struct CompositionDevice : CompositionDeviceT<CompositionDevice, implementation::CompositionDevice>
    {
    };
}
