#pragma once

#include "Composition/CompositionDevice.g.h"

#pragma push_macro("WINAPI_FAMILY")
#undef WINAPI_FAMILY
#define WINAPI_FAMILY WINAPI_FAMILY_DESKTOP_APP
#define IUnknown ::IUnknown
#include <dcomp.h>
#pragma pop_macro("WINAPI_FAMILY")
#undef IUnknown

using namespace winrt::UIComposition;
using namespace winrt::UIXaml;

using PFN_CreateVisual = HRESULT(WINAPI*)(IDCompositionDevice2* pThis, IDCompositionVisual2** ppVisual);

namespace winrt::Telegram::Native::Composition::implementation
{
    struct CompositionDevice : CompositionDeviceT<CompositionDevice>
    {
        static winrt::Telegram::Native::Composition::DirectRectangleClip2 CreateRectangleClip2(UIElement const& element);
        static winrt::Telegram::Native::Composition::DirectRectangleClip2 CreateRectangleClip2(Visual const& visual);
        static void SetClip(Visual const& visual, winrt::Telegram::Native::Composition::DirectRectangleClip2 const& clip);

        static LayerVisual GetElementLayerVisual(UIElement const& element);

    private:
        static bool s_Hooked;
        static DWORD s_ThreadId;
        static std::mutex s_Mutex;
        static PFN_CreateVisual s_CreateVisual;

        static void EnsureHooked();
        static HRESULT WINAPI CreateVisualHook(IDCompositionDevice2* pThis, IDCompositionVisual2** ppVisual);
    };
}

namespace winrt::Telegram::Native::Composition::factory_implementation
{
    struct CompositionDevice : CompositionDeviceT<CompositionDevice, implementation::CompositionDevice>
    {
    };
}
